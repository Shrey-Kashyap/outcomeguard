using System.Text.Json;

namespace OutcomeGuard.Api;

public sealed class OutcomeService
{
    private readonly object _gate = new();
    private readonly StateRepository _repository;
    private readonly TimeProvider _clock;
    private readonly bool _demoEnabled;
    private StoredState _state;
    private const int MaxStoredEvents = 25_000;
    private const int MaxObligations = 30_000;

    public OutcomeService(StateRepository repository, TimeProvider clock, bool demoEnabled)
    {
        _repository = repository;
        _clock = clock;
        _demoEnabled = demoEnabled;
        _state = repository.Load();
    }

    private static StoredState Copy(StoredState state) =>
        JsonSerializer.Deserialize<StoredState>(JsonSerializer.Serialize(state, JsonDefaults.Options), JsonDefaults.Options)!;

    private T Change<T>(Func<StoredState, DateTimeOffset, T> action)
    {
        lock (_gate)
        {
            var next = Copy(_state);
            var result = action(next, _clock.GetUtcNow());
            _repository.Save(next);
            _state = next;
            return result;
        }
    }

    public StateView GetState()
    {
        lock (_gate) return View(Copy(_state));
    }

    public StateView Evaluate() => Change((state, now) =>
    {
        EvaluateState(state, now);
        return View(state);
    });

    private StateView View(StoredState state)
    {
        var evaluations = state.Evaluations;
        var completed = evaluations.Count(e => e.Status != "pending");
        var satisfied = evaluations.Count(e => e.Status == "satisfied");
        return new StateView(new Workspace("OutcomeGuard workspace", _demoEnabled ? "demo" : "connected"),
            state.Apps.ToArray(), state.Rules.ToArray(), state.Events.OrderByDescending(e => e.OccurredAt).ToArray(),
            evaluations.OrderByDescending(e => e.TriggeredAt).ToArray(),
            state.Incidents.OrderByDescending(i => i.OpenedAt)
                .Select(i => i with { IsDemo = state.DemoEvaluationIds.Contains(i.EvaluationId) }).ToArray(),
            new Summary(state.Events.Count, evaluations.Count, evaluations.Count(e => e.Status == "pending"),
                satisfied, evaluations.Count(e => e.Status == "violated"), evaluations.Count(e => e.Status == "recovered"),
                state.Incidents.Count(i => i.Status != "resolved"), state.Incidents.Count(i => i.Status == "resolved"),
                completed == 0 ? 0 : Math.Round(satisfied * 100m / completed, 1),
                state.Incidents.Where(i => i.Status != "resolved" && i.ImpactAmount > 0)
                    .GroupBy(i => i.ImpactUnit).Select(g => new ImpactTotal(g.Key, g.Sum(i => i.ImpactAmount))).ToArray()));
    }

    public BusinessApp CreateApp(CreateApp input) => Change((state, now) =>
    {
        Required(input.Name, "Application name", 80);
        Required(input.Domain, "Domain", 60);
        Limited(input.Description, "Description", 500);
        if (state.Apps.Count >= 100) throw new DomainException(409, "This MVP supports up to 100 applications.");
        var app = new BusinessApp("app_" + Guid.NewGuid().ToString("N"), input.Name.Trim(), input.Domain.Trim(), input.Description?.Trim() ?? "");
        state.Apps.Add(app);
        return app;
    });

    public Rule CreateRule(RuleInput input) => Change((state, now) =>
    {
        if (state.Rules.Count >= 200) throw new DomainException(409, "This MVP supports up to 200 rules.");
        var rule = ValidateRule(state, input, now);
        state.Rules.Add(rule);
        EvaluateState(state, now);
        return rule;
    });

