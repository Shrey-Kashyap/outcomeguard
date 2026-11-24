# Security and deployment boundaries

OutcomeGuard is a single-workspace MVP. Evaluate it with synthetic data before considering real business events.

## Access model

The local development/demo path can run without an API key only when both the network peer and request Host are loopback. A configured API key is required for API access in secured mode, and production startup must fail closed when no key is configured. The browser accepts a key for the current session rather than embedding it in a published bundle. The public configuration endpoint reports only whether authentication and demos are enabled.

Browser writes require a same-origin request, with an explicit Development exception for the Vite origins `http://localhost:5173` and `http://127.0.0.1:5173`. The API does not offer permissive cross-origin access. These controls reduce unwanted browser-origin requests; they do not create an identity model for an unauthenticated local workspace.

An API key provides workspace access, not individual identity. There are no user accounts, per-application credentials, roles, key rotation UI, or tenant isolation. Do not publish an unauthenticated development server to the internet. A secured external deployment still needs HTTPS, a trusted reverse proxy, rate limiting appropriate to the environment, secret storage, and operational ownership.

## Data trust

- Producers control event type, correlation ID, event time, and data. Validation rejects malformed or out-of-bounds inputs; it does not authenticate the truth of a business outcome.
- Never send passwords, access tokens, payment-card details, or unnecessary personal data. Prefer opaque business identifiers.
- Do not commit local database files or real event exports. Demo names, journeys, and payloads are synthetic.
- The API stores evidence for investigation. The MVP has no retention or deletion workflow, field-level encryption, or compliance certification.
- Use one unique correlation ID for each business journey. A reused ID can make unrelated trigger occurrences share matching outcome evidence.

## Recovery boundary

Acknowledging an incident does not prove recovery. Verification requires the configured outcome evidence. Verification is not an external business action and does not independently prove a real-world result.

The synthetic recovery endpoint is for demo incidents only and must be disabled with the other demo features in a real deployment. It inserts clearly synthetic events; it does not ship orders, issue event passes, or revoke employee access.

## What to review before real deployment

1. Add authenticated producer identities, scoped credentials, transport security, rate limits, and monitoring for ingestion failures.
2. Establish event schemas, acceptable clock skew, source freshness, replay handling, retention, and data minimization.
3. Define incident ownership and escalation. Absence of an event may reflect missing telemetry rather than a failed business operation.
4. Back up persistent storage, test restoration, and load-test realistic traffic. The current architecture is not a horizontally scaled event platform.
5. Add accounts, role checks, audit attribution, and tenant separation before offering access to separate customers.

Repository publication review should cover tracked files, history, configuration examples, package outputs, screenshots, and archives. A passing secret-pattern scan alone is not proof that all sensitive material has been removed.
