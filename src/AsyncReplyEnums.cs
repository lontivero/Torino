﻿
namespace Torino
{
	public enum BuildTimeoutSetTypes
	{
		COMPUTED,
		RESET,
		SUSPENDED,
		DISCARD,
		RESUME
	}

	public enum CircBuildFlags
	{
		ONEHOP_TUNNEL,
		IS_INTERNAL,
		NEED_CAPACITY,
		NEED_UPTIME
	}

	public enum CircHsState
	{
		HSCI_CONNECTING,
		HSCI_INTRO_SENT,
		HSCI_DONE,
		HSCR_CONNECTING,
		HSCR_ESTABLISHED_IDLE,
		HSCR_ESTABLISHED_WAITING,
		HSCR_JOINED,
		HSSI_CONNECTING,
		HSSI_ESTABLISHED,
		HSSR_CONNECTING,
		HSSR_JOINED
	}

	public enum CircPurpose
	{
		GENERAL,
		HS_CLIENT_INTRO,
		HS_CLIENT_REND,
		HS_SERVICE_INTRO,
		HS_SERVICE_REND,
		TESTING,
		CONTROLLER,
		MEASURE_TIMEOUT
	}

	public enum CircReasons
	{
		NONE,
		TORPROTOCOL,
		INTERNAL,
		REQUESTED,
		HIBERNATING,
		RESOURCELIMIT,
		CONNECTFAILED,
		OR_IDENTITY,
		OR_CONN_CLOSED,
		TIMEOUT,
		FINISHED,
		DESTROYED,
		NOPATH,
		NOSUCHSERVICE,
		MEASUREMENT_EXPIRED
	}

	public enum CircStatus
	{
		LAUNCHED,
		BUILT,
		EXTENDED,
		FAILED,
		CLOSED,
		PURPOSE_CHANGED,
		CANNIBALIZED
	}

	public enum ConnBwTypes
	{
		OR,
		DIR,
		EXIT
	}

	public enum HsDescActions
	{
		REQUESTED,
		UPLOAD,
		RECEIVED,
		UPLOADED,
		IGNORE,
		FAILED,
		CREATED
	}

	public enum HsDescAuthTypes
	{
		NO_AUTH,
		BASIC_AUTH,
		STEALTH_AUTH,
		UNKNOWN
	}

	public enum OrConnReasons
	{
		DONE,
		CONNECTREFUSED,
		IDENTITY,
		CONNECTRESET,
		TIMEOUT,
		NOROUTE,
		IOERROR,
		RESOURCELIMIT,
		PT_MISSING,
		MISC
	}

	public enum OrConnStatus
	{
		NEW,
		LAUNCHED,
		CONNECTED,
		FAILED,
		CLOSED
	}

	public enum StatusSeverity
	{
		NOTICE,
		WARN,
		ERR
	}

	public enum StreamPurpose
	{
		DIR_FETCH,
		DIR_UPLOAD,
		DIRPORT_TEST,
		DNS_REQUEST,
		USER
	}

	public enum StreamReason
	{
		MISC,
		RESOLVEFAILED,
		CONNECTREFUSED,
		EXITPOLICY,
		DESTROY,
		DONE,
		TIMEOUT,
		NOROUTE,
		HIBERNATING,
		INTERNAL,
		RESOURCELIMIT,
		CONNRESET,
		TORPROTOCOL,
		NOTDIRECTORY,
		END,
		PRIVATE_ADDR
	}

	public enum StreamStatus
	{
		NEW,
		NEWRESOLVE,
		REMAP,
		SENTCONNECT,
		SENTRESOLVE,
		SUCCEEDED,
		FAILED,
		CLOSED,
		DETACHED
	}

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
}