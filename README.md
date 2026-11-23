# Student Management System (web) — Phase 0 foundation

A modern, fast, web-based Student Management System to replace the legacy Aeorion
WinForms suite (SMS, Grade Import Utility, Timetable Generator). Self-hosted on your
institution's own servers and SQL Server, accessed securely over the internet.

This repository is the **Phase 0 foundation**: it compiles, has a valid database model
(EF Core migration included), a running API with auth/Swagger, and a working React UI.
It is the skeleton every later module plugs into — not the finished system.

## Architecture

```
Browser ──HTTPS──► nginx (reverse proxy, TLS)
                     ├─► React SPA (static bundle)
                     └─► /api ─► ASP.NET Core 8 API ─► SQL Server (your DB, EF Core)
                                          │
                                          ├─► Keycloak (OIDC SSO, shared with Moodle)
                                          ├─► MinIO (S3-compatible document storage)
                                          ├─► Moodle Web Services (enrolment + grade sync)   [later phase]
                                          └─► Sage 300 Web API over HTTPS (finance)           [later phase]
```

Everything runs on your servers via Docker Compose. nginx is the only
internet-facing entry point.

| Layer | Tech | Project |
|---|---|---|
| Frontend | React 18 + TypeScript + Vite | `web/` |
| API | ASP.NET Core 8 (controllers, JWT/OIDC, Swagger) | `src/SMS.Api` |
| Application | DTOs, use cases (grows per module) | `src/SMS.Application` |
| Domain | Entities + rules, from the reverse-engineered model | `src/SMS.Domain` |
| Infrastructure | EF Core + SQL Server, migrations, auditing, soft-delete | `src/SMS.Infrastructure` |
| Deploy | Docker Compose, Dockerfiles, nginx | `deploy/` |

## What's in Phase 0 (working today)

- **Domain model** drawn directly from the legacy databases: Campus, School, Programme,
  Term, Course, Student, Enrollment (with grades), StaffFaculty. Every record is audited
  (created/modified by+when) and **soft-deleted**, not hard-deleted.
- **API**: `GET /health`, Swagger UI at `/swagger`, `GET /api/students` (paged + search),
  `GET /api/students/{id}`, `GET /api/enrollments/by-term/{termId}`, and
  `POST /api/enrollments/undo-batch/{batchId}` — batch grade-import **undo**, a capability
  the old tool tracked internally but never exposed.
- **Auth** wired for Keycloak/OIDC (JWT bearer). Controllers are `[Authorize]`.
- **Frontend**: app shell + live Students page (paged, searchable, calls the API).
- **Deploy**: one `docker compose up` brings up DB, auth, storage, API, SPA and proxy.

## Lessons applied from the legacy apps (what we deliberately fixed)

| Legacy problem (found by reverse-engineering) | Fix here |
|---|---|
| Plaintext SQL credentials shipped in `*.exe.config` | Connection string only from env/secret file (`SMS_DB`); nothing in source |
| One shared `aeorionuser` across six databases | Per-service least-privilege DB users (set during deployment) |
| Sage 300 ERP called over plain HTTP | HTTPS required for all integrations |
| Unsigned, auto-updating ClickOnce | Containerized, versioned deploys behind your proxy |
| Audit limited to logins | Full create/modify audit + soft-delete on every entity |
| Grade import via 32-bit Jet/Excel, hand-typed named ranges | (Phase 1) ClosedXML + visual column mapping + CSV |
| Student "old ID / new ID" juggling | Single stable `StudentNumber` key |

## Run it

### Local development (two terminals)
```bash
# 1) API  (needs a SQL Server; see appsettings.Development.json or set SMS_DB)
cd src/SMS.Api
dotnet run    # serves http://localhost:5080  (Swagger at /swagger)
```
```bash
# 2) Web
cd web
npm install
npm run dev   # serves http://localhost:5173, proxies /api to the API
```

