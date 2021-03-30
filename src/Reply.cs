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
		public ReplyCode Code => Entry.StatusCode; 
		public string Line => Entry.Content;
		public bool IsOK => Code == ReplyCode.OK;
		public ResponseEntry Entry { get; }

		internal SingleLineReply(Response response)
			: this(response.Entries[0])
		{}

		internal SingleLineReply(ResponseEntry entry)
		{
			Entry = entry;
		}

		internal string GetString(string key) => Entry.GetString(key);
		internal string[] GetArray(string key) => Entry.GetArray(key);
		internal int GetInt(string key) => Entry.GetInt(key);
		internal float GetFloat(string key) => Entry.GetFloat(key);
		internal DateTime GetISOTime(string key) => Entry.GetISOTime(key);
		internal TEnum GetEnum<TEnum>(string key) where TEnum : struct
			=> Entry.GetEnum<TEnum>(key);
	}

	public class MultiLineReply
	{
		private Dictionary<string, string> _values = new Dictionary<string, string>();
		public string[] Keys => _values.Keys.ToArray();

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

				if (entry.Divider == "+")
				{
					var lines = entry.Content.Split("\n", StringSplitOptions.RemoveEmptyEntries);
					for(var i = 0; i< lines.Length; i++)
					{
						_values.Add(i.ToString(), lines[i]);
					}
				}
				else if (entry.Divider == "-")
				{
					var parts = entry.Content.Split('=', StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length > 1)
					{
						_values.Add(parts[0], parts[1]);
					}
				}
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
	}

	public class ProtocolInfoReply : MultiLineReply
	{
		public string ProtocolVersion { get; }
		public string TorVersion { get; }
		public AuthMethod[] AuthMethods { get; }
		public string CookieFile { get; }

#pragma warning disable CS8618
		internal ProtocolInfoReply(Response response)
			: base(response)
		{
			foreach(var entry in response.Entries)
			{
				var name = entry.GetString("@0");
				switch(name)
				{
					case "PROTOCOLINFO":
						ProtocolVersion = entry.GetString("@1");
						break;
					case "VERSION":
						TorVersion = entry.GetString("Tor");
						break;
					case "AUTH":
						AuthMethods = entry.GetArray("METHODS").Select(Enum.Parse<AuthMethod>).ToArray();
						CookieFile = entry.GetString("COOKIEFILE");
						break;
				}
			}
		}
#pragma warning restore CS8618
	}
}
