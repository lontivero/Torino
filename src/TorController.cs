﻿using System;
using System.Threading.Tasks;
using System.Net;
using System.Threading;

namespace Torino
{
	public class TorController : IDisposable
	{
		private ControlSocket _controlSocket;
		private Channel<Reply> _replyChannel = new Channel<Reply>();
		private Channel<AsyncReply> _asyncEventNotificationChannel = new Channel<AsyncReply>();

		private CancellationTokenSource _cancellation = new CancellationTokenSource();

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

		private async Task SendCommandAsync(Command command, string args = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			var request = $"{command}";
			if (args is { })
			{
				request = $"{request} {args}";
			}
			await _controlSocket.SendAsync($"{request}\r\n", cancellationToken);
		}

		private void StartListening()
		{
			Task.Run(async () =>
			{
				while (true)
				{
					var response = await _controlSocket.ReceiveAsync(_cancellation.Token).ConfigureAwait(false);
					var reply = Reply.Parse(response);

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
		}
	}
}
