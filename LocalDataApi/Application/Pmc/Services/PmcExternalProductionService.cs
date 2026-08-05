using LocalDataApi.Application.Erp;
using LocalDataApi.Application.Pmc.Contracts;
using LocalDataApi.Dto;
using LocalDataApi.Domain.Pmc;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

using LocalDataApi.Utils;

namespace LocalDataApi.Application.Pmc.Services;

/// <summary>
/// 外产管理用例实现(发运 / 领料 / 生产 / 入库)。
/// </summary>
public class PmcExternalProductionService : PmcServiceBase, IPmcExternalProductionService
{
    private readonly ERPBaseService _erpBaseService;

    public PmcExternalProductionService(AppDbContext context, ERPBaseService erpBaseService)
        : base(context)
    {
        _erpBaseService = erpBaseService;
    }

    #region 外产发运

    /// <summary>批量添加或更新外产发运数据(存在则覆盖,不存在则新增)</summary>
    public async Task<List<ExternalProductionShipment>> AddOrUpdateExternalProductionShipmentList(List<ExternalProductionShipment> list)
    {
        if (list == null || list.Count == 0)
        {
            throw new ArgumentException("外产发运数据不能为空", nameof(list));
        }

        var result = new List<ExternalProductionShipment>();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 提取需要匹配的分析单号
        var analysisNos = list
            .Where(x => !string.IsNullOrWhiteSpace(x.分析单号))
            .Select(x => x.分析单号!)
            .Distinct()
            .ToList();

        // 按 (分析单号, 货号) 一次性查出已存在记录,建立匹配索引
        var existingDict = (await _context.外产_发运
                .Where(x => x.分析单号 != null && analysisNos.Contains(x.分析单号))
                .ToListAsync())
            .Where(x => x.分析单号 != null && x.货号 != null)
            .ToDictionary(x => (x.分析单号!, x.货号!));

        foreach (var item in list)
        {
            // 货号或分析单号为空则跳过
            if (string.IsNullOrWhiteSpace(item.分析单号) || string.IsNullOrWhiteSpace(item.货号))
            {
                continue;
            }

            if (existingDict.TryGetValue((item.分析单号!, item.货号!), out var existing))
            {
                ApplyClientRowVersion(existing, item.RowVersion);
                // 货号和分析单号一致:更新已有数据
                item.编号 = existing.编号;
                // 保留原创建时间;若原为空则用当前时间回填
                item.创建时间 = string.IsNullOrWhiteSpace(existing.创建时间) ? now : existing.创建时间;
                _context.Entry(existing).CurrentValues.SetValues(item);
                result.Add(existing);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(item.编号))
                {
                    item.编号 = Guid.NewGuid().ToString();
                }
                
                item.创建时间 = now;
                await _context.外产_发运.AddAsync(item);
                result.Add(item);
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    /// <summary>根据货号查询外产发运列表(分页)</summary>
    public async Task<PagedResult<ExternalProductionShipment>> GetExternalProductionShipmentList(
        PMCRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.外产_发运.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.编号)) query = query.Where(e => e.编号 == request.编号);

        if (!string.IsNullOrWhiteSpace(request.货号))
        {
            query = query.Where(e => e.货号 == request.货号);
        }
        if (!string.IsNullOrWhiteSpace(request.排产编号)) query = query.Where(e => e.排产编号 == request.排产编号);
        if (!string.IsNullOrWhiteSpace(request.分析单号)) query = query.Where(e => e.分析单号 == request.分析单号);

        return await query.OrderByDescending(e => e.创建时间).ThenBy(e => e.编号)
            .ToPagedResultAsync(request, cancellationToken);
    }

    /// <summary>批量删除外产发运数据</summary>
    public async Task DeleteExternalProductionShipmentList(List<string> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            throw new ArgumentException("删除列表不能为空", nameof(ids));
        }

        var items = await _context.外产_发运
            .Where(x => ids.Contains(x.编号))
            .ToListAsync();

