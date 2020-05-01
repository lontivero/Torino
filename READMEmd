Torino is a dotnet controller library for **[Tor](https://www.torproject.org/)**. With it you can use Tor's [control protocol](https://gitweb.torproject.org/torspec.git/tree/control-spec.txt) to script against the Tor process.


```c#
using(var control = new TorController())
{
    await control.AuthenticateAsync("pwd");
    var version = await control.GetVersionAsync();
    var user = await control.GetUserAsync();

    Console.WriteLine($"Tor version: {version}");
    Console.WriteLine($"Tor user   : {user}");
}
```


```c#
using(var control = new TorController())
{
    await control.AuthenticateAsync("pwd");

    await control.AddEventHandlerAsync(AsyncEvent.BW, 
        (sender, e) => Console.WriteLine($"[EVENT] {e.Event} -> {e.Line}"));

    await control.SignalAsync(Signal.DORMANT);
    Console.WriteLine("Time to sleep");
    await Task.Delay(5_000);
    Console.WriteLine("Waking up");
    await control.SignalAsync(Signal.ACTIVE);
    await Task.Delay(3_000);
}
```


```c#

using(var control = new TorController())
{
    await control.AuthenticateAsync("pwd");
    await control.SignalAsync(Signal.SHUTDOWN);
}

```