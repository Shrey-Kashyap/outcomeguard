using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OutcomeGuard.Api;

var builder = WebApplication.CreateBuilder(args);
var apiKey = builder.Configuration["OutcomeGuard:ApiKey"] ?? "";
var development = builder.Environment.IsDevelopment();
if (!development && apiKey.Length < 24)
    throw new InvalidOperationException("Set OutcomeGuard__ApiKey to a random secret of at least 24 characters before starting outside Development.");
if (apiKey.Length is > 0 and < 24)
    throw new InvalidOperationException("OutcomeGuard__ApiKey must contain at least 24 characters.");
var demoEnabled = builder.Configuration.GetValue<bool?>("OutcomeGuard:DemoEnabled") ?? development;
var databasePath = builder.Configuration["OutcomeGuard:DatabasePath"] ?? Path.Combine(builder.Environment.ContentRootPath, "data", "outcomeguard.db");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_048_576);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new StateRepository(databasePath));
builder.Services.AddSingleton(services => new OutcomeService(services.GetRequiredService<StateRepository>(), services.GetRequiredService<TimeProvider>(), demoEnabled));
builder.Services.AddHostedService<EvaluationWorker>();
var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; font-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    try
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Request.Path != "/api/config")
            {
                if (apiKey.Length != 0)
                {
                    var provided = context.Request.Headers["X-Api-Key"].ToString();
                    if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(apiKey), Encoding.UTF8.GetBytes(provided)))
                        throw new DomainException(401, "A valid workspace API key is required.");
                }
                else if (!IsLoopbackHost(context.Request.Host.Host)
                    || (context.Connection.RemoteIpAddress is { } remote && !IPAddress.IsLoopback(remote)))
                    throw new DomainException(403, "Unauthenticated demo access is restricted to localhost.");

                var origin = context.Request.Headers.Origin.ToString();
                if (origin.Length > 0)
                {
                    var sameOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
                    var devOrigin = development && origin is "http://localhost:5173" or "http://127.0.0.1:5173";
                    if (!string.Equals(origin, sameOrigin, StringComparison.OrdinalIgnoreCase) && !devOrigin)
                        throw new DomainException(403, "Requests from this browser origin are not allowed.");
                }
                if (context.Request.Method is "POST" or "PUT" or "PATCH"
                    && !context.Request.HasJsonContentType())
                    throw new DomainException(415, "Use Content-Type: application/json.");
            }
        }
        await next(context);
    }
    catch (DomainException ex)
    {
        context.Response.StatusCode = ex.StatusCode;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (BadHttpRequestException)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { error = "The request body is invalid or exceeds the request size limit." });
    }
    catch (JsonException)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { error = "The request must contain valid JSON matching the endpoint schema." });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "OutcomeGuard request failed");
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { error = "The request could not be completed. Check server logs." });
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "OutcomeGuard" }));
app.MapGet("/api/config", () => new { authRequired = apiKey.Length > 0, demoEnabled });
app.MapGet("/api/state", (OutcomeService service) => service.GetState());
app.MapPost("/api/apps", (CreateApp input, OutcomeService service) => Results.Created("/api/state", service.CreateApp(input)));
app.MapPost("/api/rules", (RuleInput input, OutcomeService service) => Results.Created("/api/state", service.CreateRule(input)));
app.MapPost("/api/rules/preview", (RuleInput input, OutcomeService service) => service.Preview(input));
app.MapPatch("/api/rules/{id}/enabled", (string id, EnabledInput input, OutcomeService service) => service.SetEnabled(id, input));
app.MapPost("/api/events", (EventBatch input, OutcomeService service) => service.Ingest(input));
app.MapPost("/api/evaluate", (OutcomeService service) => service.Evaluate());
app.MapPost("/api/incidents/{id}/acknowledge", (string id, AcknowledgeInput input, OutcomeService service) => service.Acknowledge(id, input));
app.MapPost("/api/incidents/{id}/verify", (string id, OutcomeService service) => service.Verify(id));
app.MapPost("/api/demo/{scenario}", (string scenario, OutcomeService service) => service.RunDemo(scenario));
app.MapPost("/api/demo/incidents/{id}/recover", (string id, OutcomeService service) => service.RecoverDemo(id));
app.Map("/api/{**path}", () => Results.Json(new { error = "API endpoint not found." }, statusCode: 404));
app.MapFallbackToFile("index.html");
app.Run();

static bool IsLoopbackHost(string host) => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
    || (IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));

public partial class Program;

public sealed class EvaluationWorker(OutcomeService service, ILogger<EvaluationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try { service.Evaluate(); }
            catch (Exception ex) { logger.LogError(ex, "Periodic invariant evaluation failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
