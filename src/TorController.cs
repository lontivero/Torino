using System;
using System.Threading.Tasks;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace Torino
{
	public class TorController : IDisposable
	{
		private ControlSocket _controlSocket;
		private Channel<Response> _replyChannel = new Channel<Response>();
		private Channel<AsyncReply> _asyncEventNotificationChannel = new Channel<AsyncReply>();
		private Dictionary<AsyncEvent, EventHandler<AsyncReply>> _asyncEventHandler = new Dictionary<AsyncEvent, EventHandler<AsyncReply>>(); 
		private Dictionary<string, object> _cache = new Dictionary<string, object>();
		private CancellationTokenSource _cancellation = new CancellationTokenSource();
		private DateTime _lastNewnym;

		public bool IsAuthenticated { get; private set; }


		public TorController()
			: this(IPAddress.Loopback, 9051)
		{}

		public TorController(IPAddress address, int port = 9051)
			: this(new IPEndPoint(address, port))
		{}

		public TorController(IPEndPoint endPoint)
		{
			_controlSocket = new ControlSocket(endPoint);
			StartListening();
		}

		public async Task AuthenticateAsync(string password, CancellationToken cancellationToken = default(CancellationToken))
		{
			password ??= string.Empty;

			await SendCommandAsync(Command.AUTHENTICATE, $"\"{password}\"", cancellationToken);
			IsAuthenticated = true;
		}

		public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			if (!_cache.TryGetValue("version", out var version))
			{
				var info = await GetInfoAsync("version", cancellationToken);
				version = info["version"];
				_cache.Add("version", version);
			}
			return (string)version;
		}

		public async Task<string> GetUserAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			if (!_cache.TryGetValue("user", out var user))
			{
				var info = await GetInfoAsync("process/user", cancellationToken);
				user = info["process/user"];
				_cache.Add("user", user);
			}
			return (string)user;
		}

		public async Task<MultiLineReply> GetInfoAsync(string param, CancellationToken cancellationToken = default(CancellationToken))
		{
			var reply = await SendCommandAsync(Command.GETINFO, param);
			return new MultiLineReply(reply);
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
				_asyncEventHandler[asyncEvent] -= handler;
				if (_asyncEventHandler[asyncEvent] is null)
				{
					_asyncEventHandler.Remove(asyncEvent);
				}
				await SetSubscribedEventsAsync(cancellationToken);
			}
		}

		public async Task SignalAsync(Signal signal, CancellationToken cancellationToken = default(CancellationToken))
		{
			var reply = await SendCommandAsync(Command.SIGNAL, signal.ToString(), cancellationToken);
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
			void Test(object sender, AsyncReply e)
			{
				var hsDescEvent = e as HiddenServiceDescriptorEvent;
				if (hsDescEvent.Action == HsDescActions.UPLOADED && hsDescEvent.Address == serviceId && uploading.Contains(hsDescEvent.HsDir))
				{
					publication.TrySetResult(uploading.Count);
				}
				else if (hsDescEvent.Action == HsDescActions.FAILED && hsDescEvent.Address == serviceId && uploading.Contains(hsDescEvent.HsDir))
				{
					failures++;
					if (failures == uploading.Count())
					{
						publication.TrySetException(new Exception($"Fail to publish hidden servive: {serviceId}"));
					}
				}
				else if (hsDescEvent.Action == HsDescActions.UPLOAD && hsDescEvent.Address == serviceId)
				{
					uploading.Add(hsDescEvent.HsDir);
				}
			}

			if (waitForPublication)
			{
				await AddEventHandlerAsync(AsyncEvent.HS_DESC, Test);
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

			var portMappingList = new List<string>(); 
			foreach(var portMapping in ports)
			{
				if (portMapping.Key == portMapping.Value)
				{
					portMappingList.Add($"Port={portMapping.Key}");
				}
				else
				{
					portMappingList.Add($"Port={portMapping.Key}:{portMapping.Value}");
				}
			}
			request = $"{request} {string.Join(" ", portMappingList)}";

			var reply = await SendCommandAsync(Command.ADD_ONION, request, cancellationToken);
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
					await RemoveEventHandlerAsync(AsyncEvent.HS_DESC, Test);
				}
			}
			return new HiddenServiceReply(reply);
		}


		public async Task<string[]> ListEphemeralHiddenServicesAsync(
			bool includeDetached = false, 
			CancellationToken cancellationToken = default(CancellationToken))
		{
			var result = await GetInfoAsync("onions/current");
			var hsList = new List<string>();
			hsList.AddRange(result["onions/current"].Split('\n', StringSplitOptions.RemoveEmptyEntries));

			if (includeDetached)
			{
				result = await GetInfoAsync("onions/detached");
				
				hsList.AddRange(result["onions/detached"].Split('\n', StringSplitOptions.RemoveEmptyEntries));
			}
			return hsList.ToArray();
		}

		public async Task<bool> RemoveHiddenServiceAsync(
			string serviceId,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			var reply = await SendCommandAsync(Command.DEL_ONION, serviceId, cancellationToken);
			return reply.IsOk;
		}

		public async Task<string> ResolveAsync(string address, bool isReverse = false, CancellationToken cancellationToken = default(CancellationToken))
		{
			var tcs = new TaskCompletionSource<string>();
			async void Test(object sender, AsyncReply e)
			{
				var addmapEvent = e as NewAddressMappingEvent;
				if (addmapEvent.Address == address)
				{
					tcs.TrySetResult(addmapEvent.NewAddress);
					if (!_cancellation.IsCancellationRequested)
					{
						await RemoveEventHandlerAsync(AsyncEvent.ADDRMAP, Test, cancellationToken);
					}
				}
			}
			await AddEventHandlerAsync(AsyncEvent.ADDRMAP, Test, cancellationToken);

			var args = $"{address}";
			if (isReverse)
			{
				args = $"mode=reverse {args}";
			}
			var reply = await SendCommandAsync(Command.RESOLVE, args, cancellationToken);

			return await tcs.Task;
		}

		public async Task DropGuards(CancellationToken cancellationToken = default(CancellationToken))
		{
			await SendCommandAsync(Command.DROPGUARDS, cancellationToken: cancellationToken);
		}

		public async Task CloseAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			await SendCommandAsync(Command.QUIT, cancellationToken: cancellationToken);
			this._controlSocket.Close();
		}

		public void Dispose()
		{
			this._cancellation.Cancel();
			this._controlSocket.Close();
		}

		private async Task<Response> SendCommandAsync(Command command, string args = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			var nextReply = CleanReplyChannelAsync(cancellationToken);
			var request = $"{command}";
			if (!string.IsNullOrEmpty(args))
			{
				request = $"{request} {args}";
			}
			await _controlSocket.SendAsync($"{request}\r\n", cancellationToken);
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
			var replyTask =  _replyChannel.TakeAsync();
			while (replyTask.Status == TaskStatus.RanToCompletion)
			{
				replyTask =  _replyChannel.TakeAsync();
			}
			return replyTask;
		}

		private IEnumerable<string> GetSettledAsyncEventNames()
		{
			return _asyncEventHandler.Where(x => x.Value.GetInvocationList().Any()).Select(x => x.Key.ToString());
		}

		private async Task SetSubscribedEventsAsync(CancellationToken cancellationToken)
		{
			await SendCommandAsync(Command.SETEVENTS, string.Join(" ", GetSettledAsyncEventNames()), cancellationToken);
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
						var asyncEvent = AsyncReply.Parse(new SingleLineReply(reply));
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
					var asyncEvent = await _asyncEventNotificationChannel.TakeAsync(_cancellation.Token);

					if (_asyncEventHandler.TryGetValue(asyncEvent.Event, out var handler))
					{
						try
						{
							handler.Invoke(this, asyncEvent);
						}
						catch(Exception)
						{

						}
					}
				}
			},  _cancellation.Token);
		}
	}
}
