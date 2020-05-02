using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Torino
{
	public abstract class AsyncReply : SingleLineReply
	{
		public AsyncEvent Event { get; }
		public string RawString => Line;

		protected AsyncReply(ResponseEntry entry)
			: base(entry)
		{
			var eventName = Entry.GetString("@0");
			Event = Enum.Parse<AsyncEvent>(eventName);
		}

		internal static AsyncReply Parse(ResponseEntry reply)
		{
			var eventName = reply.GetString("@0");
			var eventCode = Enum.Parse<AsyncEvent>(eventName);

			return eventCode switch
			{
				AsyncEvent.ADDRMAP => new NewAddressMappingEvent(reply),
				AsyncEvent.AUTHDIR_NEWDESCS => new AuthDirNewDescsEvent(reply),
				AsyncEvent.BUILDTIMEOUT_SET => new BuildTimeoutSetEvent(reply),
				AsyncEvent.BW => new BandwidthEvent(reply),
				AsyncEvent.CIRC => new CircuitEvent(reply),
				AsyncEvent.CIRC_MINOR => new CircuitMinorEvent(reply),
				AsyncEvent.CLIENTS_SEEN => new ClientsSeenEvent(reply),
				AsyncEvent.CONF_CHANGED => new ConfChangedEvent(reply),
				AsyncEvent.DEBUG => new DebugLogEvent(reply),
				AsyncEvent.DESCCHANGED => new DescChangedEvent(reply),
				AsyncEvent.ERR => new ErrorLogEvent(reply),
				AsyncEvent.GUARD => new GuardEvent(reply),
				AsyncEvent.INFO => new InfoLogEvent(reply),
				AsyncEvent.NEWCONSENSUS => new NewConsensusEvent(reply),
				AsyncEvent.NEWDESC => new NewDescriptorsAvailableEvent(reply),
				AsyncEvent.NOTICE => new NoticeLogEvent(reply),
				AsyncEvent.NS => new NsEvent(reply),
				AsyncEvent.ORCONN => new OrConnEvent(reply),
				AsyncEvent.SIGNAL => new SignalEvent(reply),
				AsyncEvent.STATUS_CLIENT => new StatusClientEvent(reply),
				AsyncEvent.STATUS_GENERAL => new StatusGeneralEvent(reply),
				AsyncEvent.STATUS_SERVER => new StatusServerEvent(reply),
				AsyncEvent.STREAM => new StreamEvent(reply),
				AsyncEvent.STREAM_BW => new StreamBandwidthEvent(reply),
				AsyncEvent.WARN => new WarningLogEvent(reply),
				AsyncEvent.STATUS_SEVER => new StatusServerEvent(reply),
				AsyncEvent.TRANSPORT_LAUNCHED => new TransportLaunchedEvent(reply),
				AsyncEvent.CONN_BW => new ConnectionBandwidthEvent(reply),
				AsyncEvent.CIRC_BW => new CircuitBandwidthEvent(reply),
				AsyncEvent.CELL_STATS => new CellStatsEvent(reply),
				AsyncEvent.TB_EMPTY => new TokenBucketsEmptyEvent(reply),
				AsyncEvent.HS_DESC => new HiddenServiceDescriptorEvent(reply),
				AsyncEvent.HS_DESC_CONTENT => new HiddenServiceDescriptorContentEvent(reply),
				AsyncEvent.NETWORK_LIVENESS => new NetworkLivenessEvent(reply),
				_ => throw new ProtocolException($"Unknown Event: \"{eventCode}\".")
			};
		}
	}

	public class NewAddressMappingEvent : AsyncReply
	{
		public string Address => GetString("@1");
		public string NewAddress => GetString("@2");
		public DateTime Expiry => GetISOTime("@3");
		public string Error => GetString("ERROR");
		public DateTime Expires => GetISOTime("EXPIRES");
		public bool Cached => GetString("CACHED").Contains("YES", StringComparison.InvariantCultureIgnoreCase);

		internal NewAddressMappingEvent(ResponseEntry reply)
			: base(reply)
		{}
	}

	public class AuthDirNewDescsEvent : AsyncReply
	{
		public AuthDirNewDescsEvent(ResponseEntry reply) 
			: base(reply)
		{}
	}

	public class BuildTimeoutSetEvent : AsyncReply
	{
		public BuildTimeoutSetTypes Type => GetEnum<BuildTimeoutSetTypes>("@1");
		public int TimeoutsCount => GetInt("TOTAL_TIMES");
		public int Timeout => GetInt("TIMEOUT_MS");
		public int Xm => GetInt("XM");
		public string Alpha => GetString("ALPHA");
		public string Quantile => GetString("CUTOFF_QUANTILE");
		public float TimeoutRate => GetFloat("TIMEOUT_RATE");
		public int CloseTimeout => GetInt("CLOSE_MS");
		public float CloseRate => GetFloat("CLOSE_RATE");

		internal BuildTimeoutSetEvent(ResponseEntry reply)
			: base(reply)
		{}
	}
	
	public class BandwidthEvent : AsyncReply
	{
		public int BytesRead => GetInt("@1");
		public int BytesWritten => GetInt("@1");

		internal BandwidthEvent(ResponseEntry reply)
			: base(reply)
		{}
	}

	public class CellStatsEvent : AsyncReply
	{
		public string Id => GetString("ID");
		public string InboundQueue => GetString("INBOUNDQUEUE");
		public string InboundConn => GetString("INBOUNDCONN");
		public string InboundAdded => GetString("INBOUNDADDED");
		public string InboundRemoved => GetString("INBOUNDREMOVED");
		public string InboundTime => GetString("INBOUNDTIME");
		public string OutboundQueue => GetString("OUTBOUNDQUEUE");
		public string OutboundConn => GetString("OUTBOUNDCONN");
		public string OutboundAdded => GetString("OUTBOUNDADDED");
		public string OutboundRemoved => GetString("OUTBOUNDREMOVED");
		public string OutboundTime => GetString("OUTBOUNDTIME");

		internal CellStatsEvent(ResponseEntry reply)
			: base(reply)
		{}
	}

	public class CircuitBandwidthEvent : AsyncReply
	{
		public string Id => GetString("ID");
		public int BytesRead => GetInt("READ");
		public int BytesWritten => GetInt("WRITTEN");

		internal CircuitBandwidthEvent(ResponseEntry reply)
			: base(reply)
		{}
	}
	
	public class CircuitEvent : AsyncReply
	{
		public string Id => GetString("@1");
		public CircStatus Status => GetEnum<CircStatus>("@2");
		public string Path { get; }
		public CircBuildFlags[] BuildFlags => GetArray("BUILD_FLAGS").Select(Enum.Parse<CircBuildFlags>).ToArray();
		public CircPurpose Purpose => GetEnum<CircPurpose>("PURPOSE");
		public CircHsState HsState => GetEnum<CircHsState>("HS_STATE");
		public string RendQuery => GetString("REND_QUERY");
		public string TimeCreated => GetString("TIME_CREATED");
		public CircReasons Reason => GetEnum<CircReasons>("REASON");
		public CircReasons RemoteReason => GetEnum<CircReasons>("REMOTE_REASON");
		public string SocksUsername => GetString("SOCKS_USERNAME").Replace("\"", "");
		public string SocksPassword => GetString("SOCKS_PASSWORD").Replace("\"", "");

		internal CircuitEvent(string line)
			: this(new ResponseEntry("650", " ", "CIRC " + line))
		{}

		internal CircuitEvent(ResponseEntry reply)
			: base(reply)
		{
			var path = GetString("@3"); 
			Path = path.Contains("=") ? string.Empty : path;
		}
	}

	public class CircuitMinorEvent : AsyncReply
	{
		public string Id => GetString("@1");
		public CircStatus Status => GetEnum<CircStatus>("@2");
		public string Path { get; }
		public CircBuildFlags[] BuildFlags => GetArray("BUILD_FLAGS").Select(Enum.Parse<CircBuildFlags>).ToArray();
		public CircPurpose Purpose => GetEnum<CircPurpose>("PURPOSE");
		public CircHsState HsState => GetEnum<CircHsState>("HS_STATE");
		public string RendQuery => GetString("REND_QUERY");
		public string TimeCreated => GetString("TIME_CREATED");
		public CircPurpose OldPurpose => GetEnum<CircPurpose>("OLD_PURPOSE");
		public CircHsState OldHsState => GetEnum<CircHsState>("OLD_HS_STATE");

		internal CircuitMinorEvent(ResponseEntry reply)
			:base(reply)
		{
			var path = GetString("@3"); 
			Path = path.Contains("=") ? string.Empty : path;
		}
	}

	public class ClientsSeenEvent : AsyncReply
	{
		public DateTime TimeStarted => GetISOTime("@1");
		public string[] CountrySummary => GetArray("@2");
		public string[] IPVersions  => GetArray("@3");

		internal ClientsSeenEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class ConfChangedEvent : AsyncReply
	{
		internal ConfChangedEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class ConnectionBandwidthEvent : AsyncReply
	{
		public string Id => GetString("ID");
		public ConnBwTypes Type => GetEnum<ConnBwTypes>("TYPE");
		public int BytesRead => GetInt("READ");
		public int BytesWritten => GetInt("WRITTEN");

		internal ConnectionBandwidthEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class DescChangedEvent : AsyncReply
	{
		internal DescChangedEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class GuardEvent : AsyncReply
	{
		public string Type => GetString("@1");
		public string Name => GetString("@2");
		public string Status => GetString("@3");

		internal GuardEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class HiddenServiceDescriptorContentEvent : AsyncReply
	{
		internal HiddenServiceDescriptorContentEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class HiddenServiceDescriptorEvent : AsyncReply
	{	
		public HsDescActions Action => GetEnum<HsDescActions>("@1");
		public string Address => GetString("@2");
		public HsDescAuthTypes AuthType => GetEnum<HsDescAuthTypes>("@3");
		public string HsDir => GetString("@4");
		public string DescriptorID { get; }
		public string Reason => GetString("REASON");
		public string Replica => GetString("REPLICA");
		public string HsDirIndex => GetString("HSDIR_INDEX");

		internal HiddenServiceDescriptorEvent(ResponseEntry reply)
			:base(reply)
		{
			DescriptorID = GetString("@5");
			if (DescriptorID.Contains("="))
			{
				DescriptorID = string.Empty;
			}
		}
	}

	public abstract class LogEvent : AsyncReply
	{
		public string LogMessage => Line;

		internal LogEvent(ResponseEntry reply)
			: base(reply)
		{}
	}

	public class DebugLogEvent : LogEvent
	{
		internal DebugLogEvent(ResponseEntry reply)
			: base(reply)
		{}
	}

	public class ErrorLogEvent : LogEvent
	{
		internal ErrorLogEvent(ResponseEntry reply)
			: base(reply)
		{}
	}

	public class InfoLogEvent : LogEvent
	{
		internal InfoLogEvent(ResponseEntry reply)
			: base(reply)
		{}
	}

	public class NoticeLogEvent : LogEvent
	{
		internal NoticeLogEvent(ResponseEntry reply)
			: base(reply)
		{}
	}

	public class WarningLogEvent : LogEvent
	{
		internal WarningLogEvent(ResponseEntry reply)
			: base(reply)
		{}
	}

	public class NetworkLivenessEvent : AsyncReply
	{
		public string Status => Line;

		internal NetworkLivenessEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class NewConsensusEvent : AsyncReply
	{
		internal NewConsensusEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class NewDescriptorsAvailableEvent : AsyncReply
	{
		public string[] ServerIDs => Line.Split(' ');

		internal NewDescriptorsAvailableEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class NsEvent : AsyncReply
	{
		internal NsEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class OrConnEvent : AsyncReply
	{		
		public string Target => GetString("@1");
		public OrConnStatus Status => GetEnum<OrConnStatus>("@2");
		public OrConnReasons Reason => GetEnum<OrConnReasons>("REASON");
		public int CircuitsCount => GetInt("NCIRCS");
		public string Id => GetString("ID");

		internal OrConnEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class SignalEvent : AsyncReply
	{
		public string Signal => Line;

		internal SignalEvent(ResponseEntry reply)
			:base(reply)
		{}
	}
	
	public abstract class StatusEvent : AsyncReply
	{
		public StatusSeverity Severity => GetEnum<StatusSeverity>("@1");
		public string Action => GetString("@2");
		public ReadOnlyDictionary<string, string> Arguments { get; }

		internal StatusEvent(ResponseEntry reply)
			:base(reply)
		{
			Arguments = new ReadOnlyDictionary<string, string>(Entry.Pairs);
		}
	}

	public class StatusClientEvent : StatusEvent
	{
		internal StatusClientEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class StatusGeneralEvent : StatusEvent
	{
		internal StatusGeneralEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class StatusServerEvent : StatusEvent
	{
		internal StatusServerEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class StreamBandwidthEvent : AsyncReply
	{
		public string Id => GetString("@1");
		public int BytesRead => GetInt("@2");
		public int BytesWritten => GetInt("@3");

		internal StreamBandwidthEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class StreamEvent : AsyncReply
	{
		public string StreamID => GetString("@1");
		public StreamStatus Status => GetEnum<StreamStatus>("@2");
		public string CircuitID => GetString("@3");
		public string Target => GetString("@4");
		public StreamReason Reason => GetEnum<StreamReason>("REASON");
		public StreamReason RemoteReason => GetEnum<StreamReason>("REMOTE_REASON");
		public string Source => GetString("SOURCE");
		public string SourceAddr => GetString("SOURCE_ADDR");
		public StreamPurpose Purpose => GetEnum<StreamPurpose>("PURPOSE");

		internal StreamEvent(ResponseEntry reply)
			:base(reply)
		{}
	}

	public class TokenBucketsEmptyEvent : AsyncReply
	{
		public string Name => GetString("@1");
		public string ConnectionId => GetString("ID");
		public int ReadBucket => GetInt("READ");
		public int WriteBucket => GetInt("WRITTEN");
		public int LastRefill => GetInt("LAST");

		internal TokenBucketsEmptyEvent(ResponseEntry reply)
			:base(reply)
		{}
	}
	
	public class TransportLaunchedEvent : AsyncReply
	{
		public string Type => GetString("@1");
		public string Name => GetString("@2");
		public string Address => GetString("@3");
		public string Port => GetString("@4");

		internal TransportLaunchedEvent(ResponseEntry reply)
			:base(reply)
		{}
	}	
}