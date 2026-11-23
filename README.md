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

### Run without a database (demo/preview)

With no SQL Server connection string set, the API runs on an in-memory database and seeds demo
data — handy for a quick look:
```bash
cd src/SMS.Api && ConnectionStrings__Sms= ASPNETCORE_ENVIRONMENT=Development dotnet run   # http://localhost:5080
cd web && npm install && npm run dev                                                      # http://localhost:5173
```

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
| **9 — AI assistant layer** ✅ | `IAiAssistant` (pluggable, off by default). (a) Admissions: recommends a category + flags missing docs, a human approves. (b) Staff/student Q&A grounded in the policy library + the asker's own records. |
| **10 — Notifications & KPIs** ✅ | Inbox + rule-based alerts (overdue cases, exam clashes), executive KPI dashboard across modules |
| **Grade governance** ✅ | Grade-change register (reason codes, original preserved, second-person approval), withheld grades, pre-publication reconciliation |
| **External examiners** ✅ | Register, conflict declarations (gating appointment), 11-stage assignment/package workflow, reports + action plans, time-limited examiner portal |
| **10b — Remaining modules** | Scripts/custody, question papers, HR provider, assessment-operations register |
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

## Audit log

An **append-only** audit trail, captured automatically — the EMS brief's "every change records who,
what, when, before and after; history cannot be edited or deleted".

- **Automatic change auditing** — an EF Core `SaveChanges` interceptor records a Create/Update/Delete
  `AuditEvent` for every entity change across the system (with the acting user and the changed
  properties' before → after values; soft-deletes are recorded as deletes; enums shown by name).
  No per-service code needed.
- **Access logging** — views/downloads of restricted records are logged explicitly (e.g. transcript
  downloads) via `IAuditLogger`.
- **Immutable & read-only** — audit events are never updated or deleted, and the API exposes only a
  read endpoint (`GET /api/audit`, permission `audit.read`, held by Admin, Registrar, Auditor).
- A React **Audit Trail** page provides a filterable, paged view.

## Roles & access (RBAC)

## Roles & access (RBAC)

Role-based access control with a configurable role→permission model.

- **Permission catalogue** (e.g. `students.write`, `admissions.decide`, `grades.change.approve`,
  `cases.approve`, `finance.post`, `admin.manage`) and **seeded roles** (Admin, Registrar,
  AdmissionsOfficer, ExaminationOfficer, Lecturer, Dean, FinanceOfficer, Student, Auditor).
- **Where roles come from** — the identity provider's token role claims (Keycloak) and/or DB
  `UserRoleAssignment` records. A claims transformation resolves the user's effective permissions
  on each request and adds them as claims.
- **Enforcement** — sensitive endpoints carry `[HasPermission("…")]`; a dynamic policy provider +
  authorization handler check the permission claim. In **Development the dev identity is `Admin`**,
  so everything works out of the box; production is governed by real roles.
- **Management** — `GET /api/access/me` (your roles + permissions, drives UI gating),
  and admin-only role/assignment management under `/api/access/*`. A React **Access & Roles** page.
- Scope: a `CampusId` on assignments lays the groundwork for campus/school row-level scoping.

> Production note: map your Keycloak realm roles to these role names (or assign via `/api/access`).

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

## Notifications & dashboard (Phase 10)

The glue that surfaces work across modules — the EMS principle that alerts and reports are worked
out from records, not typed in.

- **Notifications inbox** (personal + broadcast), with read/unread.
- **Rule-based alert generation** — `POST /api/notifications/run-alerts` scans for overdue open
  cases and exam clashes in active terms and files inbox alerts. It **dedupes** against unread
  alerts for the same record, so it is safe to run on a schedule (a background job in production).
- **Executive KPI dashboard** — `GET /api/metrics/overview`: students by status, admissions funnel
  (leads, applications, enrolled, conversion rate), open/overdue cases, exams scheduled + clashes,
  and the finance ledger balance — all computed live from records.
- The React home page shows the KPI tiles and the notifications panel.

## Staff & student Q&A assistant (Phase 9b)

A grounded question-answering assistant for staff and students, reusing the pluggable
`IAiAssistant` (off by default).

- **Grounded only** — it answers from (1) a managed **knowledge/policy library** (seeded from the
  UCC policies already encoded: pass rules, GPA, re-sit/Incomplete, admission categories, deferral,
  special arrangements, case turnaround) and (2) the **asker's own record facts** (GPA, account
  balance, upcoming exams, open cases) when a student id is supplied. It never invents policy,
  grades, balances or decisions, and directs unknowns to the relevant office.