    public PreviewResult Preview(RuleInput input)
    {
        lock (_gate)
        {
            var now = _clock.GetUtcNow();
            var rule = ValidateRule(_state, input, now) with { Id = "preview" };
            var events = _state.Events.Where(e => e.AppId == rule.AppId && e.OccurredAt <= now)
                .ToLookup(e => e.CorrelationId);
            var evaluations = events.SelectMany(journey => journey.Where(e => e.Type == rule.TriggerType)
                .Select(e => InvariantEngine.Evaluate(rule, e, journey, now))).ToArray();
            return new PreviewResult(evaluations,
                $"{evaluations.Length} historical journeys checked: {evaluations.Count(e => e.Status == "satisfied")} on time, {evaluations.Count(e => e.Status == "violated")} missing outcomes, {evaluations.Count(e => e.Status == "recovered")} late outcomes. No data was changed.");
        }
    }

    private static Rule ValidateRule(StoredState state, RuleInput input, DateTimeOffset now)
    {
        if (!state.Apps.Any(a => a.Id == input.AppId)) throw new DomainException(400, "Select an existing application.");
        Required(input.Name, "Rule name", 120);
        Limited(input.Description, "Description", 1000);
        EventType(input.TriggerType);
        if (input.ExpectedTypes is null || input.ExpectedTypes.Length is < 1 or > 8)
            throw new DomainException(400, "A rule needs between one and eight expected event types.");
        foreach (var type in input.ExpectedTypes) EventType(type);
        if (input.ExpectedTypes.Distinct(StringComparer.Ordinal).Count() != input.ExpectedTypes.Length
            || input.ExpectedTypes.Contains(input.TriggerType))
            throw new DomainException(400, "Expected types must be distinct and different from the trigger type.");
        if (input.WindowMinutes is < 1 or > 10080) throw new DomainException(400, "Deadline must be between 1 and 10080 minutes.");
        if (input.Severity is not ("critical" or "warning")) throw new DomainException(400, "Severity must be critical or warning.");
        Limited(input.ImpactField, "Impact field", 80);
        Limited(input.ImpactUnit, "Impact unit", 24);
        Limited(input.Owner, "Owner", 100);
        if (!string.IsNullOrWhiteSpace(input.ImpactField) && string.IsNullOrWhiteSpace(input.ImpactUnit))
            throw new DomainException(400, "Provide an impact unit when using an impact field.");
        return new Rule("rule_" + Guid.NewGuid().ToString("N"), input.AppId, input.Name.Trim(), input.Description?.Trim() ?? "",
            input.TriggerType, input.ExpectedTypes, input.WindowMinutes, input.Severity, input.ImpactField?.Trim() ?? "",
            input.ImpactUnit?.Trim() ?? "", input.Owner?.Trim() ?? "", input.Enabled, now);
    }

    public Rule SetEnabled(string id, EnabledInput input) => Change((state, now) =>
    {
        var index = state.Rules.FindIndex(r => r.Id == id);
        if (index < 0) throw new DomainException(404, "Rule not found.");
        var rule = state.Rules[index] with { Enabled = input.Enabled };
        state.Rules[index] = rule;
        EvaluateState(state, now);
        return rule;
    });

    public IngestResult Ingest(EventBatch batch) => Change((state, now) =>
    {
        var result = AddEvents(state, batch.Events, now);
        EvaluateState(state, now);
        return result;
    });

