using System.Security.Cryptography;
using System.Text;

namespace OutcomeGuard.Api;

/// <summary>Pure, deterministic temporal invariant evaluation. No I/O, wall clock, or domain-specific rules.</summary>
public static class InvariantEngine
{
    public static string EvaluationId(string ruleId, string triggerId) =>
        "eval_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ruleId + "\0" + triggerId)))[..24];

    public static Evaluation Evaluate(Rule rule, BusinessEvent trigger, IEnumerable<BusinessEvent> events, DateTimeOffset now)
    {
        var deadline = trigger.OccurredAt.AddMinutes(rule.WindowMinutes);
        var outcomes = events.Where(e => e.AppId == trigger.AppId && e.CorrelationId == trigger.CorrelationId
                && e.OccurredAt >= trigger.OccurredAt && e.OccurredAt <= now && rule.ExpectedTypes.Contains(e.Type))
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
        var matched = rule.ExpectedTypes.Select(type => outcomes.FirstOrDefault(e => e.Type == type))
            .Where(e => e is not null).Select(e => e!).ToArray();
        var missing = rule.ExpectedTypes.Except(matched.Select(e => e.Type)).ToArray();
        var status = missing.Length == 0
            ? matched.All(e => e.OccurredAt <= deadline) ? "satisfied" : "recovered"
            : now > deadline ? "violated" : "pending";
        var impact = 0m;
        if (!string.IsNullOrWhiteSpace(rule.ImpactField)
            && trigger.Data.TryGetProperty(rule.ImpactField, out var value)
            && value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetDecimal(out var number))
            impact = Math.Clamp(number, 0, 1_000_000_000_000m);
        return new Evaluation(EvaluationId(rule.Id, trigger.Id), rule.Id, rule.Name, trigger.AppId,
            trigger.CorrelationId, trigger.Id, trigger.OccurredAt, deadline, status, missing,
            matched.Select(e => e.Id).ToArray(), impact, rule.ImpactUnit);
    }

    public static Diagnosis Diagnose(Evaluation evaluation, Rule rule, IEnumerable<BusinessEvent> allEvents)
    {
        var related = allEvents.Where(e => e.AppId == evaluation.AppId && e.CorrelationId == evaluation.CorrelationId)
            .OrderBy(e => e.OccurredAt).ToArray();
        var missing = evaluation.MissingTypes.Length == 0 ? "none; evidence is now complete" : string.Join(", ", evaluation.MissingTypes);
        var facts = new List<string>
        {
            $"Trigger {rule.TriggerType} was recorded at {evaluation.TriggeredAt:O}.",
            $"All required outcomes were due by {evaluation.Deadline:O} ({rule.WindowMinutes} minute window).",
            $"Missing outcome types at the latest evaluation: {missing}.",
            $"{related.Length} stored events share this application and correlation ID."
        };
        var failures = related.Where(e => e.Type.EndsWith(".failed", StringComparison.Ordinal)).ToArray();
        foreach (var failure in failures)
            facts.Add($"Event {failure.Id} reports {failure.Type} at {failure.OccurredAt:O}; this is producer-supplied evidence.");
        return new Diagnosis(evaluation.MissingTypes.Length == 0
                ? "Required outcome evidence is present. Verify recovery to close the incident."
                : "The expected business outcome was not observed within the agreed deadline.",
            facts.ToArray(),
            evaluation.MissingTypes.Length == 0 ? [] :
            ["The downstream operation may be delayed or may have failed. This is a hypothesis, not a proven root cause.",
             "The operation may have succeeded without its event reaching OutcomeGuard. Check producer logs and ingestion delivery."],
            ["Inspect the correlation timeline and the responsible service's logs.",
             "Confirm the real business state before any corrective action; avoid duplicate fulfilment or grants.",
             "After an approved correction, ingest the outcome evidence and run recovery verification."]);
    }
}
