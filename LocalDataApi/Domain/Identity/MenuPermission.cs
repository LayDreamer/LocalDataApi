namespace LocalDataApi.Domain.Identity;

/// <summary>
/// Associates a menu with an existing permission code. Permission evaluation remains unchanged.
/// </summary>
public class MenuPermission
{
    public Guid Id { get; set; }
    public Guid MenuId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; } = DateTime.Now;

    public Menu Menu { get; set; } = null!;
}
