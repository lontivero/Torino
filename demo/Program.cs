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
			var launcher = new TorLauncher();
			launcher.OnProgress += (s,e) =>
			{
				Console.SetCursorPosition(0, Console.CursorTop);
				var progress = Enumerable.Range(0, e).Where(x => x % 10 == 0).Select(x => $"{x}%");
				Console.Write($"Bootstrapping: {string.Join(" ", progress)}");
			};
			using var torProcess = await launcher.LaunchAsync();
			var controlFilePath = launcher.Arguments["--ControlPortWriteToFile"];
			using(var control = await TorController.UseControlPortFileAsync(controlFilePath))
			{
				await control.AuthenticateAsync("");
				Console.WriteLine("General info");
				Console.WriteLine("------------------------------------------------------------------------------------------------------");
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
				Console.WriteLine();
			}

			using(var control = await TorController.UseControlPortFileAsync(controlFilePath))
			{
				await control.AuthenticateAsync("");
				Console.WriteLine("Circuits");
				Console.WriteLine("------------------------------------------------------------------------------------------------------");
				var circuits = await control.GetCircuitsAsync();
				foreach(var circ in circuits)
				{
					var pathLength = Math.Min(100, circ.Path.Length);
					Console.WriteLine($"{circ.Id} {circ.Status} {circ.TimeCreated} [{string.Join(", ", circ.BuildFlags)}] {circ.Path.Substring(0, pathLength)}...");
				}
				/*
				//Console.WriteLine(" Dropping Guard...");
				//await control.DropGuardsAsync(); 
				Console.WriteLine(" Renewing circuits...");
				await control.SignalAsync(Signal.NEWNYM);
				await Task.Delay(30_000);
				circuits = await control.GetCircuitsAsync();
				foreach(var circ in circuits)
				{
					var pathLength = Math.Min(110, circ.Path.Length);
					Console.WriteLine($"{circ.Id} {circ.Status} {circ.TimeCreated} [{string.Join(", ", circ.BuildFlags)}] {circ.Path.Substring(0, pathLength)}...");
				}
				*/
				Console.WriteLine();
			}
#if FALSE			

			using(var control = await TorController.UseControlPortFileAsync(controlFilePath))
			{
				await control.AuthenticateAsync("pwd");
				Console.WriteLine("Bandwidth event (Read/Write)");
				Console.WriteLine("------------------------------------------------------------------------------------------------------");

				var handler = new EventHandler<AsyncReply>((sender, e) => {
					var ev = (BandwidthEvent)e;
					Console.WriteLine($"[EVENT] Bandwidth -> Read: {ev.BytesRead} bytes\t\t Written: {ev.BytesWritten} bytes");
				});
				await control.AddEventHandlerAsync(AsyncEvent.BW, handler);

				await control.SignalAsync(Signal.DORMANT);
				await control.SignalAsync(Signal.ACTIVE);

				await Task.Delay(4_000);
				await control.RemoveEventHandlerAsync(AsyncEvent.BW, handler); 

				Console.WriteLine();
			}


			using(var control = await TorController.UseControlPortFileAsync(controlFilePath))
			{
				await control.AuthenticateAsync("pwd");
				Console.WriteLine("Ephemeral Hidden Services");
				Console.WriteLine("------------------------------------------------------------------------------------------------------");
				Console.Write("Creating....");
				void DisplayUpload(object sender, AsyncReply e)
				{
					var ev = (HiddenServiceDescriptorEvent)e;
					if (ev.Action == HsDescActions.UPLOADED || ev.Action == HsDescActions.UPLOAD || ev.Action == HsDescActions.CREATED)
					{
						Console.WriteLine($"{ev.Action} {ev.Address} -> {ev.HsDir}");
					}
				}
				await control.AddEventHandlerAsync(AsyncEvent.HS_DESC, DisplayUpload);

				var hs1 = control.CreateEphemeralHiddenServiceAsync("8081", flags: OnionFlags.DiscardPK, waitForPublication: true);
				var hs2 = control.CreateEphemeralHiddenServiceAsync("8082", waitForPublication: true);
				var hs = await Task.WhenAll(hs1, hs2);
				await control.RemoveEventHandlerAsync(AsyncEvent.HS_DESC, DisplayUpload);
				Console.WriteLine("Done");
				Console.WriteLine($"CREATED : {hs[0].ServiceId}");
				Console.WriteLine($"CREATED : {hs[1].ServiceId}");
				Console.WriteLine();

				Console.WriteLine("Listing...");
				var hsList = await control.ListEphemeralHiddenServicesAsync();
				for(var i = 0; i < hsList.Length; i++)
				{
					Console.WriteLine($"{i}: {hsList[i]}");
				}
				Console.WriteLine();

				Console.WriteLine("Removing...");
				for(var i = 0; i < hsList.Length; i++)
				{
					var hsItem = hsList[i];
					await control.RemoveHiddenServiceAsync(hsItem);
					Console.WriteLine($"{hsItem} REMOVED");
				}

				Console.WriteLine();
			}
#endif

			using(var control = await TorController.UseControlPortFileAsync(controlFilePath))
			{
				await control.AuthenticateAsync("");
				var handler = new EventHandler<AsyncReply>((sender, e) =>
					Console.WriteLine($"[EVENT] {e.Event} -> {e.Line.Substring(0, Math.Min(110, e.Line.Length))} {e.RawString}"));

				var handler2 = new EventHandler<AsyncReply>((sender, e) =>{
					Console.WriteLine(e.Line);
				});

				Console.WriteLine("Events");
				Console.WriteLine("------------------------------------------------------------------------------------------------------");

				Console.WriteLine("Circuit events....");
				await control.AddEventHandlerAsync(AsyncEvent.CIRC, handler);
				await Task.Delay(4_000);
				await control.RemoveEventHandlerAsync(AsyncEvent.CIRC, handler); 

				Console.WriteLine("Stream events....");
				await control.AddEventHandlerAsync(AsyncEvent.STREAM, handler2);
				await Task.Delay(16_000);
				await control.RemoveEventHandlerAsync(AsyncEvent.STREAM, handler2); 

				Console.WriteLine("Debug events....");
				await control.AddEventHandlerAsync(AsyncEvent.DEBUG, handler);
				await Task.Delay(500);
				await control.RemoveEventHandlerAsync(AsyncEvent.DEBUG, handler); 

				// await control.SignalAsync(Signal.SHUTDOWN);
			}

			Console.WriteLine("Goodbye. Enjoy it!");
		}
	}
}