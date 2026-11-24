# Data migration — Aeorion → UCCSMS

Migrates the current Aeorion student-management data into the new system. Built as a **staged,
idempotent** process with a **reconciliation report** and a **dry run**, per the EMS brief
("controlled imports of Aeorion data: staged, checked, approved, then applied, with a full
reconciliation report").

## How it works
- **Pluggable source** (`ILegacySource`): read from the **live Aeorion SQL Server** (`SqlLegacySource`)
  or from **exported CSV files** (`CsvLegacySource`) — so migration can run offline from an export
  before direct DB access is arranged.
- **Pure mappers** (`Mappers`) translate legacy rows → new entities (status/level/term-type codes
  normalised); unit-tested, no DB needed.
- **Staged runner** (`MigrationRunner`) loads in dependency order — campuses → schools → programmes
  → terms → courses → students → enrolments → **fee types → financial transactions → leads →
  applications** — resolving relationships by natural key.
- **Idempotent**: re-running updates existing rows (matched by natural key) instead of duplicating.
- **Dry run** (default): computes the reconciliation report **without writing**. `--apply` writes.
- **Reconciliation report**: per entity, Source vs Inserted/Updated/Skipped and the list of errors
  (e.g. a student whose programme code isn't found), with an overall reconciled YES/NO.

## Running the tool
```bash
# 1) DRY RUN from a live Aeorion database (no changes written)
SMS_TARGET_DB="Server=…;Database=UCCSMS;User Id=…;Password=…;TrustServerCertificate=True" \
SMS_LEGACY_DB="Server=172.17.10.28;Database=AEORIONSMS_UCC;User Id=…;Password=…" \
dotnet run --project tools/SMS.Migration

# 2) Or from exported CSVs
SMS_TARGET_DB="…" SMS_LEGACY_CSV="/path/to/aeorion-export" dotnet run --project tools/SMS.Migration

# 3) When the report looks right, apply it
SMS_TARGET_DB="…" SMS_LEGACY_DB="…" dotnet run --project tools/SMS.Migration -- --apply
```
Exit code: `0` reconciled, `2` reconciled with skips/errors to review, `1` configuration error.

## Field mapping (legacy → new)
| New entity | Natural key | Legacy source (reverse-engineered; adjust to the live schema) |
|---|---|---|
| Campus | Code | `sms_ADM_VA_Campuses` |
| School | Code | `sms_BMS_VA_Colleges` (→ CampusCode) |
| Programme | Code | `sms_ADM_V_StudentProgramList` (→ SchoolCode, CreditsRequired, Level) |
| Term | Name+Year | `sms_ADM_D_Term` (Type, StartDate, EndDate, IsActive) |
| Course | Code | `Courses` (→ ProgrammeCode) |
| Student | StudentNumber | `sms_SI_V_Students` (→ ProgrammeCode, Status, Email, DOB) |
| Enrollment | Student+Course+Term | `sms_ACA_V_StudentCourses` (LetterGrade, CourseworkMark, FinalExamMark) |
| FeeType | Code | `sms_FIN_D_FeeTypes` (Name, DefaultAmount, DistributionCode) |
| FinancialTransaction | Reference, else Student+Type+Amount+PostedUtc | `sms_FIN_V_StudentLedger` (→ StudentNumber, FeeTypeCode, Type, Amount, Method, Description, PostedUtc, Term) |
| Lead | ExternalRef (→ ExternalCrmId) | `app_Leads` (Source, Status, ProgrammeCode, AssignedTo) |
| Application | ApplicationNumber | `app_Applications` (→ ProgrammeCode, IntakeTerm, Status, Decision, officers, dates, LeadExternalRef) |

The `SqlLegacySource` query strings are overridable — tune the SELECTs/column names to the actual
Aeorion schema (finance lives in `ASM_UCC`, admissions in `SMS_Apply`). Notes:
- **Ledger signs** are normalised to the new convention: charges positive, payments/refunds negative,
  adjustments keep the legacy sign — so a student balance is just the sum of `Amount`. Legacy payments
  stored as positive amounts are handled.
- **Financial transactions** are insert-once (never updated), matched by the legacy `Reference` when
  present (otherwise a student/type/amount/date composite), so re-runs never double-post the ledger.
  Historical rows are marked `AccountingStatus = NotApplicable` so they are not re-exported to the
  accounting system.
- **Applications** need a known programme **and** intake term, or they are skipped and reported; a
  `LeadExternalRef` links the application to its migrated lead when present.
- Migrated grades are tagged `GradeSource = "Migration"`.

## CSV export format (for `SMS_LEGACY_CSV`)
One file per entity, header row required (case-insensitive):
```
campuses.csv      code,name
schools.csv       code,name,campuscode
programmes.csv    code,name,schoolcode,creditsrequired,level
terms.csv         name,year,type,startdate,enddate,isactive
courses.csv       code,title,credits,programmecode
students.csv      studentnumber,firstname,lastname,email,dateofbirth,status,programmecode
enrollments.csv   studentnumber,coursecode,termname,termyear,lettergrade,courseworkmark,finalexammark
feetypes.csv      code,name,defaultamount,distributioncode
finance.csv       studentnumber,reference,feetypecode,type,amount,method,description,postedutc,termname,termyear
leads.csv         externalref,firstname,lastname,email,phone,source,status,programmecode,assignedto
applications.csv  applicationnumber,firstname,lastname,email,dateofbirth,programmecode,intaketermname,intaketermyear,status,decision,assignedofficer,decidedby,submittedutc,decidedutc,leadexternalref
```
The finance/admissions files are optional — include only what you export. `postedutc`,
`submittedutc` and `decidedutc` accept ISO-8601 (e.g. `2026-09-01T00:00:00Z`).

## Scope & extension
Covers the academic spine (campuses…enrolments), **finance history** (fee types + ledger) and
**admissions history** (leads + applications). The exam-domain records extend the same pattern: add a
`LegacyX` row + a mapper + a runner step. `ILegacySource` gives the finance/admissions readers
default (empty) implementations, so a source only implements what it actually exports. Everything
migrates through the EF Core model, so the audit log records the load.
