using LocalDataApi.Application.Common;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Identity;

public interface IMenuService
{
    Task<List<MenuDto>> GetMenuTreeAsync(CancellationToken ct = default);
    Task<List<MenuDto>> GetMenusAsync(CancellationToken ct = default);
    Task<List<CurrentMenuDto>> GetCurrentUserMenusAsync(long userId, CancellationToken ct = default);
    Task<MenuDto> CreateMenuAsync(MenuCreateDto dto, string? operatorId = null, CancellationToken ct = default);
    Task<MenuDto> UpdateMenuAsync(Guid id, MenuUpdateDto dto, string? operatorId = null, CancellationToken ct = default);
    Task DeleteMenuAsync(Guid id, string? operatorId = null, CancellationToken ct = default);
}

public sealed class MenuService(AppDbContext context, IAuditLogService auditLog, AuthorizationService authorization) : IMenuService
{
    public async Task<List<MenuDto>> GetMenuTreeAsync(CancellationToken ct = default)
    {
        var nodes = await QueryMenus().Where(menu => menu.Status).ToListAsync(ct);
        var map = nodes.ToDictionary(menu => menu.Id);
        var roots = new List<MenuDto>();
        foreach (var node in nodes)
        {
            if (node.ParentId.HasValue && map.TryGetValue(node.ParentId.Value, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }
        return roots;
    }

    public Task<List<MenuDto>> GetMenusAsync(CancellationToken ct = default) => QueryMenus().ToListAsync(ct);

    public async Task<List<CurrentMenuDto>> GetCurrentUserMenusAsync(long userId, CancellationToken ct = default)
    {
        if (userId <= 0)
            throw new ValidationException("当前用户不能为空");

        var (roles, permissions) = await authorization.GetUserRolesAndPermissionsAsync(userId, ct);
        var activeMenus = await context.Menus.AsNoTracking()
            .Where(menu => menu.Status)
            .OrderBy(menu => menu.Sort).ThenBy(menu => menu.Name)
            .Select(menu => new Menu { Id = menu.Id, ParentId = menu.ParentId, Name = menu.Name, Path = menu.Path,
                Component = menu.Component, Icon = menu.Icon, Sort = menu.Sort })
            .ToListAsync(ct);

        // ADMIN is the existing system-administrator role seeded by the RBAC module.
        var visibleIds = roles.Contains("ADMIN", StringComparer.OrdinalIgnoreCase)
            ? activeMenus.Select(menu => menu.Id).ToHashSet()
            : (await context.MenuPermissions.AsNoTracking()
                .Where(binding => permissions.Contains(binding.PermissionCode))
                .Select(binding => binding.MenuId)
                .Distinct().ToListAsync(ct)).ToHashSet();

        // A permitted child needs all existing active ancestors so the client receives a valid tree.
        var byId = activeMenus.ToDictionary(menu => menu.Id);
        foreach (var menuId in visibleIds.ToList())
        {
            var parentId = byId.TryGetValue(menuId, out var menu) ? menu.ParentId : null;
            var visitedParents = new HashSet<Guid>();
            while (parentId.HasValue && visitedParents.Add(parentId.Value) && byId.TryGetValue(parentId.Value, out var parent))
            {
                visibleIds.Add(parent.Id);
                parentId = parent.ParentId;
            }
        }

        var nodes = activeMenus.Where(menu => visibleIds.Contains(menu.Id))
            .Select(menu => new CurrentMenuDto { Id = menu.Id, Name = menu.Name, Path = menu.Path,
                Component = menu.Component, Icon = menu.Icon }).ToList();
        var nodeById = nodes.ToDictionary(menu => menu.Id);
        var roots = new List<CurrentMenuDto>();
        foreach (var menu in activeMenus.Where(menu => visibleIds.Contains(menu.Id)))
        {
            var node = nodeById[menu.Id];
            if (menu.ParentId.HasValue && nodeById.TryGetValue(menu.ParentId.Value, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }
        return roots;
    }

    public async Task<MenuDto> CreateMenuAsync(MenuCreateDto dto, string? operatorId = null, CancellationToken ct = default)
    {
        ValidateRequired(dto.Name, dto.Path);
        await ValidateParentAsync(dto.ParentId, ct);
        var now = DateTime.Now;
        var menu = new Menu
        {
            Id = Guid.NewGuid(), ParentId = dto.ParentId, Name = dto.Name.Trim(), Path = dto.Path.Trim(),
            Component = TrimOrNull(dto.Component), Icon = TrimOrNull(dto.Icon),
            Type = string.IsNullOrWhiteSpace(dto.Type) ? "Menu" : dto.Type.Trim(), Sort = dto.Sort,
            Status = dto.Status, CreatedTime = now, UpdatedTime = now
        };
        context.Menus.Add(menu);
        await context.SaveChangesAsync(ct);
        await TryAuditAsync(operatorId, "CreateMenu", menu, ct);
        return ToDto(menu);
    }

    public async Task<MenuDto> UpdateMenuAsync(Guid id, MenuUpdateDto dto, string? operatorId = null, CancellationToken ct = default)
    {
        var menu = await context.Menus.FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("菜单不存在");
        if (dto.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationException("菜单名称不能为空");
            menu.Name = dto.Name.Trim();
        }
        if (dto.Path is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Path)) throw new ValidationException("菜单路径不能为空");
            menu.Path = dto.Path.Trim();
        }
        if (dto.Component is not null) menu.Component = TrimOrNull(dto.Component);
        if (dto.Icon is not null) menu.Icon = TrimOrNull(dto.Icon);
        if (dto.Sort.HasValue) menu.Sort = dto.Sort.Value;
        if (dto.Status.HasValue) menu.Status = dto.Status.Value;
        menu.UpdatedTime = DateTime.Now;
        await context.SaveChangesAsync(ct);
        await TryAuditAsync(operatorId, "UpdateMenu", menu, ct);
        return ToDto(menu);
    }

    public async Task DeleteMenuAsync(Guid id, string? operatorId = null, CancellationToken ct = default)
    {
        var menu = await context.Menus.FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("菜单不存在");
        if (await context.Menus.AsNoTracking().AnyAsync(item => item.ParentId == id, ct))
            throw new ConflictException("该菜单存在子菜单，不能删除");

        var bindings = await context.MenuPermissions.Where(item => item.MenuId == id).ToListAsync(ct);
        if (bindings.Count > 0) context.MenuPermissions.RemoveRange(bindings);
        menu.Status = false;
        menu.UpdatedTime = DateTime.Now;
        await context.SaveChangesAsync(ct);
        await TryAuditAsync(operatorId, "DeleteMenu", menu, ct);
    }

    private IQueryable<MenuDto> QueryMenus() => context.Menus.AsNoTracking()
        .OrderBy(menu => menu.Sort).ThenBy(menu => menu.Name)
        .Select(menu => new MenuDto { Id = menu.Id, ParentId = menu.ParentId, Name = menu.Name, Path = menu.Path,
            Component = menu.Component, Icon = menu.Icon, Type = menu.Type, Sort = menu.Sort, Status = menu.Status });

    private async Task ValidateParentAsync(Guid? parentId, CancellationToken ct)
    {
        if (parentId.HasValue && !await context.Menus.AsNoTracking().AnyAsync(menu => menu.Id == parentId && menu.Status, ct))
            throw new ValidationException("父级菜单不存在或已停用");
    }

    private static void ValidateRequired(string? name, string? path)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ValidationException("菜单名称不能为空");
        if (string.IsNullOrWhiteSpace(path)) throw new ValidationException("菜单路径不能为空");
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static MenuDto ToDto(Menu menu) => new() { Id = menu.Id, ParentId = menu.ParentId, Name = menu.Name, Path = menu.Path, Component = menu.Component, Icon = menu.Icon, Type = menu.Type, Sort = menu.Sort, Status = menu.Status };
    private async Task TryAuditAsync(string? operatorId, string action, Menu menu, CancellationToken ct)
    {
        try { await auditLog.LogAsync(operatorId, action, "Menu", menu.Id.ToString(), new { menu.Name, menu.Path, menu.Status }, ct); } catch { }
    }
}
