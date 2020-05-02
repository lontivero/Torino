using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Torino
{
	public enum ReplyCode
	{
		OK = 250,
		UNNECESSARY_OPERATION = 251,
		EXHAUSTED = 451,
		SYNTAX_ERROR = 500,
		UNRECOGNIZED_COMMAND = 510,
		UNIMPLEMENTED_COMMAND = 511,
		SYNTAX_ERROR_ARGUMENT = 512,
		UNRECOGNIZED_ARGUMENT = 513,
		AUTHENTICATION_REQUIRED = 514,
		BAD_AUTHENTICATION = 515,
		UNSPECIFIED_TOR_ERROR = 550,
		INTERNAL_ERROR = 551,
		UNRECOGNIZED_ENTITY = 552,
		INVALID_CONFIGURATION_VALUE = 553,
		INVALID_DESCRIPTOR = 554,
		UNMANAGED_ENTITY = 555,
		ASYNC_EVENT_NOTIFICATION = 650
	}

	public class SingleLineReply
	{
		public ReplyCode Code { get; }
		public string Line { get; }
		protected string[] Parts { get; }
		protected IDictionary<string, string> Pairs { get; }
		public bool IsOK => Code == ReplyCode.OK;


		internal SingleLineReply(Response response)
			: this(response.Entries[0].StatusCode, response.Entries[0].Content)
		{}

		internal SingleLineReply(ReplyCode code, string line)
		{
			Code = code;
			Line = line;
			Parts = SplitLine(line).ToArray();
			Pairs = Parts.Where(x => x.Contains('=')).Select(x => x.Split('=')).ToDictionary(x => x[0], x => x[1]);
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

		internal static SingleLineReply FromResponse((string statusCode, string divider, string content)[] response)
		{
			throw new NotImplementedException();
		}
	}

	public class MultiLineReply
	{
		private Dictionary<string, string> _values = new Dictionary<string, string>();

		public string this[string key]
		{
			get
			{
				if (!_values.TryGetValue(key, out var value))
				{
					return string.Empty;
				}
				return value;
			}
		}

		internal MultiLineReply(Response response)
		{
			foreach(var entry in response.Entries)
			{
				if (entry.StatusCode == ReplyCode.OK && entry.Divider == " " && entry.Content == "OK")
					break;

				var parts = entry.Content.Split('=', StringSplitOptions.RemoveEmptyEntries);
				_values.Add(parts[0], parts[1]);
			}
		}
	}

	public class HiddenServiceReply : MultiLineReply
	{
		public string ServiceId { get; }
		public string PrivateKey { get; }
		public OnionKeyType KeyType { get; }
		public string UserName { get; }
		public string Password { get; }


		public HiddenServiceReply(Response response)
			: base(response)
		{
			ServiceId = this["ServiceID"];
			var pk = this["PrivateKey"];
			if (!string.IsNullOrEmpty(pk))
			{
				var parts = pk.Split(':');
				PrivateKey = parts[1];
				KeyType = Enum.Parse<OnionKeyType>(parts[0].Replace("-",""));
			}
			else
			{
				PrivateKey = string.Empty;
			}

			var auth = this["ClientAuth"];
			if (!string.IsNullOrEmpty(auth))
			{
				var parts = pk.Split(':');
				UserName = parts[0];
				Password = parts[1];
			}
			else
			{
				UserName = string.Empty;
				Password = string.Empty;
			}

		}

/*		

		  self.private_key_type, self.private_key = value.split(':', 1)
		elif key == 'ClientAuth':
		  if ':' not in value:
			raise stem.ProtocolError("ADD_ONION ClientAuth lines should be of the form 'ClientAuth=[username]:[credential]: %s" % self)

		  username, credential = value.split(':', 1)
		  self.client_auth[username] = credential

*/
	}
}
