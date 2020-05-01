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
		private Channel<Reply> _replyChannel = new Channel<Reply>();
		private Channel<AsyncReply> _asyncEventNotificationChannel = new Channel<AsyncReply>();
		private Dictionary<AsyncEvent, EventHandler<AsyncReply>> _asyncEventHandler = new Dictionary<AsyncEvent, EventHandler<AsyncReply>>(); 

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

			await SendCommandAsync(Command.AUTHENTICATE, "\"{password}\"", cancellationToken);
			IsAuthenticated = true;
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

		public Task RemoveEventHandlerAsync(
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
				return SetSubscribedEventsAsync(cancellationToken);
			}
			return Task.CompletedTask;
		}

		public async Task SignalAsync(Signal signal, CancellationToken cancellationToken = default(CancellationToken))
		{
			var reply = await SendCommandAsync(Command.SIGNAL, signal.ToString(), cancellationToken);

			if (reply.IsOK && signal == Signal.NEWNYM)
			{
				_lastNewnym = DateTime.UtcNow;
			}
		}

		public async Task CloseAsync(CancellationToken cancellationToken)
		{
			await SendCommandAsync(Command.QUIT, cancellationToken: cancellationToken);
			this._controlSocket.Close();
		}

		public void Dispose()
		{
			this._cancellation.Cancel();
			this._controlSocket.Close();
		}

		private async Task<Reply> SendCommandAsync(Command command, string args = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			var nextReply = CleanReplyChannelAsync(cancellationToken);
			var request = $"{command}";
			if (args is { })
			{
				request = $"{request} {args}";
			}
			await _controlSocket.SendAsync($"{request}\r\n", cancellationToken);
			var reply = await nextReply;
			if (reply.Code != ReplyCode.OK && reply.Code != ReplyCode.UNNECESSARY_OPERATION)
			{
				throw new Exception(reply.Line);
			}

			return reply;
		}

		private Task<Reply> CleanReplyChannelAsync(CancellationToken cancellationToken)
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

		private Task SetSubscribedEventsAsync(CancellationToken cancellationToken)
		{
			return SendCommandAsync(Command.SETEVENTS, string.Join(" ", GetSettledAsyncEventNames()), cancellationToken);
		}

		private void StartListening()
		{
			Task.Run(async () =>
			{
				while (true)
				{
					var response = await _controlSocket.ReceiveAsync(_cancellation.Token).ConfigureAwait(false);
					var reply = Reply.FromResponse(response);

					if (reply.Code == ReplyCode.ASYNC_EVENT_NOTIFICATION)
					{
						var asyncEvent = AsyncReply.Parse(reply);
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
