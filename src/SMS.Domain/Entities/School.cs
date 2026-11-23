using SMS.Domain.Common;
namespace SMS.Domain.Entities;

/// <summary>A college/faculty/school within a campus (legacy: sms_BMS_VA_Colleges / Departments).</summary>
public class School : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int CampusId { get; set; }
    public Campus Campus { get; set; } = default!;
    public ICollection<Programme> Programmes { get; set; } = new List<Programme>();
}
