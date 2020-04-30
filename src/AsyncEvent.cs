using System;

namespace Torino
{
	public enum AsyncEvent
	{
		ADDRMAP,
		AUTHDIR_NEWDESCS,
		BUILDTIMEOUT_SET,
		BW,
		CIRC,
		CIRC_MINOR,
		CLIENTS_SEEN,
		CONF_CHANGED,
		DEBUG,
		DESCCHANGED,
		ERR,
		GUARD,
		INFO,
		NEWCONSENSUS,
		NEWDESC,
		NOTICE,
		NS,
		ORCONN,
		SIGNAL,
		STATUS_CLIENT,
		STATUS_GENERAL,
		STATUS_SERVER,
		STREAM,
		STREAM_BW,
		WARN,
		STATUS_SEVER,
		TRANSPORT_LAUNCHED,
		CONN_BW,
		CIRC_BW,
		CELL_STATS,
		TB_EMPTY,
		HS_DESC,
		HS_DESC_CONTENT,
		NETWORK_LIVENESS
	}

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
	}
}