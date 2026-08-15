namespace LocalDataApi.Dto;

/// <summary>
/// Base transfer model for a dynamic menu tree.
/// </summary>
public class MenuDto
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? Component { get; set; }
    public string? Icon { get; set; }
    public string Type { get; set; } = string.Empty;
    public int Sort { get; set; }
    public bool Status { get; set; }
    public IReadOnlyCollection<MenuPermissionDto> Permissions { get; set; } = Array.Empty<MenuPermissionDto>();
    public List<MenuDto> Children { get; set; } = new();
}

/// <summary>
/// Base transfer model for a menu permission binding.
/// </summary>
public class MenuPermissionDto
{
    public Guid Id { get; set; }
    public Guid MenuId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
}

/// <summary>Request for creating a management menu.</summary>
public class MenuCreateDto
{
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Component { get; set; }
    public string? Icon { get; set; }
    public string Type { get; set; } = "Menu";
    public int Sort { get; set; }
    public bool Status { get; set; } = true;
    /// <summary>菜单可见性绑定的权限码列表(为空则不绑定,仅 ADMIN 可见)。</summary>
    public List<string> Permissions { get; set; } = new();
}

/// <summary>Request for updating a management menu. ParentId and Id are immutable.</summary>
public class MenuUpdateDto
{
    public string? Name { get; set; }
    public string? Path { get; set; }
    public string? Component { get; set; }
    public string? Icon { get; set; }
    public int? Sort { get; set; }
    public bool? Status { get; set; }
    /// <summary>菜单可见性绑定的权限码列表(传入即整体覆盖,为空则清空绑定)。</summary>
    public List<string>? Permissions { get; set; }
}

/// <summary>Menu item available to the authenticated user for dynamic navigation.</summary>
public class CurrentMenuDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? Component { get; set; }
    public string? Icon { get; set; }
    public List<CurrentMenuDto> Children { get; set; } = new();
}
