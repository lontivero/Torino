using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Torino
{
	public class ControlSocket
	{
		private Socket _socket;
		private NetworkStream _stream;
		private StreamReader _reader;
		public IPEndPoint EndPoint { get; }

		public bool IsConnected => _socket.Connected || (_socket.Poll(1000, SelectMode.SelectRead) && _socket.Available == 0);

		public ControlSocket(IPEndPoint endPoint)
		{
			EndPoint = endPoint;
			_socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
		}

		public async Task ConnectAsync()
		{
			await _socket.ConnectAsync(EndPoint);
			_stream = new NetworkStream(_socket, true);
			_reader = new StreamReader(_stream, leaveOpen: true);
		}

		public void Close()
		{
			if (IsConnected)
			{
				_socket.Close();
				_socket = null;
			}
		}

		public async ValueTask SendAsync(string message, CancellationToken cancellationToken)
		{
			var buffer = new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(message));
			await _stream.WriteAsync(buffer, cancellationToken);
		}

		private static Regex MessagePrefix = new Regex("^[a-zA-Z0-9]{3}[-+ ]", RegexOptions.Compiled);

		public async Task<string> ReceiveAsync(CancellationToken cancellationToken)
		{
			if (_socket is null) return null;

			StringBuilder parsed = null;
			var line = "";
			var isFirstLine = true;

			while (true)
			{
				line = await _reader.ReadLineAsync();

				if (line is null)
				{
					throw new Exception("Received empty socket content.");
				}
				else if (!MessagePrefix.IsMatch(line))
				{
					throw new Exception("Badly formatted reply line: beginning is malformed.");
				}
				var statusCode = int.Parse(line[..3]);
				var divider = line[3..4];
				var content = line[4..];

				if (isFirstLine)
				{
					if (divider == " ") // this is a late reply
					{
						return content;
					}

					isFirstLine = false;
					parsed = new StringBuilder();
				}
				
				if (divider == "-") // mid-reply line, keep pulling for more content
				{
					parsed.AppendLine(content);
				}
				else if (divider == " ")
				{
					parsed.AppendLine(line);
					return parsed.ToString();
				}
				else if (divider == "+")
				{
					var multiLineContent = new StringBuilder();
					while (true)
					{
						line = await _reader.ReadLineAsync();

						if (line is null)
						{
							throw new Exception("Received empty socket content.");
						}
						if (line == ".")
						{
							break;
						}
						multiLineContent.AppendLine(line);
					}
					return multiLineContent.ToString();
				}
			}
		}
	}
}