Create/update the database schema:
```bash
dotnet ef database update -p src/SMS.Infrastructure -s src/SMS.Api
```

### On-premises deployment
```bash
cd deploy
cp .env.example .env     # set real passwords and URLs
docker compose up -d --build
```
Then put your TLS certificate in `deploy/certs/` and enable the `443` server block in
`deploy/nginx.conf`. Configure a Keycloak realm `sms` and a client `sms-api`.

## Roadmap (modules map 1:1 to the legacy system)

| Phase | Scope |
|---|---|
| **0 — Foundation** | Solution, auth, core model, API + SPA, deployment |
| **1 — Core SIS** ✅ | Students CRUD, Grades + modern Excel/CSV import with mapping, preview & batch undo |
| **2 — Moodle** ✅ | Course/enrolment provisioning + grade sync + shared SSO |
| **3 — Examinations** ✅ | Exam scheduling, question banks, exam assembly, result capture, post-to-gradebook (undoable). On-screen exam delivery is a later sub-phase. |
| **4 — Documents** ✅ | Versioned document store on MinIO/S3 (filesystem in dev), upload/version/download, per-student + categories. Automated PDF/Word *generation* is a later sub-phase. |
| **5 — Finance + Sage 300** | Charges, fees, payments, refunds, ACH; Sage AR sync over HTTPS |
| **6a — Grade-scheme engine** ✅ | UCC Examination Policy scales (Bachelors/Masters/Occupational), special grades, pass rules, credit-weighted GPA — configurable & versioned |
| **6b — Admissions / CRM lifecycle** ✅ | Leads → application → review → decision (second-person) → offer response → student; built-in CRM + public web capture + pluggable `ICrmProvider` |
| **7 — Exam expansion** ✅ | Rooms/venues, capacity-checked room allocation, invigilator assignment, special arrangements, automatic clash detection |
| **8 — Case management** ✅ | 14 configurable case types, working-day due dates, second-person approval, overdue/due-soon, dashboard, append-only history |
| **9 — AI assistant layer** ✅ (admissions) | `IAiAssistant` (pluggable, off by default); AI recommends an admission category + flags missing docs, a human approves. Staff/student Q&A assistant is the next AI increment. |
| **10 — Governance & integrations** | Grade governance, external examiners, scripts/custody, HR provider, notifications/KPIs |
| **11 — Data migration + cutover** | Migrate from the current Aeorion databases |

## Moodle integration (Phase 2)

The SMS is the **system of record**; Moodle is kept in step with it.

- **Provision (SMS → Moodle)** — `POST /api/moodle/provision/{courseId}/{termId}` creates the
  Moodle course (if needed), ensures each registered student exists as a Moodle user, and
  enrols them. Moodle ids are stored back on `Student.MoodleUserId` / `Course.MoodleCourseId`.
- **Grade sync (Moodle → SMS)** — `POST /api/moodle/pull-grades/{courseId}/{termId}` pulls
  course grades and writes them to enrolments as **one undoable batch** (same `ImportBatchId`
  mechanism as the file import, so `undo-batch` reverses a Moodle sync too).
- **Connection check** — `GET /api/moodle/health`.
- **SSO** — Moodle users are created with `auth = oidc`; point Moodle's OIDC plugin at the
  same Keycloak realm as the API so one login spans both systems.

Config (never in source): `Moodle:BaseUrl` (https), `Moodle:Token` (a Moodle web-service
token), `Moodle:StudentRoleId`. Uses Moodle's REST API (`core_course_*`, `core_user_*`,
`enrol_manual_enrol_users`, `gradereport_user_get_grade_items`).

## Document management (Phase 4)

A versioned document store. Metadata and versions live in SQL Server; the bytes live in
**object storage** — MinIO/S3 in production, the local filesystem in dev/tests (no MinIO needed
to run locally). Pluggable via `Storage:Provider` (`FileSystem` | `S3`).

