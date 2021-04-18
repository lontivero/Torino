using System;
using System.Threading.Tasks;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.IO;
using System.Text;

namespace Torino
{
	public class TorController : IDisposable
	{
		private const int DEFAULT_TOR_CONTROL_PORT = 9051;

		private ControlSocket _controlSocket;
		private Channel<Response> _replyChannel = new();
		private Channel<AsyncReply> _asyncEventNotificationChannel = new();
		private Dictionary<AsyncEvent, EventHandler<AsyncReply>> _asyncEventHandler = new(); 
		private Dictionary<string, object> _cache = new();
		private CancellationTokenSource _cancellation = new();
		private DateTime _lastNewnym;

		public bool IsAuthenticated { get; private set; }


		public TorController()
			: this(IPAddress.Loopback, DEFAULT_TOR_CONTROL_PORT)
		{}

		public static async Task<TorController> UseControlPortFileAsync(string controlPortFilePath)
		{
			var content = await File.ReadAllLinesAsync(controlPortFilePath).ConfigureAwait(false);
			var endpointStr = content[0]["PORT=".Length..];
			if (!IPEndPoint.TryParse(endpointStr, out var ipendpoint))
			{
				throw new FormatException("Unsupported endpoint format.");
			}
			return new TorController(ipendpoint);
		}

		public TorController(IPAddress address, int port = DEFAULT_TOR_CONTROL_PORT)
			: this(new IPEndPoint(address, port))
		{}

		public TorController(IPEndPoint endPoint)
		{
			_controlSocket = new ControlSocket(endPoint);
			StartListening();
		}

		public Task AddEventHandlerAsync(
			AsyncEvent asyncEvent, 
			EventHandler<AsyncReply> handler, 
			CancellationToken cancellationToken = default(CancellationToken))
		{
			if (_asyncEventHandler.TryGetValue(asyncEvent, out var existingHandler))
			{
				_asyncEventHandler[asyncEvent] += handler;
			}
			else
			{
				_asyncEventHandler.Add(asyncEvent, handler);
				return SetSubscribedEventsAsync(cancellationToken);
			}
			return Task.CompletedTask;
		}

		public async Task RemoveEventHandlerAsync(
			AsyncEvent asyncEvent, 
			EventHandler<AsyncReply> handler, 
			CancellationToken cancellationToken = default(CancellationToken))
		{
			if (_asyncEventHandler.TryGetValue(asyncEvent, out var existingHandler))
			{
#pragma warning disable CS8601
				_asyncEventHandler[asyncEvent] -= handler;
#pragma warning restore CS8601
				if (_asyncEventHandler[asyncEvent] is null)
				{
					_asyncEventHandler.Remove(asyncEvent);
				}
				await SetSubscribedEventsAsync(cancellationToken).ConfigureAwait(false);
			}
		}

		public async Task AuthenticateAsync(string password, CancellationToken cancellationToken = default(CancellationToken))
		{
			var protocolInfo = await GetProtocolInfoAsync(cancellationToken).ConfigureAwait(false);
			var authString = "";
			if (protocolInfo.AuthMethods.Contains(AuthMethod.HASHEDPASSWORD) && !string.IsNullOrEmpty(password))
			{
				authString = $"\"{password}\"";
			}
			else if (protocolInfo.AuthMethods.Contains(AuthMethod.COOKIE) && !string.IsNullOrEmpty(protocolInfo.CookieFile))
			{
				static string ToHex(byte[] bytes)
				{
					var result = new StringBuilder(bytes.Length * 2);
					var hexAlphabet = "0123456789ABCDEF";

					foreach (byte b in bytes)
					{
						result.Append(hexAlphabet[b >> 4]);
						result.Append(hexAlphabet[b & 0xF]);
					}

					return result.ToString();
				}
				authString = ToHex(File.ReadAllBytes(protocolInfo.CookieFile[1..^1]));
			}
			else if (protocolInfo.AuthMethods.Contains(AuthMethod.NULL))
			{
				authString = "";
			}
			else
			{
				throw new NotSupportedException("Not supported authentication method.");
			}

			await SendCommandAsync(Command.AUTHENTICATE, $"{authString}", cancellationToken).ConfigureAwait(false);
			IsAuthenticated = true;
		}

