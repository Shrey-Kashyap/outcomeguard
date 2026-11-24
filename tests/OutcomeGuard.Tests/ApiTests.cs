using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OutcomeGuard.Api;

namespace OutcomeGuard.Tests;

public sealed class TestHost : WebApplicationFactory<Program>
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "outcomeguard-api-tests", Guid.NewGuid().ToString("N"));
    public string ApiKey { get; init; } = "";
    public bool DemoEnabled { get; init; } = true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("OutcomeGuard:DatabasePath", Path.Combine(_directory, "test.db"));
        builder.UseSetting("OutcomeGuard:ApiKey", ApiKey);
        builder.UseSetting("OutcomeGuard:DemoEnabled", DemoEnabled.ToString());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}

public sealed class ApiTests
{
    [Fact]
    public async Task DemoToVerifiedRecoveryWorksThroughRealHttpEndpoints()
    {
        using var host = new TestHost();
        using var client = host.CreateClient();
        var response = await client.PostAsJsonAsync("/api/demo/commerce", new { });
        response.EnsureSuccessStatusCode();
        var state = await client.GetFromJsonAsync<StateView>("/api/state");
        Assert.Equal(3, state!.Evaluations.Length);
        var incident = Assert.Single(state.Incidents);
        var blocked = await (await client.PostAsJsonAsync($"/api/incidents/{incident.Id}/verify", new { })).Content.ReadFromJsonAsync<VerifyResult>();
        Assert.False(blocked!.Verified);
        (await client.PostAsJsonAsync($"/api/demo/incidents/{incident.Id}/recover", new { })).EnsureSuccessStatusCode();
        var verified = await (await client.PostAsJsonAsync($"/api/incidents/{incident.Id}/verify", new { })).Content.ReadFromJsonAsync<VerifyResult>();
        Assert.True(verified!.Verified);
        Assert.Equal("resolved", verified.Incident.Status);
    }

    [Fact]
    public async Task ApiKeyProtectsReadAndWriteButConfigRemainsPublic()
    {
        using var host = new TestHost { ApiKey = "synthetic-test-key-with-32-characters" };
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/state")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/demo/hr", new { })).StatusCode);
        (await client.GetAsync("/api/config")).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/state")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Add("X-Api-Key", host.ApiKey);
        (await client.GetAsync("/api/state")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ForeignBrowserOriginAndReboundHostCannotAccessLocalDemo()
    {
        using var host = new TestHost();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "https://attacker.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/demo/hr", new { })).StatusCode);
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Host = "attacker.example";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/state")).StatusCode);
    }

    [Fact]
    public async Task AllowedLocalFrontendOriginWorksAndFormPostsAreRejected()
    {
        using var host = new TestHost();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "http://127.0.0.1:5173");
        (await client.PostAsJsonAsync("/api/demo/events", new { })).EnsureSuccessStatusCode();
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["name"] = "Forged" });
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await client.PostAsync("/api/apps", form)).StatusCode);
    }

    [Fact]
    public async Task InvalidInputAndUnknownEndpointProduceSafeErrors()
    {
        using var host = new TestHost();
        using var client = host.CreateClient();
        var badApp = await client.PostAsJsonAsync("/api/apps", new { name = "", domain = "test", description = "" });
        Assert.Equal(HttpStatusCode.BadRequest, badApp.StatusCode);
        Assert.Contains("error", await badApp.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/events", new { events = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/not-an-endpoint")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/incidents/unknown/verify", new { })).StatusCode);
    }

    [Fact]
    public async Task DemoEndpointsCanBeDisabled()
    {
        using var host = new TestHost { DemoEnabled = false };
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/demo/commerce", new { })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<StateView>("/api/state"))!.Events);
    }
}