        if (items.Count > 0)
        {
            _context.外产_发运.RemoveRange(items);
            await _context.SaveChangesAsync();
        }
    }

    #endregion

    #region 外产领料

    /// <summary>批量添加或更新外产领料数据(存在则覆盖,不存在则新增)</summary>
    public async Task<List<ExternalProductionPickMaterial>> AddOrUpdateExternalProductionPickMaterialList(List<ExternalProductionPickMaterial> list)
    {
        if (list == null || list.Count == 0)
        {
            throw new ArgumentException("外产领料数据不能为空", nameof(list));
        }

        var result = new List<ExternalProductionPickMaterial>();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 收集用于查询已存在记录的编号(编号唯一,外产_领料 以编号关联)
        var keys = list
            .Where(x => !string.IsNullOrWhiteSpace(x.编号))
            .Select(x => x.编号!)
            .Distinct()
            .ToList();

        // 按编号批量拉取已存在记录(EF Core 可翻译)
        var existingItems = new List<ExternalProductionPickMaterial>();
        if (keys.Count > 0)
        {
            existingItems = await _context.外产_领料
                .Where(x => keys.Contains(x.编号))
                .ToListAsync();
        }

        var existingDict = existingItems
            .Where(x => x.编号 != null)
            .ToDictionary(x => x.编号!);

        foreach (var item in list)
        {
            if (string.IsNullOrWhiteSpace(item.编号))
            {
                throw new ArgumentException("外产领料数据中的编号不能为空", nameof(item));
            }
            // 编号存在:更新已存在记录的出库数量(需求量不在后端计算)
            if (existingDict.TryGetValue(item.编号!, out var existing))
            {
                ApplyClientRowVersion(existing, item.RowVersion);
                existing.出库数量 = item.出库数量;
                // 原创建时间为空则回填当前时间
                existing.创建时间 = string.IsNullOrWhiteSpace(existing.创建时间) ? now : existing.创建时间;

                result.Add(existing);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(item.货号))
                {
                    throw new ArgumentException("外产领料数据中的货号不能为空", nameof(item));
                }
                item.创建时间 = now;
                await _context.外产_领料.AddAsync(item);
                result.Add(item);
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    /// <summary>根据货号查询外产领料列表(分页)</summary>
    public async Task<PagedResult<ExternalProductionPickMaterial>> GetExternalProductionPickMaterialList(
        PMCRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.外产_领料.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.编号)) query = query.Where(e => e.编号 == request.编号);

        if (!string.IsNullOrWhiteSpace(request.货号))
        {
            query = query.Where(e => e.货号 == request.货号);
        }
        if (!string.IsNullOrWhiteSpace(request.分析单号)) query = query.Where(e => e.分析单号 == request.分析单号);

        return await query.OrderByDescending(e => e.创建时间).ThenBy(e => e.编号)
            .ToPagedResultAsync(request, cancellationToken);
    }

    /// <summary>批量删除外产领料数据</summary>
    public async Task DeleteExternalProductionPickMaterialList(List<string> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            throw new ArgumentException("删除列表不能为空", nameof(ids));
        }

        var items = await _context.外产_领料
            .Where(x => ids.Contains(x.编号))
            .ToListAsync();

        if (items.Count > 0)
        {
            _context.外产_领料.RemoveRange(items);
            await _context.SaveChangesAsync();
        }
    }

    #endregion

    #region 外产生产

    /// <summary>批量添加或更新外产生产数据(存在则覆盖,不存在则新增)</summary>
    public async Task<List<ExternalProduction>> AddOrUpdateExternalProductionList(List<ExternalProduction> list)
    {
        if (list == null || list.Count == 0)
        {
            throw new ArgumentException("外产生产数据不能为空", nameof(list));
        }

        var result = new List<ExternalProduction>();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 提取需要匹配的分析单号
        var analysisNos = list
            .Where(x => !string.IsNullOrWhiteSpace(x.分析单号))
            .Select(x => x.分析单号!)
            .Distinct()
            .ToList();

        // 按 (分析单号, 货号) 一次性查出已存在记录,建立匹配索引
        var existingDict = (await _context.外产_生产
                .Where(x => x.分析单号 != null && analysisNos.Contains(x.分析单号))
                .ToListAsync())
            .Where(x => x.分析单号 != null && x.货号 != null)
            .ToDictionary(x => (x.分析单号!, x.货号!));

        foreach (var item in list)
        {
            // 货号或分析单号为空则跳过
            if (string.IsNullOrWhiteSpace(item.分析单号) || string.IsNullOrWhiteSpace(item.货号))
            {
                continue;
            }

            if (existingDict.TryGetValue((item.分析单号!, item.货号!), out var existing))
            {
                ApplyClientRowVersion(existing, item.RowVersion);
                // 货号和分析单号一致:更新已有数据
                item.编号 = existing.编号;
                // 保留原创建时间;若原为空则用当前时间回填
                item.创建时间 = string.IsNullOrWhiteSpace(existing.创建时间) ? now : existing.创建时间;
                // 打印时间:前端传入"更新"表示触发打印动作,后端直接以当前时间赋值;否则保留原值
                if (string.Equals(item.打印时间, "更新", StringComparison.Ordinal))
                {
                    item.打印时间 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    item.打印时间 = existing.打印时间;
                }
                _context.Entry(existing).CurrentValues.SetValues(item);
                result.Add(existing);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(item.编号))
                {
                    item.编号 = Guid.NewGuid().ToString();
                }
                item.工单单号 = _erpBaseService.CalculateWorkOrder(item.编号);
                item.创建时间 = now;
                await _context.外产_生产.AddAsync(item);
                result.Add(item);
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    /// <summary>根据货号查询外产生产列表(分页)</summary>
    public async Task<PagedResult<ExternalProduction>> GetExternalProductionList(
        PMCRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.外产_生产.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.编号)) query = query.Where(e => e.编号 == request.编号);

        if (!string.IsNullOrWhiteSpace(request.货号))
        {
            query = query.Where(e => e.货号 == request.货号);
        }
        if (!string.IsNullOrWhiteSpace(request.排产编号)) query = query.Where(e => e.排产编号 == request.排产编号);
        if (!string.IsNullOrWhiteSpace(request.分析单号)) query = query.Where(e => e.分析单号 == request.分析单号);

        return await query.OrderByDescending(e => e.创建时间).ThenBy(e => e.编号)
            .ToPagedResultAsync(request, cancellationToken);
    }

    /// <summary>根据编号查询单条外产生产数据</summary>
    public async Task<ExternalProduction?> GetExternalProductionByNo(string 编号)
    {
        if (string.IsNullOrWhiteSpace(编号))
        {
            throw new ArgumentException("编号不能为空", nameof(编号));
        }

        return await _context.外产_生产
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.编号 == 编号);
    }

    /// <summary>批量删除外产生产数据</summary>
    public async Task DeleteExternalProductionList(List<string> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            throw new ArgumentException("删除列表不能为空", nameof(ids));
        }

        var items = await _context.外产_生产
            .Where(x => ids.Contains(x.编号))
            .ToListAsync();

        if (items.Count > 0)
        {
            _context.外产_生产.RemoveRange(items);
            await _context.SaveChangesAsync();
        }
    }

    #endregion

    #region 外产入库

    /// <summary>批量添加或更新外产入库数据(存在则覆盖,不存在则新增;联动更新工单销控)</summary>
    public async Task<List<ExternalProductionWarehousing>> AddOrUpdateExternalProductionWarehousingList(List<ExternalProductionWarehousing> list)
    {
        if (list == null || list.Count == 0)
        {
            throw new ArgumentException("外产入库数据不能为空", nameof(list));
        }
        var result = new List<ExternalProductionWarehousing>();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 收集用于查询已存在记录的编号(编号唯一,外产_入库 与 工单销控表明细 均以编号关联)
        var keys = list
            .Where(x => !string.IsNullOrWhiteSpace(x.编号))
            .Select(x => x.编号!)
            .Distinct()
            .ToList();

        // 按编号批量拉取已存在记录(EF Core 可翻译)
        var existingItems = new List<ExternalProductionWarehousing>();
        var workOrderDetailDict = new Dictionary<string, WorkOrderSalesControlDetail>();
        if (keys.Count > 0)
        {
            existingItems = await _context.外产_入库
                .Where(x => keys.Contains(x.编号))
                .ToListAsync();

            // 同步拉取对应的工单销控表明细(按编号匹配),用于同步入库数
            var allDetails = await _context.工单销控表明细
                .Where(x => keys.Contains(x.编号))
                .ToListAsync();

            workOrderDetailDict = allDetails
                .Where(x => x.编号 != null)
                .ToDictionary(x => x.编号!);
        }

        var existingDict = existingItems
            .Where(x => x.编号 != null)
            .ToDictionary(x => x.编号!);

        // 记录受影响的明细父级编号,用于主表汇总
        var affectedParentNos = new HashSet<string>();

        foreach (var item in list)
        {
            // 编号存在,且入库数量>0:更新已存在记录的入库数量
            if (!string.IsNullOrWhiteSpace(item.编号)
                && ParseDouble(item.入库数量) > 0
                && existingDict.TryGetValue(item.编号!, out var existing))
            {
                ApplyClientRowVersion(existing, item.RowVersion);
                // 原创建时间为空则回填当前时间
                existing.创建时间 = string.IsNullOrWhiteSpace(existing.创建时间) ? now : existing.创建时间;
                // 同步更新工单销控表明细的入库数(按编号匹配)
                if (workOrderDetailDict.TryGetValue(item.编号!, out var detail))
                {
                    detail.入库数 = item.入库数量;
                    // 实时更新待产数 = 生产数 - 入库数
                    detail.待产数 = (ParseDouble(detail.生产数) - ParseDouble(detail.入库数)).ToString();
                    if (!string.IsNullOrWhiteSpace(detail.父级编号))
                    {
                        affectedParentNos.Add(detail.父级编号!);
                    }
                }

                // 入库数量 >= 需求数量:订单已满足,直接删除该记录
                if (ParseDouble(item.入库数量) >= ParseDouble(existing.需求量))
                {
                    _context.外产_入库.Remove(existing);
                }
                else
                {
                    existing.入库数量 = item.入库数量;
                    result.Add(existing);
                }
            }
            else
            {
                // 新增时必须提供编号
                if (string.IsNullOrWhiteSpace(item.编号) || string.IsNullOrWhiteSpace(item.货号))
                {
                    throw new ArgumentException("外产入库数据中的编号或者货号不能为空", nameof(item));
                }

                // 同步更新工单销控表明细的入库数(按编号匹配)
                if (workOrderDetailDict.TryGetValue(item.编号!, out var detail))
                {
                    detail.入库数 = item.入库数量;
                    detail.待产数 = (ParseDouble(detail.生产数) - ParseDouble(detail.入库数)).ToString();
                    if (!string.IsNullOrWhiteSpace(detail.父级编号))
                    {
                        affectedParentNos.Add(detail.父级编号!);
                    }
                }

                item.工单单号 = _erpBaseService.CalculateWorkOrder(item.编号);
                item.创建时间 = now;
                await _context.外产_入库.AddAsync(item);
                result.Add(item);
            }
        }

        // 联动更新工单销控表主表:按父级编号汇总明细入库数 -> 已入库数;在产数量 = 工单总数 - 已入库数
        if (affectedParentNos.Count > 0)
        {
            var parentList = affectedParentNos.ToList();
            var mainRecords = await _context.工单销控表
                .Where(x => parentList.Contains(x.编号))
                .ToListAsync();

            var allParentDetails = await _context.工单销控表明细
                .Where(x => x.父级编号 != null && parentList.Contains(x.父级编号))
                .ToListAsync();
            var detailsByParent = allParentDetails
                .Where(x => !string.IsNullOrWhiteSpace(x.父级编号))
                .GroupBy(x => x.父级编号!)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var main in mainRecords)
            {
                if (string.IsNullOrWhiteSpace(main.编号)) continue;
                var totalInStock = detailsByParent.TryGetValue(main.编号, out var details)
                    ? details.Sum(d => ParseDouble(d.入库数))
                    : 0d;

                main.已入库数 = totalInStock.ToString();
                main.在产数量 = (ParseDouble(main.工单总数) - totalInStock).ToString();
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    /// <summary>根据货号查询外产入库列表(分页)</summary>
    public async Task<PagedResult<ExternalProductionWarehousing>> GetExternalProductionWarehousingList(
        PMCRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.外产_入库.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.编号)) query = query.Where(e => e.编号 == request.编号);

        if (!string.IsNullOrWhiteSpace(request.货号))
        {
            query = query.Where(e => e.货号 == request.货号);
        }
        if (!string.IsNullOrWhiteSpace(request.分析单号)) query = query.Where(e => e.分析单号 == request.分析单号);

        return await query.OrderByDescending(e => e.创建时间).ThenBy(e => e.编号)
            .ToPagedResultAsync(request, cancellationToken);
    }

    /// <summary>批量删除外产入库数据</summary>
    public async Task DeleteExternalProductionWarehousingList(List<string> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            throw new ArgumentException("删除列表不能为空", nameof(ids));
        }

        var items = await _context.外产_入库
            .Where(x => ids.Contains(x.编号))
            .ToListAsync();

        if (items.Count > 0)
        {
            _context.外产_入库.RemoveRange(items);
            await _context.SaveChangesAsync();
        }
    }

    #endregion
}