    private static IngestResult AddEvents(StoredState state, EventInput[]? inputs, DateTimeOffset now)
    {
        if (inputs is null || inputs.Length is < 1 or > 100) throw new DomainException(400, "Send between 1 and 100 events per batch.");
        var known = state.Events.ToDictionary(e => (e.AppId, e.Id));
        var accepted = new List<BusinessEvent>();
        var duplicates = 0;
        foreach (var input in inputs)
        {
            if (input is null) throw new DomainException(400, "Event cannot be null.");
            Required(input.Id, "Event ID", 100);
            Required(input.CorrelationId, "Correlation ID", 120);
            EventType(input.Type);
            if (!state.Apps.Any(a => a.Id == input.AppId)) throw new DomainException(400, "Every event must reference an existing application.");
            if (input.Data.ValueKind != JsonValueKind.Object) throw new DomainException(400, "Event data must be a JSON object.");
            if (input.Data.GetRawText().Length > 16_384) throw new DomainException(400, "Event data cannot exceed 16 KB.");
            if (input.OccurredAt == default || input.OccurredAt < DateTimeOffset.UnixEpoch)
                throw new DomainException(400, "Event timestamp must be a valid date on or after 1970-01-01.");
            if (input.OccurredAt > now.AddSeconds(30)) throw new DomainException(400, "Event timestamp is more than 30 seconds in the future.");
            if (known.TryGetValue((input.AppId, input.Id), out var existing))
            {
                if (existing.CorrelationId != input.CorrelationId || existing.Type != input.Type
                    || existing.OccurredAt != input.OccurredAt || !JsonElement.DeepEquals(existing.Data, input.Data))
                    throw new DomainException(409, $"Event ID '{input.Id}' already exists with a different payload in this application.");
                duplicates++;
                continue;
            }
            var ev = new BusinessEvent(input.Id, input.AppId, input.CorrelationId, input.Type, input.OccurredAt.ToUniversalTime(), now, input.Data.Clone());
            known.Add((ev.AppId, ev.Id), ev);
            accepted.Add(ev);
        }
        if (state.Events.Count + accepted.Count > MaxStoredEvents)
            throw new DomainException(409, "This single-workspace MVP is limited to 25000 stored events. Archive the database before starting a new workspace.");
        state.Events.AddRange(accepted);
        return new IngestResult(accepted.Count, duplicates);
    }

    public Incident Acknowledge(string id, AcknowledgeInput input) => Change((state, now) =>
    {
        Limited(input.Note, "Acknowledgement note", 1000);
        var index = IncidentIndex(state, id);
        var incident = state.Incidents[index];
        if (incident.Status == "resolved") throw new DomainException(409, "This incident is already resolved.");
        if (incident.Status == "acknowledged") return incident;
        incident = incident with { Status = "acknowledged" };
        incident.Timeline.Add(new TimelineEntry(now, "acknowledged", "Investigation acknowledged",
            string.IsNullOrWhiteSpace(input.Note) ? "An operator acknowledged this incident." : input.Note.Trim()));
        state.Incidents[index] = incident;
        return incident;
    });

    public VerifyResult Verify(string id) => Change((state, now) =>
    {
        EvaluateState(state, now);
        var index = IncidentIndex(state, id);
        var incident = state.Incidents[index];
        if (incident.Status == "resolved") return new VerifyResult(true, "This incident was already verified and resolved.", incident);
        var evaluation = state.Evaluations.Single(e => e.Id == incident.EvaluationId);
        if (evaluation.MissingTypes.Length != 0)
        {
            var message = "Recovery is not verified. Still missing: " + string.Join(", ", evaluation.MissingTypes) + ".";
            // Bound repeated failed-verification audit entries without suppressing distinct evidence changes.
            if (incident.Timeline.LastOrDefault()?.Detail != message)
                incident.Timeline.Add(new TimelineEntry(now, "verification_failed", "Recovery verification blocked", message));
            return new VerifyResult(false, message, incident);
        }
        var summary = "All required outcomes have matching stored event evidence. Original deadline history is preserved.";
        incident = incident with { Status = "resolved", ResolvedAt = now,
            Diagnosis = incident.Diagnosis with { Summary = summary },
            Verification = new Verification(now, evaluation.MatchedEventIds, summary) };
        incident.Timeline.Add(new TimelineEntry(now, "resolved", "Recovery verified", summary));
        state.Incidents[index] = incident;
        return new VerifyResult(true, summary, incident);
    });

    private static int IncidentIndex(StoredState state, string id)
    {
        var index = state.Incidents.FindIndex(i => i.Id == id);
        return index >= 0 ? index : throw new DomainException(404, "Incident not found.");
    }

