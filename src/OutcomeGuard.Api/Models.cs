using System.Text.Json;

namespace OutcomeGuard.Api;

public record BusinessApp(string Id, string Name, string Domain, string Description);
public record CreateApp(string Name, string Domain, string Description);
public record RuleInput(string AppId, string Name, string Description, string TriggerType, string[] ExpectedTypes,
    int WindowMinutes, string Severity, string ImpactField, string ImpactUnit, string Owner, bool Enabled = true);
public record Rule(string Id, string AppId, string Name, string Description, string TriggerType, string[] ExpectedTypes,
    int WindowMinutes, string Severity, string ImpactField, string ImpactUnit, string Owner, bool Enabled, DateTimeOffset CreatedAt);
public record EventInput(string Id, string AppId, string CorrelationId, string Type, DateTimeOffset OccurredAt, JsonElement Data);
public record BusinessEvent(string Id, string AppId, string CorrelationId, string Type, DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt, JsonElement Data);
public record EventBatch(EventInput[] Events);
public record IngestResult(int Accepted, int Duplicates);
public record EnabledInput(bool Enabled);
public record AcknowledgeInput(string Note);
public record Evaluation(string Id, string RuleId, string RuleName, string AppId, string CorrelationId, string TriggerEventId,
    DateTimeOffset TriggeredAt, DateTimeOffset Deadline, string Status, string[] MissingTypes, string[] MatchedEventIds,
    decimal ImpactAmount, string ImpactUnit);
public record Diagnosis(string Summary, string[] Facts, string[] Hypotheses, string[] NextSteps);
public record TimelineEntry(DateTimeOffset At, string Kind, string Title, string Detail);
public record Verification(DateTimeOffset VerifiedAt, string[] EvidenceEventIds, string Summary);
public record Incident(string Id, string EvaluationId, string RuleId, string RuleName, string AppId, string CorrelationId,
    string Severity, string Status, DateTimeOffset OpenedAt, DateTimeOffset? ResolvedAt, string Owner, decimal ImpactAmount,
    string ImpactUnit, string[] MissingTypes, Diagnosis Diagnosis, List<TimelineEntry> Timeline, Verification? Verification);
public record Workspace(string Name, string Mode);
public record ImpactTotal(string Unit, decimal Amount);
public record Summary(int TotalEvents, int TotalEvaluations, int Pending, int Satisfied, int Violated, int Recovered,
    int OpenIncidents, int ResolvedIncidents, decimal SuccessRate, ImpactTotal[] Impact);
public record StateView(Workspace Workspace, BusinessApp[] Apps, Rule[] Rules, BusinessEvent[] Events,
    Evaluation[] Evaluations, Incident[] Incidents, Summary Summary);
public record PreviewResult(Evaluation[] Evaluations, string Summary);
public record VerifyResult(bool Verified, string Message, Incident Incident);
public record MessageResult(string Message);

public sealed class StoredState
{
    public List<BusinessApp> Apps { get; set; } = [];
    public List<Rule> Rules { get; set; } = [];
    public List<BusinessEvent> Events { get; set; } = [];
    public List<Evaluation> Evaluations { get; set; } = [];
    public List<Incident> Incidents { get; set; } = [];
    // Demo provenance is server-controlled and is never inferred from user-supplied event payloads.
    public HashSet<string> DemoEvaluationIds { get; set; } = [];
}

public sealed class DomainException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
