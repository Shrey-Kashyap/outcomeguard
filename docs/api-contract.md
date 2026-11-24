# MVP contract

JSON uses camelCase. Base `/api`. UTC ISO timestamps. All identifiers are strings. Errors: `{ error: string }` with non-2xx status. All APIs require `X-Api-Key` when configured; local demo mode permits loopback usage without a key. GET `/api/config` publicly returns `{ authRequired, demoEnabled }`. UI key stays in session memory. No runtime AI dependency.

## State
GET `/state`: `{ workspace: { name, mode }, apps, rules, events, evaluations, incidents, summary }`.
- App: `{ id, name, domain, description }`.
- Rule: `{ id, appId, name, description, triggerType, expectedTypes: string[], windowMinutes, severity, impactField, impactUnit, owner, enabled, createdAt }`. severity `critical|warning`. impactField is a top-level numeric event data key; unit such as INR or people. Empty field means impact 0. Rules are immutable except enabled flag, preserving historical meaning.
- Event: `{ id, appId, correlationId, type, occurredAt, receivedAt, data: object }`.
- Evaluation: `{ id, ruleId, ruleName, appId, correlationId, triggerEventId, triggeredAt, deadline, status, missingTypes: string[], matchedEventIds: string[], impactAmount, impactUnit }`. Status `pending|satisfied|violated|recovered`. One obligation per rule and trigger event; each journey should use a distinct correlationId.
- Incident: `{ id, evaluationId, ruleId, ruleName, appId, correlationId, severity, status, openedAt, resolvedAt, owner, impactAmount, impactUnit, missingTypes: string[], diagnosis: { summary, facts: string[], hypotheses: string[], nextSteps: string[] }, timeline: [{ at, kind, title, detail }], verification: null | { verifiedAt, evidenceEventIds: string[], summary } }`. Status `open|acknowledged|resolved`. Late evidence does not automatically close incidents; verify explicitly.
- Summary: `{ totalEvents, totalEvaluations, pending, satisfied, violated, recovered, openIncidents, resolvedIncidents, successRate, impact: [{ unit, amount }] }`. Success rate uses on-time satisfied / completed obligations, excludes pending. Impact totals unresolved incidents by unit, never combines currencies.

## Mutations
- POST `/apps`: `{ name, domain, description }` -> App.
- POST `/rules`: Rule fields except id/createdAt; enabled defaults true -> Rule.
- POST `/rules/preview`: same body -> `{ evaluations: Evaluation[], summary: string }`, no writes.
- PATCH `/rules/{id}/enabled`: `{ enabled: boolean }` -> Rule.
- POST `/events`: `{ events: [{ id, appId, correlationId, type, occurredAt, data }] }` -> `{ accepted, duplicates }`. Atomic batch, max 100. Identical event IDs replay safely; mismatched payload for same ID is 409. app-scoped event IDs. Reject future event timestamps beyond small clock tolerance.
- POST `/evaluate`: empty JSON body -> State. Server clock only.
- POST `/incidents/{id}/acknowledge`: `{ note: string }` -> Incident.
- POST `/incidents/{id}/verify`: `{}` -> `{ verified: boolean, message: string, incident }`; false does not close incident. Only stored matching outcome evidence can verify recovery.
- POST `/demo/{scenario}` with scenario `commerce|events|hr`: `{}` -> `{ message: string }`. Append synthetic healthy and failed journeys, auto-create scenario app/rule as needed. Evaluates immediately. Repeat runs create fresh synthetic IDs.
- POST `/demo/incidents/{id}/recover`: `{}` -> `{ message: string }`. Demo-only, synthetic incident only; ingest missing expected outcome events at current time. Still requires separate verify call. Does not execute real business actions.

## Engine semantics
Match appId + correlationId; outcome event time must be at or after trigger and no later than evaluation clock. Deadline is trigger time + window; deadline inclusive for success, violation at now > deadline. All expectedTypes required. Data that arrives late but occurred on time can satisfy an obligation, while preserving any incident already observed until explicit verification. Diagnostic hypotheses must be labeled unproven. Paused rules create no new obligations; existing obligations continue evaluation. Periodic evaluation opens incidents without a user refresh. SQLite persistence. No arbitrary expression execution.
