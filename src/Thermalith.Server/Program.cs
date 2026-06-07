// Thermalith.Server — headless API / MCP host over Thermalith.Core.
// Scaffold only: the MCP tool surface (§8 of the build spec) is deliberately deferred
// until there is a working core system to expose.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Thermalith.Server (scaffold). API / MCP surface to follow once the core system prints.");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
