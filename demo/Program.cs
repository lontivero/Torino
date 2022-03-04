using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Torino.Demo
{
	public class Program
	{
		static async Task Main(string[] args)
		{
			using var control = new TorController(IPAddress.Loopback, 37151);
			await control.AuthenticateAsync("").ConfigureAwait(false);
			EventHandler<AsyncReply> handler = (sender, e) =>
				Console.WriteLine($"{e.Event} -> {e.RawString}");

			await control.AddEventHandlerAsync(AsyncEvent.CIRC, handler);
			await control.AddEventHandlerAsync(AsyncEvent.STREAM, handler);
			await control.AddEventHandlerAsync(AsyncEvent.ERR, handler);
			await control.AddEventHandlerAsync(AsyncEvent.STREAM, handler);
			await control.AddEventHandlerAsync(AsyncEvent.GUARD, handler);
			await control.AddEventHandlerAsync(AsyncEvent.ORCONN, handler);
			for (int i = 0; i < 10000; i++)
			{
				await Task.Delay(100).ConfigureAwait(false);
			}
		}
	}
}