    private static void EvaluateState(StoredState state, DateTimeOffset now)
    {
        var triggersByType = state.Events.ToLookup(e => (e.AppId, e.Type));
        var eventsByJourney = state.Events.Where(e => e.OccurredAt <= now)
            .ToLookup(e => (e.AppId, e.CorrelationId));
        var evaluationIndex = state.Evaluations.Select((e, index) => (e.Id, index))
            .ToDictionary(entry => entry.Id, entry => entry.index);
        var evaluationsByRule = state.Evaluations.ToLookup(e => e.RuleId);
        var incidentIndexByEvaluation = state.Incidents.Select((i, index) => (i.EvaluationId, index))
            .ToDictionary(entry => entry.EvaluationId, entry => entry.index);
        var capacity = state.Rules.Sum(rule => rule.Enabled
            ? triggersByType[(rule.AppId, rule.TriggerType)].Count()
            : evaluationsByRule[rule.Id].Count());
        if (capacity > MaxObligations)
            throw new DomainException(409, "This workspace supports up to 30000 obligations. Pause overlapping rules or start a fresh workspace.");

        foreach (var rule in state.Rules)
        {
            var triggers = triggersByType[(rule.AppId, rule.TriggerType)].Where(e => e.OccurredAt <= now);
            foreach (var trigger in triggers)
            {
                var id = InvariantEngine.EvaluationId(rule.Id, trigger.Id);
                var hasEvaluation = evaluationIndex.TryGetValue(id, out var evalIndex);
                if (!rule.Enabled && !hasEvaluation) continue;
                var related = eventsByJourney[(rule.AppId, trigger.CorrelationId)];
                var evaluation = InvariantEngine.Evaluate(rule, trigger, related, now);
                if (hasEvaluation) state.Evaluations[evalIndex] = evaluation; else state.Evaluations.Add(evaluation);
                var incidentIndex = incidentIndexByEvaluation.GetValueOrDefault(id, -1);
                if (incidentIndex < 0 && evaluation.Status is "violated" or "recovered")
                {
                    var incident = new Incident("inc_" + evaluation.Id[5..], evaluation.Id, rule.Id, rule.Name, rule.AppId,
                        trigger.CorrelationId, rule.Severity, "open", now, null, rule.Owner, evaluation.ImpactAmount,
                        evaluation.ImpactUnit, evaluation.MissingTypes, InvariantEngine.Diagnose(evaluation, rule, related),
                        [new TimelineEntry(evaluation.Deadline, "deadline", "Business deadline", "Required: " + string.Join(", ", rule.ExpectedTypes)),
                         new TimelineEntry(now, "opened", "Missed outcome detected", "The business commitment was not completed within its deadline.")], null);
                    state.Incidents.Add(incident);
                    incidentIndex = state.Incidents.Count - 1;
                }
                if (incidentIndex >= 0)
                {
                    if (state.Incidents[incidentIndex].Status == "resolved") continue;
                    var incident = state.Incidents[incidentIndex] with
                    {
                        MissingTypes = evaluation.MissingTypes,
                        Diagnosis = InvariantEngine.Diagnose(evaluation, rule, related)
                    };
                    foreach (var ev in related)
                    {
                        var detail = $"Event {ev.Id}; received {ev.ReceivedAt:O}.";
                        if (!incident.Timeline.Any(t => t.Kind == "event" && t.Detail == detail))
                            incident.Timeline.Add(new TimelineEntry(ev.OccurredAt, "event", ev.Type, detail));
                    }
                    incident.Timeline.Sort((a, b) => a.At.CompareTo(b.At));
                    state.Incidents[incidentIndex] = incident;
                }
            }
        }
    }

