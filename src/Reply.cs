using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

	public class Reply
	{
		public ReplyCode Code { get; }
		public string Line { get; }
		protected string[] Parts { get; }
		protected IDictionary<string, string> Pairs { get; }

		public bool IsOK => Code == ReplyCode.OK;

		internal Reply(ReplyCode code, string line)
		{
			Code = code;
			Line = line;
			Parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			Pairs = Parts.Where(x => x.Contains('=')).Select(x => x.Split('=')).ToDictionary(x => x[0], x => x[1]);
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
