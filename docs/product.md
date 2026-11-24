# Product brief

## The problem

A healthy API does not prove that a business commitment was fulfilled. An order can be paid but never shipped, an attendee can register without receiving a pass, and a departing employee can retain access. These failures cross service boundaries and can escape ordinary uptime and error monitoring.

**OutcomeGuard makes an expected outcome explicit, detects missing evidence, and keeps the incident open until recovery is verified.** Payment is one trigger among many; the engine does not contain payment-specific evaluation logic.

This brief is based on the project owner's request and the referenced product discussion. The personas below are design hypotheses, not the result of completed interviews or measured market research.

## People and jobs

| Persona | Job to be done | First useful result |
| --- | --- | --- |
| Operations lead | Find commitments that need attention and prioritize the work | Open incidents, affected journeys, and exposure grouped by unit |
| Product manager | Turn a business promise into a measurable expectation | A readable rule with a trigger, required outcomes, deadline, and owner |
| Developer or QA engineer | Explain a failed journey and check a correction | A correlated event timeline, observed facts, and explicit recovery verification |
| HR or event operations owner | Apply the same workflow outside commerce | A relevant demo that uses the same generic rule engine |

## Primary workflow

1. Register an application or start one of the synthetic demos.
2. Define a rule: after a trigger event, require one or more outcome event types within a time window.
3. Preview the rule against stored events, then enable it.
4. Send events with stable IDs and a journey correlation ID through the ingestion API or Event Explorer.
5. Review business health. Open an incident to see the missing outcomes, deadline, owner, exposure, and evidence.
6. Acknowledge responsibility and investigate. Treat diagnosis hypotheses as unproven leads.
7. Correct the source business system outside OutcomeGuard. In a demo, use the clearly labeled synthetic recovery action instead.
8. Ingest the resulting outcome evidence and explicitly verify recovery. Keep the original incident history.

## MVP scope

- One workspace, several applications, a React interface, ASP.NET Core API, and local SQLite persistence.
- A generic temporal invariant: all expected event types must follow a trigger within a configured deadline.
- Guided rule creation, preview, enable/pause, event ingestion, periodic evaluation, incident investigation, acknowledgment, and verification.
- Commerce, event registration, and HR offboarding demos built from synthetic events.
- Deterministic diagnosis from observed evidence, with clearly separated unproven hypotheses.
- Unit/integration checks, frontend build, local run path, container configuration, CI, and an auditable evidence pack.

## Acceptance criteria

| Area | Observable behavior |
| --- | --- |
| Domain independence | All three demos use the same evaluator; adding a rule does not require evaluator changes |
| Rules | Users can create a rule, preview its effect without saving, and pause new obligations; creating or resuming a rule evaluates retained historical triggers |
| Ingestion | Valid batches persist; exact retries do not duplicate events; conflicting reuse of an event ID fails atomically |
| Correlation | An outcome from a different application or journey never satisfies an obligation |
| Time | Outcomes exactly at the deadline satisfy it; missing outcomes become violations only after the deadline |
| Out-of-order delivery | Matching uses event time, while receipt time remains available as provenance |
| Incidents | A failed obligation has one persistent incident, a timeline, owner, missing evidence, and exposure |
| Diagnosis | Observed facts are distinguishable from possible explanations; absence of an event is not presented as proof of source-system failure |
| Recovery | Verification fails while required evidence is missing and succeeds only when matching evidence exists |
| Durability | Application state survives an API restart with the same database |
| Usability | Empty, loading, and error states provide a usable next action; primary workflows fit smaller screens |
| Publication | Public source excludes credentials, databases, private work, and captured user data |

## Boundaries and next work

This release is a runnable SaaS-shaped MVP, not a complete production SaaS business. It has no customer tenancy, user accounts, SSO, role-based permissions, billing, managed connector catalog, outbound alerts, or automatic external remediation. It does not prove access was revoked or an order shipped; it proves that an authorized producer supplied the configured evidence.

The rule vocabulary does not yet express absence constraints, exact event cardinality, numeric reconciliation, cancellation, or arbitrary conditions. Use one correlation ID per business journey. Repeated trigger events are separate obligations, and one matching outcome can satisfy more than one trigger when a journey ID is reused. A new rule evaluates retained historical triggers. Pausing suppresses unseen obligations while paused; resuming deliberately backfills those retained triggers. Existing obligations keep running throughout.

Storage is bounded to 25,000 events and 200 user-created rules in one process. SQLite persists an atomic serialized workspace snapshot. This is a practical demonstration store, not a high-volume event warehouse.

Exposure is an estimate from a trigger's numeric data field. It is not booked loss or accounting output. Multiple rules on the same journey may count exposure more than once, so totals should be interpreted as obligation-level exposure. Different units are never added together.

Next priorities: validate the workflow with actual operators; improve ingestion trust and freshness monitoring; add source integrations and notifications; then address multi-tenant identity, scale, retention, and deployment operations. Record those as future work until implemented and observed.
