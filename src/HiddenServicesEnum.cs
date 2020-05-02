
using System;

namespace Torino
{
	[Flags]
	public enum OnionFlags
	{
		None = 0,
		DiscardPK = 1,
		Detach = 2,
	}

	public enum OnionKeyBlob
	{
		BEST,
		RSA1024,
		STRING
	}

	public enum OnionKeyType
	{
		NEW,
		RSA1024,
		ED25519V3
	}

	public enum AuthMethod
	{
		NULL,
		HASHEDPASSWORD,
		COOKIE,
		SAFECOOKIE,
	}
}