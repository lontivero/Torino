using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Torino
{
	public abstract class AsyncReply : Reply
	{
		public AsyncEvent Event { get; }
		public string RawString => Line;

		protected AsyncReply(ReplyCode code, string line)
			: base(code, line)
		{
			var eventName = GetString("@0");
			Event = Enum.Parse<AsyncEvent>(eventName);
		}

		internal static AsyncReply Parse(Reply reply)
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

		internal NewAddressMappingEvent(Reply reply)
			: base(reply.Code, reply.Line)
		{}
	}

	public class AuthDirNewDescsEvent : AsyncReply
	{
		public AuthDirNewDescsEvent(Reply reply) 
			: base(reply.Code, reply.Line)
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

		internal BuildTimeoutSetEvent(Reply reply)
			: base(reply.Code, reply.Line)
		{}
	}
	
	public class BandwidthEvent : AsyncReply
	{
		public int BytesRead => GetInt("@1");
		public int BytesWritten => GetInt("@1");

		internal BandwidthEvent(Reply reply)
			: base(reply.Code, reply.Line)
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

		internal CellStatsEvent(Reply reply)
			: base(reply.Code, reply.Line)
		{}
	}

	public class CircuitBandwidthEvent : AsyncReply
	{
		public string Id => GetString("ID");
		public int BytesRead => GetInt("READ");
		public int BytesWritten => GetInt("WRITTEN");

		internal CircuitBandwidthEvent(Reply reply)
			: base(reply.Code, reply.Line)
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

		internal CircuitEvent(Reply reply)
			: base(reply.Code, reply.Line)
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

		internal CircuitMinorEvent(Reply reply)
			:base(reply.Code, reply.Line)
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

		internal ClientsSeenEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{}
	}

	public class ConfChangedEvent : AsyncReply
	{
		internal ConfChangedEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{}
	}

	public class ConnectionBandwidthEvent : AsyncReply
	{
		public string Id => GetString("ID");
		public ConnBwTypes Type => GetEnum<ConnBwTypes>("TYPE");
		public int BytesRead => GetInt("READ");
		public int BytesWritten => GetInt("WRITTEN");

		internal ConnectionBandwidthEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{}
	}

	public class DescChangedEvent : AsyncReply
	{
		internal DescChangedEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{}
	}

	public class GuardEvent : AsyncReply
	{
		public string Type => GetString("@1");
		public string Name => GetString("@2");
		public string Status => GetString("@3");

		internal GuardEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{}
	}

	public class HiddenServiceDescriptorContentEvent : AsyncReply
	{
		internal HiddenServiceDescriptorContentEvent(Reply reply)
			:base(reply.Code, reply.Line)
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

		internal HiddenServiceDescriptorEvent(Reply reply)
			:base(reply.Code, reply.Line)
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

		internal LogEvent(Reply reply)
			: base(reply.Code, reply.Line)
		{}
	}

	public class DebugLogEvent : LogEvent
	{
		internal DebugLogEvent(Reply reply)
			: base(reply)
		{}
	}

	public class ErrorLogEvent : LogEvent
	{
		internal ErrorLogEvent(Reply reply)
			: base(reply)
		{}
	}

	public class InfoLogEvent : LogEvent
	{
		internal InfoLogEvent(Reply reply)
			: base(reply)
		{}
	}

	public class NoticeLogEvent : LogEvent
	{
		internal NoticeLogEvent(Reply reply)
			: base(reply)
		{}
	}

	public class WarningLogEvent : LogEvent
	{
		internal WarningLogEvent(Reply reply)
			: base(reply)
		{}
	}

	public class NetworkLivenessEvent : AsyncReply
	{
		public string Status => Line;

		internal NetworkLivenessEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{}
	}

	public class NewConsensusEvent : AsyncReply
	{
		internal NewConsensusEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{}
	}

	public class NewDescriptorsAvailableEvent : AsyncReply
	{
		public string[] ServerIDs => Line.Split(' ');

		internal NewDescriptorsAvailableEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{}
	}

	public class NsEvent : AsyncReply
	{
		internal NsEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{}
	}

	public class OrConnEvent : AsyncReply
	{		
		public string Target => GetString("@1");
		public OrConnStatus Status => GetEnum<OrConnStatus>("@2");
		public OrConnReasons Reason => GetEnum<OrConnReasons>("REASON");
		public int CircuitsCount => GetInt("NCIRCS");
		public string Id => GetString("ID");

		internal OrConnEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{}
	}

	public class SignalEvent : AsyncReply
	{
		public string Signal => Line;

		internal SignalEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{}
	}
	
	public abstract class StatusEvent : AsyncReply
	{
		public StatusSeverity Severity => GetEnum<StatusSeverity>("@1");
		public string Action => GetString("@2");
		public ReadOnlyDictionary<string, string> Arguments { get; }

		internal StatusEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{
			Arguments = new ReadOnlyDictionary<string, string>(Pairs);
		}
	}

	public class StatusClientEvent : StatusEvent
	{
		internal StatusClientEvent(Reply reply)
			:base(reply)
		{}
	}

	public class StatusGeneralEvent : StatusEvent
	{
		internal StatusGeneralEvent(Reply reply)
			:base(reply)
		{}
	}

	public class StatusServerEvent : StatusEvent
	{
		internal StatusServerEvent(Reply reply)
			:base(reply)
		{}
	}

	public class StreamBandwidthEvent : AsyncReply
	{
		public string Id => GetString("@1");
		public int BytesRead => GetInt("@2");
		public int BytesWritten => GetInt("@3");

		internal StreamBandwidthEvent(Reply reply)
			:base(reply.Code, reply.Line)
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

		internal StreamEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{}
	}

	public class TokenBucketsEmptyEvent : AsyncReply
	{
		public string Name => GetString("@1");
		public string ConnectionId => GetString("ID");
		public int ReadBucket => GetInt("READ");
		public int WriteBucket => GetInt("WRITTEN");
		public int LastRefill => GetInt("LAST");

		internal TokenBucketsEmptyEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{}
	}
	
	public class TransportLaunchedEvent : AsyncReply
	{
		public string Type => GetString("@1");
		public string Name => GetString("@2");
		public string Address => GetString("@3");
		public string Port => GetString("@4");

		internal TransportLaunchedEvent(Reply reply)
			:base(reply.Code, reply.Line)
		{}
	}	
}