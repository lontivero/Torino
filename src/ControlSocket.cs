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
		private static Regex MessagePrefix = new Regex("^[a-zA-Z0-9]{3}[-+ ]", RegexOptions.Compiled);

		private Stream _stream;
		private StreamReader _reader;

		public ControlSocket(IPEndPoint endPoint)
		{
			var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(endPoint);
			_stream = new NetworkStream(socket);
			_reader = new StreamReader(_stream);
		}

		public ControlSocket(Stream stream)
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
			await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
		}

		public async Task<Response> ReceiveAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			var parsed = new List<ResponseEntry>();;
			var line = "";
			var isFirstLine = true;

			while (true)
			{
				line = await _reader.ReadLineAsync().ConfigureAwait(false);

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
		protected string[] Parts { get; }
		internal IDictionary<string, string> Pairs { get; }

		public ResponseEntry(string statusCode, string divider, string content)
		{
			StatusCode = Enum.Parse<ReplyCode>(statusCode);
			Divider = divider;
			Content = content;
			Parts = SplitLine(content).ToArray();
			Pairs = divider == "+"
				? new Dictionary<string, string>()
				: Parts.Where(x => x.Contains('=')).Select(x => x.Split('=')).ToDictionary(x => x[0], x => x[1]);
		}

		private IEnumerable<string> SplitLine(string line)
		{
			var begin = 0;
			var end = begin + 1;
			var inQuote = false;

			while (end < line.Length)
			{
				if (line[end] == '"')
				{
					inQuote = !inQuote;
					end++;
				}
				else if (line[end] == ' ' && !inQuote)
				{
					yield return line.Substring(begin, end - begin);
					begin = end + 1;
					end = begin;
				}
				else
				{
					end++;
				}
			}
			if (begin < end )
			{
				yield return line.Substring(begin);
			}
		}

		internal string GetString(string key)
		{
			if (key[0] == '@')
			{
				var index = key[1] - '0';
				if (Parts.Length > index)
				{
					return Parts[index];
				}
			}
			else if (Pairs.TryGetValue(key, out var value))
			{
				return value;
			}
			return string.Empty;
		}

		internal string[] GetArray(string key)
		{
			return GetString(key).Split(',');
		}

		internal int GetInt(string key)
		{
			return int.Parse(GetString(key));
		}

		internal float GetFloat(string key)
		{
			return float.Parse(GetString(key));
		}

		internal TEnum GetEnum<TEnum>(string key) where TEnum : struct
		{
			return Enum.Parse<TEnum>(GetString(key));
		}

		internal DateTime GetISOTime(string key)
		{
			var value = GetString(key);
			return DateTime.Parse(value[1..^1]);
		}
	}
}
