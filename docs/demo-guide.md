# Demo walkthrough

The demos use synthetic data and the real API, store, evaluator, and incident workflow. They require no external accounts or business-system access. Re-running a scenario adds fresh journeys; it does not reset existing history.

## A five-minute product story

1. Open OutcomeGuard and start in the Demo Lab.
2. Run **E-commerce**. Explain the commitment: a captured payment should result in fulfilment within the configured time window. Healthy journeys and a failed journey are ingested through the same domain-independent engine.
3. Open the resulting incident. Show the deadline, missing outcome evidence, owner, business exposure, and event timeline. Distinguish facts from possible causes.
4. Acknowledge the incident. Select **Verify recovery** before adding evidence: the incident should remain open because the outcome is still missing.
5. Use the clearly labeled demo recovery action. This adds synthetic evidence only. Verify again, then show the recorded evidence and preserved incident history.
6. Run **Event registration** and **HR offboarding**. Their different event names and deadlines require no evaluator changes.
7. Visit Rule Studio, preview a custom expectation, then create it. Use Event Explorer or the API to submit matching events and observe the evaluated result.

## Scenarios

| Scenario | Intended user | Commitment | Failure illustrated |
| --- | --- | --- | --- |
| E-commerce | Fulfilment or operations lead | A paid order should reserve inventory and create a shipment | Inventory is reserved, but shipment creation is missing |
| Event registration | Event operations manager | A registration should produce a pass and confirmation | A pass is issued, but confirmation delivery is missing |
| HR offboarding | HR and IT operations | Offboarding should revoke access and terminate active sessions | Access revocation is recorded, but active-session termination is missing |

The supplied rule definitions are:

| Application | Trigger | Required outcomes | Deadline |
| --- | --- | --- | --- |
| Northstar Commerce | `payment.captured` | `inventory.reserved`, `shipment.created` | 15 minutes |
| Gather Events | `registration.completed` | `ticket.issued`, `confirmation.sent` | 5 minutes |
| PeopleOps | `employee.offboarded` | `access.revoked`, `sessions.terminated` | 10 minutes |

Use the rule shown in the app as the current event-name/deadline reference if these definitions are changed. The scenarios demonstrate monitoring contracts; they do not connect to a real shop, ticketing provider, or identity system. The two healthy journeys satisfy every required outcome; the failed journey deliberately omits the last required type and supplies a synthetic producer failure event for investigation.

## Creating an independent rule

Choose an application and describe the promise in plain language. Select a trigger event name, one or more required outcome event names, a window in minutes, severity, and owner. Optionally choose a numeric trigger-data field and a unit for estimated exposure. Preview against stored events before saving. New rules immediately evaluate retained historical triggers. Paused rules keep evaluating existing obligations; resuming also backfills triggers received while paused.

In Event Explorer, adapt the generated JSON example. Keep each producer event ID stable across retries; use a new correlation ID for a new journey. An outcome from another application or correlation ID will not satisfy the rule. Sending the same event again should report a duplicate rather than create another obligation.

## Reading the dashboard honestly

- On-time success excludes obligations still waiting within their deadline. Recovered late outcomes do not become on-time successes.
- Unresolved incidents can remain visible after matching evidence arrives, until an operator verifies recovery.
- Exposure is estimated per obligation. It can overlap across rules and must not be described as confirmed financial loss.
- Counts reflect this local workspace and its stored evidence, not uptime or live business-system health.

## Reset and handoff

There is no destructive reset button in the product. To start with a fresh database, stop the app and point it at a new empty database path using the documented configuration. Preserve the existing file if its incident history matters. Share only synthetic demonstrations or sanitized exports.
