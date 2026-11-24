# Codex Evidence Pack

## Provenance and scope

Project: **OutcomeGuard**, a domain-agnostic business-outcome monitoring MVP.

This work began in a Codex desktop session on **24 November 2026 (Asia/Calcutta)** from the owner's referenced product discussion and explicit build request. The source concept distinguishes business outcomes from technical uptime and includes commerce, event registration, and HR offboarding.

This document is an index of actual work and its limits, not a claim of a month of development. Commits are normal implementation milestones from this build session; no backdating or synthetic development history is intended. The workshop date and official deadline were not provided. The eight stage names below follow the exact challenge stages subsequently supplied by the owner: Understand, Plan, Build, Test, Review, Document, Ship, Automate.

## Observed Codex use

| Capability | Use in this session | Evidence boundary |
| --- | --- | --- |
| Context retrieval | Read the referenced product conversation and preserved its domain-independent scope | Product brief and API contract |
| Repository/account inspection | Checked available SDKs and GitHub authentication before selecting the run/publication path | Coordinator's execution history; final publication details recorded below |
| Parallel agents | Delegated backend/tests, frontend, and documentation as bounded workstreams | AGENTS.md ownership boundaries; session messages |
| Implementation tools | Wrote source, tests, configuration, and documentation in the shared workspace | Repository files and meaningful commits |
| Verification and review | Ran the .NET suite, browser workflows, dependency audits, and an extracted release smoke check | See the validation record and findings below |

## Model and reasoning-effort honesty

The initial build context identified Codex in the GPT-6 family. Its exact model and the inherited agents' variants were unavailable. The resumed checkpoint, verification, and release work exposed **gpt-6.2-sol** as the active model. Reasoning-effort settings were unavailable. There was no documented task-by-task model selection, cost comparison, or reasoning-effort experiment.

Engineering discipline here is visible in task separation, explicit contracts, deterministic runtime decisions, bounded code review, and checks that target temporal and ingestion failure modes. That is not a substitute for evidence of deliberate model selection. If the competition requires exact model/effort records, the owner should attach authentic session exports or screenshots that expose those settings. Do not reconstruct or invent them after the fact.

The product does **not** call a language model at runtime. Missing-event detection is deterministic. Diagnosis uses evidence-based facts and labeled possible explanations. No AI-generated cause is allowed to silently decide a violation or perform a business recovery action.

## Lifecycle evidence map

| Challenge stage | Work represented | Inspectable artifact | Claim limit |
| --- | --- | --- | --- |
| Understand | Framed silent business failures and four user jobs from the owner's brief | [Product brief](product.md) | Personas are hypotheses; no customer interviews were conducted |
| Plan | Bounded MVP, workflows, acceptance criteria, architecture, and exclusions | [Product brief](product.md), [API contract](api-contract.md), [Architecture and ADRs](architecture.md) | No validated market-size, commercial-readiness, or distributed scalability claim |
| Build | Implemented API, evaluator, persistence, frontend, and domain demos | `src/`, `frontend/`, commit history | A single workspace runs in one process |
| Test | Exercised meaningful rule, ingestion, isolation, recovery, and API behavior | `tests/`, actual check results below | Tests only establish their covered cases |
| Review | Checked domain semantics, access boundaries, data handling, and publication content | [Security boundaries](security.md), review log below | Not a formal security audit |
| Document | Wrote operator workflows, architecture decisions, setup, limitations, and authentic evidence | `README.md`, this pack, [Demo guide](demo-guide.md) | Documentation must match observed implementation, not planned features |
| Ship | Pushed public source with milestone commits and packaged a runnable release | Delivery record below | Release runs locally |
| Automate | Added CI and repeatable run/build paths | `.github/workflows/`, scripts, container files | A committed workflow is not proof of a completed hosted run |

## Validation record

Local verification used Windows, .NET SDK 10.0.404, Node 22.12.0, and Playwright Chromium. The release tag points to the verified application and browser test checkpoint.

| Check | Observed result |
| --- | --- |
| `dotnet test -c Release` | 32 passed, 0 failed, 0 skipped. Includes deadline boundaries, out-of-order events, application isolation, retry conflicts, persistence, authentication, and recovery. |
| `npm ci` and `npm run build` | TypeScript and Vite production build passed. npm reported zero known vulnerabilities. |
| `npm run test:e2e` | 4 Chromium workflows passed. Covers API key entry, all three demos plus recovery, custom application and rule creation plus ingestion, and mobile navigation. |
| `dotnet package list --include-transitive --vulnerable` | NuGet reported zero known vulnerable packages after updating the SQLite dependency. |
| Extracted release smoke check | Windows served the packaged dashboard. Running HR created two on-time obligations and one incident with `sessions.terminated` missing. |
| Container build and protected API | GitHub Actions built the production container and verified readiness, key enforcement, and authenticated state access at the release checkpoint. The local Docker engine was unavailable. |
| Public repository | GitHub reported public visibility. Application, tests, documentation, and CI were pushed in milestone commits. |
| Hosted CI | The [release checkpoint run](https://github.com/Shrey-Kashyap/outcomeguard/actions/runs/45373799037) passed the Linux build, backend tests, browser workflows, and container smoke check. |

## Debug and review log

- Solution-level testing initially found no projects. Added the app and test projects to the solution and reran the suite.
- NuGet flagged the bundled SQLite dependency during restore. Updated the Microsoft SQLite package and retested. The subsequent dependency scan was clear.
- Review found that diagnosis could include events beyond the evaluation clock and could still ask for verification after resolution. Restricted diagnosis to eligible events and recorded the verified closure in the incident.
- Review identified the cost of scanning all events for each trigger. Grouped events by journey and trigger type and capped the number of obligations.
- Browser tests initially timed out in the custom-rule workflow. The application field required a combobox selector, and the test needed to provide the required accountable owner. The corrected workflow passed through rule activation and event ingestion.

## Remaining evidence and product gaps

- No actual operator interviews, usability study, production incident history, or measured customer outcomes.
- No exact model metadata for the initial build or reasoning-effort metadata for either phase.
- No billing, customer tenancy, SSO/RBAC, managed integrations, outbound notifications, or automatic external remediation.
- No load-test, high-availability, disaster-recovery, compliance, or formal penetration-test claim.
- The release was smoke tested on Windows. macOS and Linux launchers use the same framework-dependent build. Linux CI runs the app and its browser workflows from source.

## Delivery record

- [Public repository and milestone history](https://github.com/Shrey-Kashyap/outcomeguard)
- [Runnable v0.1.0 release](https://github.com/Shrey-Kashyap/outcomeguard/releases/tag/v0.1.0), source checkpoint `ae17c35`
- [Published ZIP](https://github.com/Shrey-Kashyap/outcomeguard/releases/download/v0.1.0/OutcomeGuard-v0.1.0.zip), SHA-256 `48d6207665d1413b619847016b475c83cf7258bc550be189ff864727f4d508f6`
- [Setup instructions](../README.md#try-it-locally) and [submission worksheet](submission.md)
