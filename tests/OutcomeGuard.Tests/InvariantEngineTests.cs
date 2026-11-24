using System.Text.Json;
using OutcomeGuard.Api;

namespace OutcomeGuard.Tests;

public sealed class InvariantEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);
    private static Rule Rule => new("rule", "app", "Complete order", "", "started", ["completed"], 5,
        "critical", "amount", "INR", "Ops", true, Start);
    private static BusinessEvent Event(string id, string type, int minutes = 0, string app = "app", string correlation = "journey") =>
        new(id, app, correlation, type, Start.AddMinutes(minutes), Start.AddMinutes(minutes), JsonSerializer.SerializeToElement(new { amount = 125m }));

    [Fact]
    public void DeadlineIsInclusiveAndViolationStartsAfterIt()
    {
        var trigger = Event("start", "started");
        Assert.Equal("pending", InvariantEngine.Evaluate(Rule, trigger, [trigger], Start.AddMinutes(5)).Status);
        Assert.Equal("violated", InvariantEngine.Evaluate(Rule, trigger, [trigger], Start.AddMinutes(5).AddTicks(1)).Status);
        Assert.Equal("satisfied", InvariantEngine.Evaluate(Rule, trigger, [trigger, Event("done", "completed", 5)], Start.AddMinutes(6)).Status);
    }

    [Fact]
    public void LateOutcomeIsRecoveredRatherThanOnTime()
    {
        var trigger = Event("start", "started");
        var result = InvariantEngine.Evaluate(Rule, trigger, [Event("done", "completed", 6)], Start.AddMinutes(7));
        Assert.Equal("recovered", result.Status);
        Assert.Empty(result.MissingTypes);
        Assert.Equal(["done"], result.MatchedEventIds);
        Assert.Equal(125m, result.ImpactAmount);
    }

    [Fact]
    public void ApplicationsAndCorrelationsAreIsolated()
    {
        var trigger = Event("start", "started");
        var result = InvariantEngine.Evaluate(Rule, trigger,
            [Event("otherapp", "completed", 1, "other"), Event("otherjourney", "completed", 1, correlation: "another")], Start.AddMinutes(9));
        Assert.Equal("violated", result.Status);
        Assert.Equal(["completed"], result.MissingTypes);
    }

    [Fact]
    public void OutcomesBeforeTriggerOrBeyondEvaluationTimeCannotSatisfy()
    {
        var trigger = Event("start", "started");
        Assert.Equal("pending", InvariantEngine.Evaluate(Rule, trigger,
            [Event("early", "completed", -1), Event("future", "completed", 4)], Start.AddMinutes(2)).Status);
    }

    [Fact]
    public void EveryExpectedTypeIsRequired()
    {
        var rule = Rule with { ExpectedTypes = ["stock.reserved", "shipment.created"] };
        var trigger = Event("start", "started");
        var result = InvariantEngine.Evaluate(rule, trigger, [Event("stock", "stock.reserved", 1)], Start.AddMinutes(6));
        Assert.Equal("violated", result.Status);
        Assert.Equal(["shipment.created"], result.MissingTypes);
    }

    [Fact]
    public void ArrivalOrderAndDuplicateOutcomesDoNotChangeDeterministicResult()
    {
        var trigger = Event("start", "started");
        BusinessEvent[] events = [Event("late", "completed", 7), Event("b", "completed", 2), Event("a", "completed", 2), trigger];
        var first = InvariantEngine.Evaluate(Rule, trigger, events, Start.AddMinutes(8));
        var second = InvariantEngine.Evaluate(Rule, trigger, events.Reverse(), Start.AddMinutes(8));
        Assert.Equal("satisfied", first.Status);
        Assert.Equal(["a"], first.MatchedEventIds);
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.MatchedEventIds, second.MatchedEventIds);
        Assert.Equal(first.Id, second.Id);
    }

    [Theory]
    [InlineData("{}", 0)]
    [InlineData("{\"amount\": -22}", 0)]
    [InlineData("{\"amount\": \"900\"}", 0)]
    [InlineData("{\"amount\": 120}", 120)]
    public void ImpactOnlyReadsNonnegativeNumericField(string data, int expected)
    {
        var trigger = Event("start", "started") with { Data = JsonDocument.Parse(data).RootElement.Clone() };
        Assert.Equal(expected, InvariantEngine.Evaluate(Rule, trigger, [], Start).ImpactAmount);
    }
}
