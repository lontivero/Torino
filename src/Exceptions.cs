using System;
using System.Runtime.Serialization;

namespace Torino
{
	public class ProtocolException : Exception
	{
		public ProtocolException(string message) 
			: base(message)
		{
		}
	}
}
