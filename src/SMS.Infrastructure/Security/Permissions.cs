namespace SMS.Infrastructure.Security;

/// <summary>The permission catalogue and the default role → permission map (configurable in the DB
/// afterwards).</summary>
public static class Permissions
{
    public static readonly (string Key, string Desc)[] All =
    {
        ("students.read", "View students"), ("students.write", "Create/edit students"),
        ("admissions.read", "View admissions"), ("admissions.decide", "Record admission decisions"),
        ("registration.write", "Register/add-drop"),
        ("grades.read", "View grades"), ("grades.import", "Import grades"),
        ("grades.change.propose", "Propose a grade change"), ("grades.change.approve", "Approve a grade change"),
        ("exams.manage", "Manage exams"), ("exams.schedule", "Schedule exams/rooms"),
        ("finance.read", "View finance"), ("finance.post", "Post charges/payments"),
        ("documents.read", "View documents"), ("documents.write", "Upload documents"),
        ("cases.read", "View cases"), ("cases.create", "Raise cases"), ("cases.approve", "Approve cases"),
        ("reports.read", "View reports/KPIs"), ("assistant.use", "Use the assistant"),
        ("pdf.generate", "Generate PDFs"), ("admin.manage", "Manage roles & users"),
        ("audit.read", "View the audit log"),
        ("examiners.manage", "Manage external examiners"),
    };

    public static readonly Dictionary<string, string[]> RoleMap = new()
    {
        ["Admin"] = All.Select(p => p.Key).ToArray(),
        ["Registrar"] = new[] { "students.read","students.write","admissions.read","admissions.decide","registration.write",
            "grades.read","grades.change.approve","documents.read","documents.write","cases.read","cases.create","cases.approve",
            "reports.read","pdf.generate","exams.manage","audit.read","examiners.manage" },
        ["AdmissionsOfficer"] = new[] { "admissions.read","admissions.decide","students.read","documents.read","documents.write","cases.read","cases.create","pdf.generate" },
        ["ExaminationOfficer"] = new[] { "exams.manage","exams.schedule","grades.read","grades.change.propose","cases.read","cases.create","reports.read","pdf.generate","examiners.manage" },
        ["Lecturer"] = new[] { "grades.read","grades.import","grades.change.propose","cases.read","cases.create","assistant.use" },
        ["Dean"] = new[] { "grades.change.approve","cases.approve","reports.read","students.read","examiners.manage" },
        ["FinanceOfficer"] = new[] { "finance.read","finance.post","students.read","reports.read" },
        ["Student"] = new[] { "assistant.use" },
        ["Auditor"] = new[] { "students.read","admissions.read","grades.read","finance.read","documents.read","cases.read","reports.read","audit.read" },
    };
}
