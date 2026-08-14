namespace LocalDataApi.Domain.Identity;

/// <summary>
/// Dynamic navigation menu. Menus form an unbounded tree through <see cref="ParentId"/>.
/// </summary>
public class Menu
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? Component { get; set; }
    public string? Icon { get; set; }
    public string Type { get; set; } = "Menu";
    public int Sort { get; set; }
    public bool Status { get; set; } = true;
    public DateTime CreatedTime { get; set; } = DateTime.Now;
    public DateTime UpdatedTime { get; set; } = DateTime.Now;

    public Menu? Parent { get; set; }
    public ICollection<Menu> Children { get; set; } = new List<Menu>();
    public ICollection<MenuPermission> Permissions { get; set; } = new List<MenuPermission>();
}
