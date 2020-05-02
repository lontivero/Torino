using System;
using System.Net;
using System.Threading.Tasks;

namespace Torino.Demo
{
	public class Program
	{
		static async Task Main(string[] args)
		{
			using(var control = new TorController(IPAddress.Loopback, 9051))
			{
				await control.AuthenticateAsync("pwd");
				var version = await control.GetVersionAsync();
				var user = await control.GetUserAsync();
		
				Console.WriteLine($"Tor version: {version}");
				Console.WriteLine($"Tor user   : {user}");

				var ip = await control.ResolveAsync("google.com");
				var domain = await control.ResolveAsync(ip, isReverse: true);
				Console.WriteLine($"google.com : {ip}");
				Console.WriteLine($"{ip} : {domain}");
				Console.WriteLine("------------------------------------------------------------------------------------------------------");
			}


			using(var control = new TorController(IPAddress.Loopback, 9051))
			{
				await control.AuthenticateAsync("pwd");

				await control.AddEventHandlerAsync(AsyncEvent.BW, 
					(sender, e) => Console.WriteLine($"[EVENT] {e.Event} -> {e.Line}"));

				await control.SignalAsync(Signal.DORMANT);
				Console.WriteLine("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<< Time to sleep");
				await Task.Delay(5_000);
				Console.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> Waking up");
				await control.SignalAsync(Signal.ACTIVE);
				await Task.Delay(3_000);
				Console.WriteLine("------------------------------------------------------------------------------------------------------");
			}


			using(var control = new TorController(IPAddress.Loopback, 9051))
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

			using(var control = new TorController(IPAddress.Loopback, 9051))
			{
				await control.AuthenticateAsync("pwd");
				// await control.SignalAsync(Signal.SHUTDOWN);
			}

			Console.WriteLine("Goodbye. Enjoy it!");
		}
	}
}