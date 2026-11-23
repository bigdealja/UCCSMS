using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>Staff/faculty member (legacy: StaffFacFirstName/LastName in ReportLogins).</summary>
public class StaffFaculty : BaseEntity
{
    public string StaffNumber { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
}
