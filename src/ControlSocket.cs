using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
		private Stream _stream;
		private StreamReader _reader;

		public ControlSocket(IPEndPoint endPoint)
		{
			var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(endPoint);
			SetStream(new NetworkStream(socket));
		}

		public ControlSocket(Stream stream)
		{
			SetStream(stream);
		}

		private void SetStream(Stream stream)
		{
			_stream = stream;
			_reader = new StreamReader(_stream);
		}

		public void Close()
		{
			_reader.Close();
		}

		public async ValueTask SendAsync(string message, CancellationToken cancellationToken)
		{
			var buffer = new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(message));
			await _stream.WriteAsync(buffer, cancellationToken);
		}

		private static Regex MessagePrefix = new Regex("^[a-zA-Z0-9]{3}[-+ ]", RegexOptions.Compiled);

		public async Task<Response> ReceiveAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			List<ResponseEntry> parsed = null;
			var line = "";
			var isFirstLine = true;

			while (true)
			{
				line = await _reader.ReadLineAsync();

				if (line is null)
				{
					throw new ProtocolException("Received empty socket content.");
				}
				else if (!MessagePrefix.IsMatch(line))
				{
					throw new ProtocolException("Badly formatted reply line: beginning is malformed.");
				}
				var statusCode = line[..3];
				var divider = line[3..4];
				var content = line[4..];
				var current = new ResponseEntry(statusCode, divider, content);

				if (isFirstLine)
				{
					if (divider == " ") // this is a late reply
					{
						return new Response(current);
					}

					isFirstLine = false;
					parsed = new List<ResponseEntry>();
				}
				
				if (divider == "-") // mid-reply line, keep pulling for more content
				{
					parsed.Add(current);
				}
				else if (divider == " ")
				{
					parsed.Add(current);
					return new Response(parsed);
				}
				else if (divider == "+")
				{
					var multiLineContent = new StringBuilder();
					multiLineContent.AppendLine(content);
					while (true)
					{
						line = await _reader.ReadLineAsync();

						if (line is null)
						{
							throw new ProtocolException("Received empty socket content.");
						}
						if (line == ".")
						{
							break;
						}
						multiLineContent.AppendLine(line);
					}
					return new Response( new ResponseEntry( statusCode, divider, multiLineContent.ToString()) );
				}
			}
		}
	}

	public class Response
	{
		public ResponseEntry[] Entries { get; }
		public bool IsOk => Entries[0].StatusCode == ReplyCode.OK;

		public Response(ResponseEntry entry)
		{
			Entries = new[] { entry };
		}

		public Response(IEnumerable<ResponseEntry> entries)
		{
			Entries = entries.ToArray();
		}
	}

	public class ResponseEntry
	{
		public ReplyCode StatusCode { get; }
		public string Divider { get; }
		public string Content { get; }

		public ResponseEntry(string statusCode, string divider, string content)
		{
			StatusCode = Enum.Parse<ReplyCode>(statusCode);
			Divider = divider;
			Content = content;
		} 
	}
}
