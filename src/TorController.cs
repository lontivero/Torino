using System;
using System.Threading.Tasks;
using System.Net;
using System.Threading;

namespace Torino
{
	public class TorController : IDisposable
	{
		private ControlSocket _controlSocket;
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
		}


		public async Task AuthenticateAsync(string password, CancellationToken cancellationToken = default(CancellationToken))
		{
			password ??= string.Empty;

			var request = $"AUTHENTICATE \"{password}\"";
			await SendCommandAsync(request, cancellationToken);
			IsAuthenticated = true;
		}

		public async Task CloseAsync(CancellationToken cancellationToken)
		{
			await SendCommandAsync("QUIT", cancellationToken);
			this._controlSocket.Close();
		}
	
		public void Dispose()
		{
			this._cancellation.Cancel();
			this._controlSocket.Close();
		}

		private async Task SendCommandAsync(string command, CancellationToken cancellationToken = default(CancellationToken))
		{
			await _controlSocket.SendAsync($"{command}\r\n", cancellationToken);
		}
	}
}