		public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			if (!_cache.TryGetValue("version", out var version))
			{
				var info = await GetInfoAsync("version", cancellationToken).ConfigureAwait(false);
				version = info["version"];
				_cache.Add("version", version);
			}
			return (string)version;
		}

		public async Task<string> GetUserAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			if (!_cache.TryGetValue("user", out var user))
			{
				var info = await GetInfoAsync("process/user", cancellationToken).ConfigureAwait(false);
				user = info["process/user"];
				_cache.Add("user", user);
			}
			return (string)user;
		}

		public async Task<int> GetPidAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			if (!_cache.TryGetValue("pid", out var pid))
			{
				var info = await GetInfoAsync("process/pid", cancellationToken).ConfigureAwait(false);
				pid = int.Parse(info["process/pid"]);
				_cache.Add("pid", pid);
			}
			return (int)pid;
		}

		public async Task<MultiLineReply> GetInfoAsync(string param, CancellationToken cancellationToken = default(CancellationToken))
		{
			var reply = await SendCommandAsync(Command.GETINFO, param).ConfigureAwait(false);
			return new MultiLineReply(reply);
		}

		public async Task SignalAsync(Signal signal, CancellationToken cancellationToken = default(CancellationToken))
		{
			var reply = await SendCommandAsync(Command.SIGNAL, signal.ToString(), cancellationToken).ConfigureAwait(false);
			var singleLine = new SingleLineReply(reply);

			if (singleLine.IsOK && signal == Signal.NEWNYM)
			{
				_lastNewnym = DateTime.UtcNow;
			}
		}

		public Task<HiddenServiceReply> CreateEphemeralHiddenServiceAsync(
			string port, 
			OnionKeyType keyType = OnionKeyType.NEW, 
			OnionKeyBlob keyBob = OnionKeyBlob.BEST, 
			OnionFlags flags = OnionFlags.None,
			int maxStreams = 0,
			bool waitForPublication = false,
			CancellationToken cancellationToken = default(CancellationToken)) => 
				CreateEphemeralHiddenServiceAsync( new Dictionary<string, string>{ { port, port} }, 
					keyType, keyBob, flags, maxStreams, waitForPublication, cancellationToken);

		public async Task<HiddenServiceReply> CreateEphemeralHiddenServiceAsync(
			IDictionary<string, string> ports, 
			OnionKeyType keyType = OnionKeyType.NEW, 
			OnionKeyBlob keyBob = OnionKeyBlob.BEST, 
			OnionFlags flags = OnionFlags.None,
			int maxStreams = 0,
			bool waitForPublication = false,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			var serviceId = string.Empty;
			var publication = new TaskCompletionSource<int>();

			var uploading = new HashSet<string>();
			var failures = 0;

			void HiddenServicePublicationHandler(object? sender, AsyncReply e)
			{
				if ( e is not HiddenServiceDescriptorEvent hsDescEvent || hsDescEvent.Address == serviceId)
				{
					return;
				}
				if (hsDescEvent.Action == HsDescActions.UPLOADED && uploading.Contains(hsDescEvent.HsDir))
				{
					publication.TrySetResult(uploading.Count);
				}
				else if (hsDescEvent.Action == HsDescActions.FAILED && uploading.Contains(hsDescEvent.HsDir))
				{
					failures++;
					if (failures == uploading.Count())
					{
						publication.TrySetException(new Exception($"Fail to publish hidden servive: {serviceId}"));
					}
				}
				else if (hsDescEvent.Action == HsDescActions.UPLOAD)
				{
					uploading.Add(hsDescEvent.HsDir);
				}
			}

			if (waitForPublication)
			{
				await AddEventHandlerAsync(AsyncEvent.HS_DESC, HiddenServicePublicationHandler).ConfigureAwait(false);
			}

			var request = $"{keyType}:{keyBob}";
			var flagsStr = flags switch
			{
				OnionFlags.Detach => "Flags=Detach",
				OnionFlags.DiscardPK => "Flags=DiscardPK",
				OnionFlags.Detach | OnionFlags.DiscardPK => "Flags=Detach,DiscardPK",
				_ => null 
			};


			if (flagsStr is { })
			{
				request = $"{request} {flagsStr}";
			}

			if (maxStreams > 0)
			{
				request = $"{request} MaxStreams={maxStreams}";
			}

			var portMappingList = ports.Select(portMapping => 
				portMapping.Key == portMapping.Value
					? $"Port={portMapping.Key}"
					: $"Port={portMapping.Key}:{portMapping.Value}");

			request = $"{request} {string.Join(" ", portMappingList)}";

			var reply = await SendCommandAsync(Command.ADD_ONION, request, cancellationToken).ConfigureAwait(false);
			var hsReply = new HiddenServiceReply(reply);
			serviceId = hsReply.ServiceId;

			if (waitForPublication)
			{
				try
				{
					await publication.Task;
				}
				finally
				{
					await RemoveEventHandlerAsync(AsyncEvent.HS_DESC, HiddenServicePublicationHandler).ConfigureAwait(false);
				}
			}
			return new HiddenServiceReply(reply);
		}


		public async Task<string[]> ListEphemeralHiddenServicesAsync(
			bool includeDetached = false, 
			CancellationToken cancellationToken = default(CancellationToken))
		{
			var reply = await GetInfoAsync("onions/current").ConfigureAwait(false);
			var hsList = new List<string>();
			hsList.AddRange(reply.Keys.Skip(1).Select(key => reply[key]));

			if (includeDetached)
			{
				reply = await GetInfoAsync("onions/detached").ConfigureAwait(false);
				hsList.AddRange(reply.Keys.Skip(1).Select(key => reply[key]));
			}
			return hsList.ToArray();
		}

		public async Task<bool> RemoveHiddenServiceAsync(
			string serviceId,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			var reply = await SendCommandAsync(Command.DEL_ONION, serviceId, cancellationToken).ConfigureAwait(false);
			return reply.IsOk;
		}

		public async Task<string> ResolveAsync(string address, bool isReverse = false, CancellationToken cancellationToken = default(CancellationToken))
		{
			var tcs = new TaskCompletionSource<string>();
			async void Test(object? sender, AsyncReply e)
			{
				if (e is NewAddressMappingEvent addmapEvent && addmapEvent.Address == address)
				{
					tcs.TrySetResult(addmapEvent.NewAddress);
					if (!_cancellation.IsCancellationRequested)
					{
						await RemoveEventHandlerAsync(AsyncEvent.ADDRMAP, Test, cancellationToken).ConfigureAwait(false);
					}
				}
			}
			await AddEventHandlerAsync(AsyncEvent.ADDRMAP, Test, cancellationToken).ConfigureAwait(false);

			var args = $"{address}";
			if (isReverse)
			{
				args = $"mode=reverse {args}";
			}
			var reply = await SendCommandAsync(Command.RESOLVE, args, cancellationToken).ConfigureAwait(false);

			return await tcs.Task;
		}

		public async Task DropGuardsAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			await SendCommandAsync(Command.DROPGUARDS, cancellationToken: cancellationToken);
		}

		public async Task<ProtocolInfoReply> GetProtocolInfoAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			var reply = await SendCommandAsync(Command.PROTOCOLINFO, cancellationToken: cancellationToken).ConfigureAwait(false);
			var protocolInfo = new ProtocolInfoReply(reply);
			return protocolInfo;
		}

		public async Task LoadConfigAsync(string configPath, CancellationToken cancellationToken = default(CancellationToken))
		{
			await SendCommandAsync(Command.LOADCONF, configPath, cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		public async Task SaveConfigAsync(bool force = false, CancellationToken cancellationToken = default(CancellationToken))
		{
			await SendCommandAsync(Command.SAVECONF, force ? "force": "", cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		public async Task<CircuitEvent[]> GetCircuitsAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			var reply = await GetInfoAsync("circuit-status", cancellationToken).ConfigureAwait(false);
			return reply.Keys.Skip(1).Select(key => new CircuitEvent(reply[key])).ToArray();
		}


		private bool TryGetCached<T>(string key, string @namespace, out T? value)
		{
			var lookupKey = string.IsNullOrWhiteSpace(@namespace)
				? key
				: $"{@namespace}:{key}";
			var ret = _cache.TryGetValue(lookupKey, out var cachedValue);
			value = (T?)cachedValue;
			return ret; ;
		}

		private Dictionary<string, object> GetCachedValues(string[] keys, string @namespace = "")
		{
			var ret = new Dictionary<string, object>();
			foreach(var key in keys)
			{
				var lookupKey = string.IsNullOrWhiteSpace(@namespace)
					? key
					: $"{@namespace}:{key}";
				if(_cache.TryGetValue(lookupKey, out var value))
				{
					ret.Add(lookupKey, value);
				}
			}
			return ret;
		}

		public async Task CloseAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			await SendCommandAsync(Command.QUIT, cancellationToken: cancellationToken).ConfigureAwait(false);
			this._controlSocket.Close();
		}

		public void Dispose()
		{
			this._cancellation.Cancel();
			this._controlSocket.Close();
		}

		private async Task<Response> SendCommandAsync(Command command, string args = "", CancellationToken cancellationToken = default(CancellationToken))
		{
			var nextReply = CleanReplyChannelAsync(cancellationToken);
			var request = $"{command}";
			if (!string.IsNullOrEmpty(args))
			{
				request = $"{request} {args}";
			}
			await _controlSocket.SendAsync($"{request}\r\n", cancellationToken).ConfigureAwait(false);
			var reply = await nextReply;

			var sl = new SingleLineReply(reply);
			if (sl.Code != ReplyCode.OK && sl.Code != ReplyCode.UNNECESSARY_OPERATION)
			{
				throw new Exception(sl.Line);
			}

			return reply;
		}

		private Task<Response> CleanReplyChannelAsync(CancellationToken cancellationToken)
		{
			var replyTask =  _replyChannel.TakeAsync(cancellationToken);
			while (replyTask.Status == TaskStatus.RanToCompletion)
			{
				replyTask =  _replyChannel.TakeAsync(cancellationToken);
			}
			return replyTask;
		}

		private IEnumerable<string> GetSettledAsyncEventNames()
		{
			return _asyncEventHandler.Where(x => x.Value.GetInvocationList().Any()).Select(x => x.Key.ToString());
		}

		private async Task SetSubscribedEventsAsync(CancellationToken cancellationToken)
		{
			await SendCommandAsync(Command.SETEVENTS, string.Join(" ", GetSettledAsyncEventNames()), cancellationToken).ConfigureAwait(false);
		}

		private void StartListening()
		{
			Task.Run(async () =>
			{
				while (true)
				{
					if (_cancellation.Token.IsCancellationRequested) break;
					var reply = await _controlSocket.ReceiveAsync(_cancellation.Token).ConfigureAwait(false);

					if (reply.Entries[0].StatusCode == ReplyCode.ASYNC_EVENT_NOTIFICATION)
					{
						var asyncEvent = AsyncReply.Parse(reply.Entries[0]);
						_asyncEventNotificationChannel.Send(asyncEvent);
					}
					else
					{
						_replyChannel.Send(reply);
					}
				}
			}, _cancellation.Token);

			Task.Run(async () =>
			{
				while (true)
				{
					if (_cancellation.Token.IsCancellationRequested) break;
					var asyncEvent = await _asyncEventNotificationChannel.TakeAsync(_cancellation.Token).ConfigureAwait(false);

					if (_asyncEventHandler.TryGetValue(asyncEvent.Event, out var handler))
					{
						handler?.Invoke(this, asyncEvent);
					}
				}
			}, _cancellation.Token);
		}
	}
}
