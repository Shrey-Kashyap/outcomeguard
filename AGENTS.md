# Working on OutcomeGuard

OutcomeGuard monitors business commitments across domains. Keep the event engine deterministic and explain each incident using stored evidence. Never claim hypotheses are proven causes.

## Boundaries
- Use synthetic demo data only. Never commit credentials, local databases, logs with user data, or production payloads.
- Keep recovery verification read-only with respect to external business systems. A simulated recovery must be labeled as a demo.
- Preserve idempotent ingestion, application-scoped correlation, deadlines, and historical incidents.
- Keep business language and accessible interactions in the UI.
- Document limitations honestly; this is a single-workspace MVP, not a production multi-tenant service.

## Validation
- Run the .NET unit/integration suite, frontend production build, and browser smoke tests when relevant.
- Record actual observed commands and outcomes in docs/evidence.md. Do not invent models, reasoning settings, user research, or lifecycle sessions.
- Commit coherent completed milestones with descriptive messages. Do not backdate commits or manufacture history.

## Ownership during parallel implementation
- Backend agent: src/OutcomeGuard.Api and tests/OutcomeGuard.Tests.
- Frontend agent: frontend.
- Documentation agent: docs (except evidence-validation updates by the coordinator).
- Coordinator: root configuration, CI, scripts, integration, final review, and publication.
