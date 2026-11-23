using SMS.Domain.Common;
namespace SMS.Domain.Entities;

public class Campus : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public ICollection<School> Schools { get; set; } = new List<School>();
}
