// Pipe-protocol self-check over a real named pipe (random name, so it never collides with a running plugin).
// Run: dotnet run --project FF14ModManagerBridge.Tests   (exit code = number of failed checks)
using System.Diagnostics;
using System.Text.Json.Nodes;
using FF14ModManager.Core;

var failed = 0;
void Check(bool ok, string what) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}"); if (!ok) failed++; }

var pipe = "FF14ModManager.Test." + Guid.NewGuid().ToString("N")[..8];
using var cts = new CancellationTokenSource();
var server = Bridge.Serve(req => (string?)req["cmd"] == "boom"
    ? throw new InvalidOperationException("kaboom")
    : Task.FromResult(new JsonObject { ["ok"] = true, ["echo"] = (string?)req["cmd"] }), cts.Token, pipeName: pipe);

var r1 = await Bridge.Ask(new JsonObject { ["cmd"] = "status" }, pipeName: pipe);
var r2 = await Bridge.Ask(new JsonObject { ["cmd"] = "boom" }, pipeName: pipe);
var r3 = await Bridge.Ask(new JsonObject { ["cmd"] = "again" }, pipeName: pipe);
Check((string?)r1?["echo"] == "status", "round trip: one JSON line each way");
Check(r2?["ok"]?.GetValue<bool>() == false && ((string?)r2?["error"])?.Contains("kaboom") == true, "a handler exception becomes {ok:false, error}");
Check((string?)r3?["echo"] == "again", "the server keeps serving after an error");

cts.Cancel();
await server.WaitAsync(TimeSpan.FromSeconds(5));
var sw = Stopwatch.StartNew();
var none = await Bridge.Ask(new JsonObject { ["cmd"] = "status" }, timeoutMs: 500, pipeName: pipe);
Check(none == null && sw.ElapsedMilliseconds < 3000, $"no server = null, quickly ({sw.ElapsedMilliseconds} ms)");
Check(Bridge.Error("x")["ok"]?.GetValue<bool>() == false, "Error() shape");

Console.WriteLine(failed == 0 ? "ALL PASS" : $"{failed} FAILED");
return failed;
