using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Linq;

namespace Torino
{
	public class TorLauncher
	{
		private static Regex bootstrapLineRegEx = new ("Bootstrapped ([0-9]+)%", RegexOptions.Compiled);
		private static Regex problemLineRegEx = new ("\\[(warn|err)\\] (.*)$", RegexOptions.Compiled);

		public ImmutableDictionary<string, string> Arguments { get; private set; } = ImmutableDictionary.Create<string, string>();
		public EventHandler<int>? OnProgress;

		public async Task<Process> LaunchAsync(string? torExecutableFilePath = null, string? torConfigFilePath = null, IDictionary<string, string>? args = null, bool takeOwnership = true)
		{
			var arguments = args is { }
				? args.ToImmutableDictionary(x => x.Key, x => x.Value)
				: ImmutableDictionary.Create<string, string>();
			
			ImmutableDictionary<string, string> AddSwitch(string key, string value) =>
				arguments.ContainsKey(key) 
					? arguments 
					: arguments.Add(key, value);

			if (!string.IsNullOrEmpty(torConfigFilePath))
			{
				arguments = AddSwitch("-f", torConfigFilePath == "<no torrc>" ? Path.GetTempFileName() : torConfigFilePath);
			}

			string datadir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			arguments = AddSwitch("--DataDirectory", datadir);

			string GetDataDirRandomFileName(string prefix) =>
				Path.Combine(datadir, prefix + "-" + Path.GetRandomFileName());


			if (takeOwnership)
			{
				arguments = AddSwitch("__OwningControllerProcess", Process.GetCurrentProcess().Id.ToString());
			}

			arguments = AddSwitch("--ControlPortWriteToFile", GetDataDirRandomFileName("control-port"));
			arguments = AddSwitch("--ControlPort", "auto");
			arguments = AddSwitch("--SocksPort", "auto");
			arguments = AddSwitch("--CookieAuthentication", "1");
			arguments = AddSwitch("--CookieAuthFile", GetDataDirRandomFileName("cookie-auth"));

			var process = new Process();
			try
			{
				torExecutableFilePath ??= true switch {
					_ when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => "tor.exe",
					_ when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => "tor.real",
					_ when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "tor",
					_ => throw new NotSupportedException($"Unsupported platform.")
				};
				process.StartInfo.FileName = torExecutableFilePath;
				process.StartInfo.Arguments = string.Join(" ", arguments.SelectMany(x => new [] { x.Key, x.Value }));
				process.EnableRaisingEvents = true;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = true;
				var tcs = new TaskCompletionSource<bool>();

				void onExit(object? sender, EventArgs e)
				{
					process.Exited -= onExit;
					process.OutputDataReceived -= onOutputWrite;
					process.ErrorDataReceived -= onOutputWrite;
					tcs.SetResult(false);
				};

				void onOutputWrite(object? sender, DataReceivedEventArgs e)
				{
					var line = e.Data ?? "";
					var bootstrapMatch = bootstrapLineRegEx.Match(line);
					var problemMatch = problemLineRegEx.Match(line);

					if (bootstrapMatch.Success)
					{
						var progress = int.Parse(bootstrapMatch.Groups[1].Value);
						OnProgress?.Invoke(this, progress);

						if (progress == 100)
						{
							tcs.SetResult(true);
						}
						return;
					}
					if (problemMatch.Success)
					{
						var level = problemMatch.Groups[1].Value;
						var msg = problemMatch.Groups[2].Value;
						if (msg.Contains(": "))
						{
							msg = msg.Split(": ")[^1];
						}
						return;
					}
				}

				process.OutputDataReceived += onOutputWrite;
				process.ErrorDataReceived += onOutputWrite;
				process.Exited += onExit;

				process.Start();
				process.BeginOutputReadLine();
				await Task.WhenAny(tcs.Task);

				Arguments = arguments;
				return process;
			}
			catch (Exception)
			{
				process.Kill();
				throw;
			}
		}
	}
}