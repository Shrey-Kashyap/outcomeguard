using System.Text.Json;
using OutcomeGuard.Api;

namespace OutcomeGuard.Tests;

public sealed class ManualClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class ServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "outcomeguard-tests", Guid.NewGuid().ToString("N"));
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));
    private readonly OutcomeService _service;
    private readonly StateRepository _repository;
    private readonly BusinessApp _app;
    private readonly RuleInput _ruleInput;

    public ServiceTests()
    {
        _repository = new StateRepository(Path.Combine(_directory, "test.db"));
        _service = new OutcomeService(_repository, _clock, true);
        _app = _service.CreateApp(new CreateApp("Test app", "Test", "Synthetic test"));
        _ruleInput = new RuleInput(_app.Id, "Complete each journey", "", "started", ["completed"], 5, "critical", "amount", "INR", "Ops");
    }

    private EventInput Event(string id, string type = "started", int minutesAgo = 10, string correlation = "journey") =>
        new(id, _app.Id, correlation, type, _clock.Now.AddMinutes(-minutesAgo), JsonSerializer.SerializeToElement(new { amount = 100 }));

    [Fact]
    public void ReplayIsIdempotentAndConflictingBatchIsAtomic()
    {
        var input = Event("a");
        Assert.Equal(new IngestResult(1, 0), _service.Ingest(new([input])));
        Assert.Equal(new IngestResult(0, 1), _service.Ingest(new([input])));
        var ex = Assert.Throws<DomainException>(() => _service.Ingest(new([Event("new"), input with { Type = "changed" }])));
        Assert.Equal(409, ex.StatusCode);
        Assert.Single(_service.GetState().Events);
    }

    [Fact]
    public void SameBatchDuplicatesAndObjectPropertyOrderReplaySafely()
    {
        var one = Event("a") with { Data = JsonDocument.Parse("{\"a\":1,\"b\":2}").RootElement.Clone() };
        var two = one with { Data = JsonDocument.Parse("{\"b\":2,\"a\":1}").RootElement.Clone() };
        Assert.Equal(new IngestResult(1, 1), _service.Ingest(new([one, two])));
    }

    [Fact]
    public void EventIdsAreScopedByApplication()
    {
        var other = _service.CreateApp(new("Other", "Test", ""));
        var input = Event("same-id");
        Assert.Equal(2, _service.Ingest(new([input, input with { AppId = other.Id, Type = "different" }])).Accepted);
    }

    [Fact]
    public void InvalidFutureOrUnknownAppEventRejectsWholeBatch()
    {
        Assert.Throws<DomainException>(() => _service.Ingest(new([Event("good"), Event("future") with { OccurredAt = _clock.Now.AddMinutes(1) }])));
        Assert.Throws<DomainException>(() => _service.Ingest(new([Event("good"), Event("unknown") with { AppId = "unknown" }])));
        Assert.Empty(_service.GetState().Events);
    }

    [Fact]
    public void PreviewIsReadOnlyAndRuleCanDetectExistingEvents()
    {
        _service.Ingest(new([Event("start")]));
        var preview = _service.Preview(_ruleInput);
        Assert.Equal("violated", Assert.Single(preview.Evaluations).Status);
        Assert.Empty(_service.GetState().Rules);
        Assert.Empty(_service.GetState().Incidents);
        _service.CreateRule(_ruleInput);
        Assert.Single(_service.GetState().Incidents);
    }

    [Fact]
    public void VerificationRequiresEvidenceAndPreservesMissedDeadlineHistory()
    {
        _service.CreateRule(_ruleInput);
        _service.Ingest(new([Event("start")]));
        var incident = Assert.Single(_service.GetState().Incidents);
        Assert.False(_service.Verify(incident.Id).Verified);
        _service.Acknowledge(incident.Id, new("Investigating with producer logs."));
        _service.Ingest(new([Event("done", "completed", 0)]));
        Assert.Equal("acknowledged", Assert.Single(_service.GetState().Incidents).Status);
        var verification = _service.Verify(incident.Id);
        Assert.True(verification.Verified);
        Assert.Equal("resolved", verification.Incident.Status);
        Assert.Equal(["done"], verification.Incident.Verification!.EvidenceEventIds);
        Assert.Equal("recovered", Assert.Single(_service.GetState().Evaluations).Status);
        Assert.Equal(0, _service.GetState().Summary.SuccessRate);
        Assert.Empty(_service.GetState().Summary.Impact);
    }

    [Fact]
    public void LateArrivingOnTimeEvidencePreservesIncidentUntilVerification()
    {
        _service.CreateRule(_ruleInput);
        _service.Ingest(new([Event("start")]));
        var incident = Assert.Single(_service.GetState().Incidents);
        _service.Ingest(new([Event("done", "completed", 8)]));
        var state = _service.GetState();
        Assert.Equal("satisfied", Assert.Single(state.Evaluations).Status);
        Assert.Equal("open", Assert.Single(state.Incidents).Status);
        Assert.Equal(incident.OpenedAt, state.Incidents[0].OpenedAt);
        Assert.True(_service.Verify(incident.Id).Verified);
    }

    [Fact]
    public void OutOfOrderOutcomeBeforeTriggerIngestionStillSatisfies()
    {
        _service.CreateRule(_ruleInput);
        _service.Ingest(new([Event("done", "completed", 8)]));
        _service.Ingest(new([Event("start")]));
        Assert.Equal("satisfied", Assert.Single(_service.GetState().Evaluations).Status);
        Assert.Empty(_service.GetState().Incidents);
    }

    [Fact]
    public void PausedRulesTrackExistingObligationsAndResumeBackfillsRetainedTriggers()
    {
        var rule = _service.CreateRule(_ruleInput);
        _service.Ingest(new([Event("one", minutesAgo: 0)]));
        _service.SetEnabled(rule.Id, new(false));
        _service.Ingest(new([Event("two", minutesAgo: 0, correlation: "two")]));
        Assert.Single(_service.GetState().Evaluations);
        _clock.Now = _clock.Now.AddMinutes(6);
        _service.Evaluate();
        Assert.Equal("violated", Assert.Single(_service.GetState().Evaluations).Status);
        _service.SetEnabled(rule.Id, new(true));
        Assert.Equal(2, _service.GetState().Evaluations.Length);
    }

    [Fact]
    public void DatabaseReloadRetainsEventsIncidentsAndAudit()
    {
        _service.CreateRule(_ruleInput);
        _service.Ingest(new([Event("start")]));
        _service.Acknowledge(_service.GetState().Incidents[0].Id, new("Owner is investigating."));
        var reloaded = new OutcomeService(new StateRepository(Path.Combine(_directory, "test.db")), _clock, true).GetState();
        Assert.Single(reloaded.Events);
        Assert.Equal("acknowledged", Assert.Single(reloaded.Incidents).Status);
        Assert.Contains(reloaded.Incidents[0].Timeline, entry => entry.Detail == "Owner is investigating.");
    }

    [Theory]
    [InlineData("commerce")]
    [InlineData("events")]
    [InlineData("hr")]
    public void EveryDemoUsesGenericEngineAndSafeTwoStepRecovery(string scenario)
    {
        _service.RunDemo(scenario);
        var state = _service.GetState();
        Assert.Equal(3, state.Evaluations.Length);
        Assert.Equal(2, state.Summary.Satisfied);
        var incident = Assert.Single(state.Incidents);
        Assert.False(_service.Verify(incident.Id).Verified);
        _service.RecoverDemo(incident.Id);
        Assert.Equal("open", Assert.Single(_service.GetState().Incidents).Status);
        Assert.True(_service.Verify(incident.Id).Verified);
        Assert.Equal(1, _service.GetState().Summary.Recovered);
    }

    [Fact]
    public void DemoRecoveryCannotFabricateEvidenceForNonDemoIncidents()
    {
        _service.CreateRule(_ruleInput);
        _service.Ingest(new([Event("start") with { Data = JsonSerializer.SerializeToElement(new { synthetic = true }) }]));
        var incident = Assert.Single(_service.GetState().Incidents);
        Assert.Equal(403, Assert.Throws<DomainException>(() => _service.RecoverDemo(incident.Id)).StatusCode);
        Assert.False(_service.Verify(incident.Id).Verified);
    }

    [Fact]
    public void RuleValidationRejectsSelfSatisfactionAndInvalidDeadlines()
    {
        Assert.Throws<DomainException>(() => _service.CreateRule(_ruleInput with { ExpectedTypes = ["started"] }));
        Assert.Throws<DomainException>(() => _service.CreateRule(_ruleInput with { ExpectedTypes = ["completed", "completed"] }));
        Assert.Throws<DomainException>(() => _service.CreateRule(_ruleInput with { WindowMinutes = 0 }));
        Assert.Throws<DomainException>(() => _service.CreateRule(_ruleInput with { Severity = "unknown" }));
        Assert.Empty(_service.GetState().Rules);
    }

    [Fact]
    public void ImpactUnitsAreNotCombinedAndPendingIsExcludedFromSuccessRate()
    {
        _service.RunDemo("commerce");
        _service.RunDemo("hr");
        var summary = _service.GetState().Summary;
        Assert.Contains(summary.Impact, x => x.Unit == "INR" && x.Amount == 4999);
        Assert.Contains(summary.Impact, x => x.Unit == "people" && x.Amount == 1);
        Assert.Equal(66.7m, summary.SuccessRate);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