- `GET /api/documents?studentId=&category=` — list (per student and/or category).
- `POST /api/documents` (multipart) — upload a new document (title, category, optional student).
- `POST /api/documents/{id}/versions` (multipart) — add a new version (history is kept).
- `GET /api/documents/{id}/download?version=` — download a version (latest if omitted).
- `DELETE /api/documents/{id}` — soft-delete.

Each version records size + **SHA-256** for integrity. Categories: Transcript, AdmissionDocument,
IdDocument, Letter, Certificate, Receipt, Other. Config (secrets via env): `Storage:S3:{Endpoint,
Bucket,AccessKey,SecretKey}` — for on-prem MinIO, point at its endpoint with path-style addressing.

> Automated generation of PDFs/Word (transcripts, letters — the legacy iTextSharp/DocX feature)
> is scoped as a follow-on sub-phase.

## Development auth

Every API endpoint is `[Authorize]`. In **Development with no Keycloak configured**, the API
auto-authenticates requests as a dev user so the UI works out of the box. Production always uses
real OIDC/JWT bearer tokens from Keycloak.

## Student finance & accounting integration (Phase 5)

The SMS keeps its **own authoritative ledger** (charges, payments, refunds, adjustments → a
running per-student balance) and, optionally, mirrors each entry to an external accounting system.

**The accounting integration is provider-agnostic.** Finance logic depends only on
`IAccountingProvider` — never on a specific vendor:

- `None` (default) — internal ledger only; posts nowhere external.
- `Sage300` — posts AR documents to Sage 300's Web API **over HTTPS** (the legacy app used plain
  HTTP; this doesn't).
- **To add QuickBooks, Xero, or any other system later:** implement `IAccountingProvider`
  (one class), register it in `DependencyInjection` under its name, and set
  `Accounting:Provider`. **No change to the finance module.**

API: `GET /api/finance/accounting/health`, `GET/POST /api/finance/fee-types`,
`GET /api/finance/students/{id}/statement`, `POST .../charges`, `POST .../payments`.
Config (secrets via env): `Accounting:Provider`, `Accounting:Sage300:{BaseUrl,Company,Username,Password}`.

## Grading (UCC Examination Policy)

Grades follow the Examination Policy, as **configurable, versioned grade schemes** — not hard-coded.
Four scales are seeded (Bachelors, Masters, Occupational Associate, Occupational Diploma/Certificate),
each with percentage→letter bands, GPA quality points, and special grades (I, RE, DNS, W, WF, DEF, MED,
AM, EC). A programme's **level** selects its scheme by effective date.

- Pass rule (policy): coursework ≥ 50% AND end-of-semester ≥ 50% AND combined ≥ overall pass mark
  (50% undergraduate / Occupational, 60% Masters).
- GPA = Σ(quality points × credits) ÷ Σ(credits), excluding grades that don't count (e.g. `I`).
- `GET /api/grading/schemes`, `GET /api/grading/evaluate?programmeId=&coursework=&eosa=&final=`,
  `GET /api/grading/students/{id}/gpa`.

## Admissions & CRM (Phase 6b)

Covers the front of the student lifecycle per the UCC Admissions Policy, **as one module in the
platform** (no separate system):

- **Built-in CRM** — leads captured, qualified and converted. Your website posts leads directly to
  the public endpoint `POST /api/public/lead` (an in-house replacement for Mautic; an optional
  shared secret deters spam). No external marketing tool required.
- **Pluggable lead sync** — `ICrmProvider`: `BuiltIn` (default) or a generic **`Webhook`** that
  pushes leads to any external system (another CRM, an automation tool, a custom endpoint).
  Named vendor providers (Salesforce, HubSpot) can be added later without touching admissions logic.
