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
| **3 — Examinations** | Exam scheduling, question banks, on-screen exams, grade capture |
| **4 — Documents** | Versioned document store (MinIO), PDF/Word generation, student/staff portals |
| **5 — Finance + Sage 300** | Charges, fees, payments, refunds, ACH; Sage AR sync over HTTPS |
| **6 — Remaining modules + migration** | Timetable, Housing, Health, Sports, Chapel; data migration cutover |

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

## Data migration
Legacy source databases on the current SQL Server: `AEORIONSMS_UCC`, `ASM_UCC`,
`SMS_Apply`, `NCUChapel`. The reverse-engineered table and stored-procedure names give a
direct source→target mapping; migration is built as its own phase, running old and new in
parallel until cutover.
