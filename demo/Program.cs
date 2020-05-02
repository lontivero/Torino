using System;
using System.Net;
using System.Threading.Tasks;

namespace Torino.Demo
{
	public class Program
	{
		static async Task Main(string[] args)
		{
			using(var control = new TorController())
			{
				await control.AuthenticateAsync("pwd");
				var protocolInfo = await control.GetProtocolInfoAsync();
				var version = await control.GetVersionAsync();
				var user = await control.GetUserAsync();
				var pid = await control.GetPidAsync();
		
				Console.WriteLine($"Tor version   : {version}");
				Console.WriteLine($"Tor user      : {user}");
				Console.WriteLine($"Tor Process Id: {pid}");
				Console.WriteLine();
				Console.WriteLine($"Protocol");
				Console.WriteLine($"  - Protocol Version: {protocolInfo.ProtocolVersion}");
				Console.WriteLine($"  - Tor Version     : {protocolInfo.TorVersion}");
				Console.WriteLine($"  - Auth methods    : {string.Join(", ", protocolInfo.AuthMethods)}");
				Console.WriteLine($"  - Cookie file path: {protocolInfo.CookieFile}");
				Console.WriteLine();
				Console.WriteLine("Resolve domains");

				var ip = await control.ResolveAsync("google.com");
				var domain = await control.ResolveAsync(ip, isReverse: true);
				Console.WriteLine($"  - google.com   : {ip}");
				Console.WriteLine($"  - {ip} : {domain}");
				Console.WriteLine("------------------------------------------------------------------------------------------------------");
			}


			using(var control = new TorController())
			{
				await control.AuthenticateAsync("pwd");
				var handler = new EventHandler<AsyncReply>((sender, e) => Console.WriteLine($"[EVENT] Bandwidth -> {e.Line}"));
				await control.AddEventHandlerAsync(AsyncEvent.BW, handler);

				await control.SignalAsync(Signal.DORMANT);
				await control.SignalAsync(Signal.ACTIVE);

				await Task.Delay(4_000);
				await control.RemoveEventHandlerAsync(AsyncEvent.BW, handler); 

				Console.WriteLine("------------------------------------------------------------------------------------------------------");
			}


			using(var control = new TorController())
			{
				await control.AuthenticateAsync("pwd");

				var hs1 = await control.CreateEphemeralHiddenServiceAsync("8081", flags: OnionFlags.DiscardPK, waitForPublication: true);
				var hs2 = await control.CreateEphemeralHiddenServiceAsync("8082", waitForPublication: true);
				Console.WriteLine($"Service ID : {hs1.ServiceId}");
				Console.WriteLine($"Service ID : {hs2.ServiceId}");

				var hsList = await control.ListEphemeralHiddenServicesAsync();
				foreach(var hs in hsList)
				{
					Console.WriteLine($"[] {hs}");
					await control.RemoveHiddenServiceAsync(hs);
				}
				Console.WriteLine("------------------------------------------------------------------------------------------------------");
			}

			using(var control = new TorController())
			{
				await control.AuthenticateAsync("pwd");
				// await control.SignalAsync(Signal.SHUTDOWN);
			}

			Console.WriteLine("Goodbye. Enjoy it!");
		}
	}
}