- **Application lifecycle** — submit (automatic Offer of Admission), assign/review, document
  verification, **decision** with the policy's admission categories (Full Acceptance, Full-to-Core,
  Conditional Class 1/2, Mature Entrant, Provisional), **offer response** (accept/decline/defer),
  and **convert an accepted applicant into a Student** — joining admissions to registration.
- **Segregation of duties** — the officer who reviews an application cannot approve its decision
  (second-person approval), per the Admissions Policy and the EMS brief.

API under `/api/admissions/*` (leads, applications, decision, offer-response, convert-to-student)
and `POST /api/public/lead`. Config: `Crm:Provider`, `Crm:CaptureSecret`, `Crm:Webhook:Url`.

## AI assistant (Phase 9 — admissions)

AI automates the lead-to-admit paperwork the Admissions Policy describes (today staff retype
application data into spreadsheets), under a strict rule from both policies: **AI recommends, a
human decides.**

- **Pluggable & off by default** — `IAiAssistant`: `None` (default) or `OpenAiCompatible` (OpenAI,
  Azure OpenAI, or a self-hosted model gateway). Enabled only when `Ai:Enabled=true`
  (data-protection approval); the API key is an environment secret, never in source.
- **Advisory recommendation** — `POST /api/admissions/applications/{id}/ai-recommend` judges the
  applicant against the policy's admission categories (Full Acceptance, Full-to-Core, Conditional
  Class 1/2, Mature Entrant, Provisional) and flags missing documents. It is stored as an
  `AdmissionRecommendation` for audit and **never changes the decision**.
- **Graceful fallback** — with AI disabled, a deterministic rule-based recommendation is produced,
  so the feature works with or without a model.
- A human then approves through the existing **second-person** `/decision` workflow.

`GET /api/admissions/ai/health` reports the provider and whether it's configured.

## Exam scheduling (Phase 7)

Extends the Examinations module with the EMS brief's timetabling capabilities:

- **Rooms / venues** per campus with capacity; **room allocation** to exams (rejected if seats
  exceed capacity).
- **Invigilator assignment** (chief/assistant) to exams.
- **Special arrangements** per student (extra time, separate room, reader, scribe, assistive
  technology, rest breaks, large print) — approved, and **extra time extends that student's
  effective exam window** for clash checking.
- **Automatic clash detection** across a term's scheduled exams:
  student overlaps, double-booked rooms, double-booked invigilators, over-capacity sittings, and
  too many exams a day (configurable limit). Each clash is an Error or Warning with a description.

API under `/api/examscheduling/*` (rooms, allocate-room, invigilators, arrangements,
`clashes/{termId}`). A React **Exam Scheduling** page manages rooms and runs the clash check.

## Case management (Phase 8)

The staff-work backbone from the EMS brief: every query, appeal, grade amendment, irregularity,
remark, etc. is a tracked case.

- **14 configurable case types** seeded (Query, Grade Amendment, Add/Drop, Alternative Request,
  Resit Request, Clash Form, EE Recruitment/Contract, Irregularity, Coursework Breach, Appeal,
  Committee Action Item, Script Remark, Script Review, Change of Exam Location) — each with a
  working-day turnaround and whether it needs second-person approval.
- **Due dates in working days** (weekends skipped); **overdue** and **due-soon** are computed from
  the dates, never typed in.
- **Second-person approval** on sensitive types — the approver cannot be the raiser or assignee.
- **Append-only history** of every note, assignment, approval, resolution and cancellation.
- **Dashboard** (open / overdue / due-soon / pending approval, and counts by type) and list filters.

API under `/api/cases/*` (types, dashboard, list, create, notes, assign, approve, resolve, cancel,
link). A React **Cases** page with the dashboard, intake form and work list.

## Data migration
Legacy source databases on the current SQL Server: `AEORIONSMS_UCC`, `ASM_UCC`,
`SMS_Apply`, `NCUChapel`. The reverse-engineered table and stored-procedure names give a
direct source→target mapping; migration is built as its own phase, running old and new in
parallel until cutover.