    private static void Required(string? value, string name, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max) throw new DomainException(400, $"{name} is required and must be at most {max} characters.");
    }

    private static void Limited(string? value, string name, int max)
    {
        if (value?.Length > max) throw new DomainException(400, $"{name} must be at most {max} characters.");
    }

    private static void EventType(string? value)
    {
        Required(value, "Event type", 100);
        if (value!.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')))
            throw new DomainException(400, "Event types may contain ASCII letters, digits, dots, underscores and hyphens.");
    }

    public MessageResult RunDemo(string scenario) => Change((state, now) =>
    {
        RequireDemo();
        var definition = DemoScenarios.Get(scenario);
        if (!state.Apps.Any(a => a.Id == definition.App.Id))
        {
            if (state.Apps.Count >= 100) throw new DomainException(409, "This MVP supports up to 100 applications.");
            state.Apps.Add(definition.App);
        }
        var rule = definition.Rule with { CreatedAt = now };
        var existing = state.Rules.SingleOrDefault(r => r.Id == rule.Id);
        if (existing is null)
        {
            if (state.Rules.Count >= 200) throw new DomainException(409, "This MVP supports up to 200 rules.");
            state.Rules.Add(rule);
        }
        else if (!existing.Enabled) throw new DomainException(409, "Resume this demo's rule before running the scenario again.");
        var run = Guid.NewGuid().ToString("N")[..8];
        var inputs = new List<EventInput>();
        for (var journey = 0; journey < 3; journey++)
        {
            var correlation = $"{definition.Prefix}-{run}-{journey + 1}";
            var start = now.AddMinutes(-45 - journey * 4);
            var data = JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                [rule.ImpactField] = definition.Impact + journey * definition.Increment,
                ["synthetic"] = true,
                ["description"] = definition.Description
            });
            var triggerId = $"{run}-{journey}-trigger";
            inputs.Add(new EventInput(triggerId, rule.AppId, correlation, rule.TriggerType, start, data));
            state.DemoEvaluationIds.Add(InvariantEngine.EvaluationId(rule.Id, triggerId));
            for (var outcome = 0; outcome < rule.ExpectedTypes.Length; outcome++)
            {
                if (journey == 0 && outcome == rule.ExpectedTypes.Length - 1) continue;
                inputs.Add(new EventInput($"{run}-{journey}-outcome-{outcome}", rule.AppId, correlation,
                    rule.ExpectedTypes[outcome], start.AddMinutes(outcome + 1), JsonSerializer.SerializeToElement(new { synthetic = true })));
            }
            if (journey == 0)
                inputs.Add(new EventInput($"{run}-failure", rule.AppId, correlation, definition.FailureType, start.AddMinutes(2),
                    JsonSerializer.SerializeToElement(new { synthetic = true, reason = definition.FailureReason })));
        }
        AddEvents(state, inputs.ToArray(), now);
        EvaluateState(state, now);
        return new MessageResult($"{definition.App.Name}: three synthetic journeys ingested, including one missed business outcome.");
    });

    public MessageResult RecoverDemo(string id) => Change((state, now) =>
    {
        RequireDemo();
        var incident = state.Incidents[IncidentIndex(state, id)];
        if (!state.DemoEvaluationIds.Contains(incident.EvaluationId))
            throw new DomainException(403, "Recovery simulation is only available for server-created demo journeys.");
        var evaluation = state.Evaluations.Single(e => e.Id == incident.EvaluationId);
        if (evaluation.MissingTypes.Length == 0) return new MessageResult("The required demo evidence is already present. Run verification to close the incident.");
        var inputs = evaluation.MissingTypes.Select(type => new EventInput("demo-recovery-" + Guid.NewGuid().ToString("N"),
            incident.AppId, incident.CorrelationId, type, now, JsonSerializer.SerializeToElement(new { synthetic = true, simulatedRecovery = true }))).ToArray();
        AddEvents(state, inputs, now);
        incident.Timeline.Add(new TimelineEntry(now, "simulation", "Demo recovery simulated", "Synthetic outcome evidence was added. No external business action was performed."));
        EvaluateState(state, now);
        return new MessageResult("Synthetic recovery evidence was ingested. Verify recovery to close this incident.");
    });

    private void RequireDemo()
    {
        if (!_demoEnabled) throw new DomainException(404, "Demo endpoints are disabled.");
    }
}
