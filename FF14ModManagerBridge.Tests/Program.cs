// Pipe-protocol self-check over a real named pipe (random name, so it never collides with a running plugin).
// Run: dotnet run --project FF14ModManagerBridge.Tests   (exit code = number of failed checks)
using System.Diagnostics;
using System.IO.Pipes;
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

// Raw client: send exactly `line` (null = send nothing), return the reply line (null = none).
async Task<string?> Raw(string? line)
{
    await using var c = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    await c.ConnectAsync(3000);
    using var r = new StreamReader(c, leaveOpen: true);
    await using var w = new StreamWriter(c, leaveOpen: true) { AutoFlush = true };
    if (line == null) return null;
    await w.WriteLineAsync(line);
    using var t = new CancellationTokenSource(3000);
    try { return await r.ReadLineAsync(t.Token); } catch (OperationCanceledException) { return null; }
}
bool IsError(string? reply, string text) =>
    reply != null && JsonNode.Parse(reply) is JsonObject o && o["ok"]?.GetValue<bool>() == false && ((string?)o["error"])?.Contains(text) == true;

Check(IsError(await Raw("{not json"), ""), "malformed JSON becomes {ok:false, error}");
Check(IsError(await Raw("[1,2]"), ""), "a JSON array becomes {ok:false, error}");
var nullReply = await Raw("null");
Check(IsError(nullReply, "not an object"), $"a JSON null is rejected as 'not an object' ({nullReply})");
await Raw(null);   // connect and hang up without a request
Check((string?)(await Bridge.Ask(new JsonObject { ["cmd"] = "after-hangup" }, pipeName: pipe))?["echo"] == "after-hangup", "the server keeps serving after a client hangs up silently");

// A client that connects and never writes must not hold the single pipe instance forever.
var stuck = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
await stuck.ConnectAsync(3000);
var afterStuck = await Bridge.Ask(new JsonObject { ["cmd"] = "after-stuck" }, timeoutMs: 3000, pipeName: pipe);
Check((string?)afterStuck?["echo"] == "after-stuck", "a silent client is dropped so the next one is served");
await stuck.DisposeAsync();
Check((string?)(await Bridge.Ask(new JsonObject { ["cmd"] = "still" }, pipeName: pipe))?["echo"] == "still", "the server survives the read timeout");

cts.Cancel();
await server.WaitAsync(TimeSpan.FromSeconds(5));
var sw = Stopwatch.StartNew();
var none = await Bridge.Ask(new JsonObject { ["cmd"] = "status" }, timeoutMs: 500, pipeName: pipe);
Check(none == null && sw.ElapsedMilliseconds < 3000, $"no server = null, quickly ({sw.ElapsedMilliseconds} ms)");
Check(Bridge.Error("x")["ok"]?.GetValue<bool>() == false, "Error() shape");

Console.WriteLine(failed == 0 ? "ALL PASS" : $"{failed} FAILED");
return failed;
