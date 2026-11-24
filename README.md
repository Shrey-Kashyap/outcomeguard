# OutcomeGuard

**Your services are running. Are your business commitments being met?**

OutcomeGuard turns application events into visible business obligations. Define an expectation such as “every employee offboarding must revoke access within 15 minutes,” send events, and investigate missed outcomes with a correlated timeline and verifiable recovery.

A runnable, single-workspace SaaS MVP built with **ASP.NET Core 10, React, TypeScript, and SQLite**. All dashboard data comes from the API and durable database. There is no runtime LLM or paid service dependency.

## Who it helps

| User | Job to do | OutcomeGuard workflow |
| --- | --- | --- |
| Operations lead | Find commitments that quietly fell through | Prioritize open incidents by severity and estimated exposure |
| Product or QA lead | Make business expectations testable | Create a rule, preview it against recorded events, and monitor outcomes |
| Developer | Explain what happened across services | Inspect the event timeline, observed facts, and clearly labeled hypotheses |
| IT operations | Verify an offboarding outcome | Require access-revocation evidence before resolving an incident |

These personas are design hypotheses, not claims of completed customer interviews.

## Try it locally

For a quick start, see the [runnable release](https://github.com/Shrey-Kashyap/outcomeguard/releases/tag/v0.1.0). To build from source, install [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and [Node.js](https://nodejs.org/) 22.12 or newer.

```sh
git clone https://github.com/Shrey-Kashyap/outcomeguard.git
cd outcomeguard
```

Windows PowerShell:

```powershell
./scripts/run.ps1
```

macOS / Linux:

```sh
sh scripts/run.sh
```

Open **http://127.0.0.1:5080**. The first start installs frontend packages and builds both parts. Local Development mode is restricted to loopback; demo events are synthetic. SQLite state persists under `src/OutcomeGuard.Api/data/`.

1. Open **Demo Lab** and run the e-commerce, event registration, and HR offboarding scenarios.
2. Open a missed outcome on the overview and inspect its evidence and business impact.
3. Try verification before recovery: missing evidence keeps the incident open.
4. Add the labeled synthetic recovery evidence, then verify again.
5. Create an application and your own rule in **Rule Studio**, then send real JSON events through **Event Explorer** or the API.

See the [demo walkthrough](docs/demo-guide.md) for the full judging path.

## What works

- **Rule Studio:** guided, domain-independent “trigger → required outcomes within a deadline” rules, preview, and pause/resume.
- **Event ingestion:** validated batches, application-scoped event identity, idempotent retries, conflict detection, and persisted evidence.
- **Deterministic evaluation:** a background worker evaluates deadlines; app and correlation ID isolate each journey. All required outcome types must be present.
- **Investigation:** incidents carry business exposure, chronological evidence, observed facts, and unproven diagnostic hypotheses.
- **Safe recovery:** acknowledgement and verification are audited. Verification checks stored events; it never runs a real refund, access change, or other business action.
- **Three working demos:** paid orders without shipment, registrations without confirmation, and offboarding with a lingering session use the same engine.
- **Persistence and deployment:** SQLite, API-key protection, production startup checks, Docker packaging, and GitHub Actions.

## Engine semantics

A trigger event opens an obligation. Outcome events must share its application and correlation ID and occur at or after the trigger. Outcomes at the exact deadline count as on time; a missing outcome becomes overdue strictly after the deadline. Repeated delivery of the same event is safe. Conflicting reuse of an event ID is rejected atomically.

Each journey should use a unique correlation ID. Rules are immutable except for their enabled flag, so historical meaning cannot silently change. Pausing prevents new obligations; existing ones keep running. Creating or resuming a rule checks retained historical triggers, including those received while paused. Late evidence can restore an outcome but never erases an observed incident; explicit verification records closure. Exposure is summed per obligation and unit, so overlapping rules can count the same journey more than once. It is an estimate, not booked loss.

See [the API contract](docs/api-contract.md) and [architecture decisions](docs/architecture.md).

## Send your own events

Create an application in the UI, then copy its ID into your events. Create a rule whose trigger is `order.paid` and expected outcome is `order.shipped`.

```sh
curl http://127.0.0.1:5080/api/events \
  -H 'Content-Type: application/json' \
  -d '{"events":[{"id":"evt-unique-001","appId":"YOUR_APP_ID","correlationId":"order-unique-001","type":"order.paid","occurredAt":"REPLACE_WITH_CURRENT_UTC_ISO_TIMESTAMP","data":{"amount":2499}}]}'
```

With authentication enabled, add `-H "X-Api-Key: YOUR_API_KEY"`. Each batch accepts up to 100 events. Do not put personal customer details or credentials into event payloads.

## Docker

```sh
cp .env.example .env
# Set OUTCOMEGUARD_API_KEY in .env to a random value of at least 24 characters.
docker compose up --build
```

Open **http://127.0.0.1:5080** and enter that key. Data lives in the `outcome-data` volume. Compose enables synthetic demos for evaluation. Set `OUTCOMEGUARD_DEMO_ENABLED=false` to disable them. Production requires a random key of at least 24 characters. For remote use, terminate TLS at a trusted reverse proxy and set the app's public origin.

| Configuration | Purpose |
| --- | --- |
| `OutcomeGuard__ApiKey` | Shared workspace API key; required in Production |
| `OutcomeGuard__DemoEnabled` | Enable synthetic demo endpoints; defaults off outside Development |
| `OutcomeGuard__DatabasePath` | SQLite file path |
| `OutcomeGuard__PublicOrigin` | Optional exact HTTPS origin for browser requests behind a reverse proxy, such as `https://outcomeguard.example` |
| `ASPNETCORE_ENVIRONMENT` | `Development` for loopback demo, `Production` for protected operation |

The frontend keeps the entered key only in memory; refresh requires re-entry.

## Develop and test

```sh
dotnet test tests/OutcomeGuard.Tests/OutcomeGuard.Tests.csproj -c Release
```

Build and copy the frontend with the applicable run script above, then stop its server. In `frontend`, run `npx playwright install chromium` and `npm run test:e2e`. Browser tests start their own server and use an isolated database. For live frontend development, start the API on port 5080 in Development and run `npm run dev` in `frontend`. Vite proxies `/api` to the backend. CI runs the .NET tests, production build, browser workflows, and container smoke test.

## Scope and limitations

This is a **single-workspace, single-process MVP**, not a production multi-tenant service. It has a shared API key rather than individual user accounts, RBAC, or billing. There are no production connectors, outbound notifications, arbitrary rule expressions, or autonomous remediation. SQLite and bounded in-process evaluation are suited to small demonstrations, not high event throughput. Event payloads are producer assertions, not independent proof that a real external system changed. Retention, production backups, key rotation, and deployment operations remain follow-up work.

See [security and trust boundaries](docs/security.md) before connecting any real system.

## Challenge evidence

- [Codex Evidence Pack](docs/evidence.md): actual work, lifecycle artifacts, validation, model disclosure, and limitations.
- [Product brief and acceptance criteria](docs/product.md).
- [Submission details](docs/submission.md).
- [AGENTS.md](AGENTS.md): repository-specific guidance for future work.

All supplied demo data is synthetic. This project was created from scratch; it contains no employer source or customer records. The evidence pack does not claim unobserved model switching, research interviews, or a month of elapsed development.

MIT licensed.
