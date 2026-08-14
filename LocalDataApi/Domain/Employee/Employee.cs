using LocalDataApi.Domain.Identity;

namespace LocalDataApi.Domain.Employee;

/// <summary>
/// 员工主档。
/// </summary>
public class Employee
{
    public long Id { get; set; }

    public string EmployeeNo { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Guid DepartmentId { get; set; }

    public long PositionId { get; set; }

    public long? UserIdentityId { get; set; }

    public int Status { get; set; }

    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedTime { get; set; }

    public Department Department { get; set; } = null!;

    public Position Position { get; set; } = null!;

    public User? User { get; set; }
}