- **Works with AI on or off** — with AI enabled the model answers from the supplied context; with
  AI disabled a deterministic answer is assembled from the same context.
- **Audited** — every question and answer is logged (data protection).

API: `POST /api/assistant/ask` (question + optional studentId), `GET/POST /api/assistant/articles`
(manage the policy library). A React **Assistant** page provides the chat UI.

> Production note: RBAC must ensure a student can only request their own record facts.

## Grade governance

Controls on changing and publishing grades, from both policy documents:

- **Grade-change register** — a change captures the **original** letter/mark (never overwritten)
  and the proposed values with a **reason code** (clerical error, re-mark, appeal, late coursework,
  incomplete cleared, integrity outcome). The enrolment grade changes only on approval; after an
  error you raise a new request (a new version), never edit history.
- **Second-person approval** — the approver cannot be the proposer. On approval the change is
  applied under a traceable batch id (the same mechanism as imports/Moodle/exam posting).
- **Withheld grades** — a grade can be withheld pending an integrity case (linked by case id) and
  released later.
- **Pre-publication reconciliation** — `GET /api/gradegovernance/pre-publication/{termId}` flags
  ungraded enrolments, withheld grades and pending grade-change requests before a term's results
  are released.

API under `/api/gradegovernance/*`. A React **Grade Governance** page manages the register and
runs the pre-publication check.

## PDF generation

Official documents are generated from data already in the system (QuestPDF):

- **Transcript** — `GET /api/pdf/transcript/{studentId}`: student details, a table of courses
  (code, title, term, credits, grade) and the cumulative GPA from the grade engine.
- **Acceptance letter** — `GET /api/pdf/acceptance-letter/{applicationId}`: a templated offer
  letter naming the programme, intake term and admission category, with the application reference.

Linked from the Students list (Transcript) and the Admissions page (Letter, for accepted
applications). More templates (certificates, enrolment letters) plug into the same `PdfService`.

> **Licensing:** QuestPDF is used under its free **Community** licence (set in code). Institutions
> above QuestPDF's revenue threshold must obtain a QuestPDF **Professional** licence.

## External examiner management

Covers the external-examiner lifecycle from the EMS brief:

- **Register** of external examiners with institution, appointment and contract reference.
- **Conflict-of-interest declaration** — recorded per examiner; **appointment is blocked** until a
  declaration is on file, and blocked entirely if a conflict is declared.
- **11-stage assignment/package workflow** (Nominated → ConflictCheck → Appointed → ContractIssued
  → ContractSigned → PackagePreparation → PackageDispatched → UnderReview → ReportReceived →
  ActionPlan → Completed, plus Cancelled); stages only move forward.
- **Reports + action plans** — examiners submit a report (advancing the stage); Deans/HODs attach an
  action plan with an owner and corrective actions.
- **Time-limited examiner portal** — a token (default 30 days) lets an external examiner view their
  own assignments at `GET /api/examinerportal/{token}` without a staff account.

Management API under `/api/examiners/*` (permission `examiners.manage`: Registrar, Examination
Officer, Dean, Admin). A React **External Examiners** page drives it.

## Data migration
Legacy source databases on the current SQL Server: `AEORIONSMS_UCC`, `ASM_UCC`,
`SMS_Apply`, `NCUChapel`. The reverse-engineered table and stored-procedure names give a
direct source→target mapping; migration is built as its own phase, running old and new in
parallel until cutover.
