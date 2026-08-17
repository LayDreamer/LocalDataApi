using LocalDataApi.Application.Erp;
using LocalDataApi.Application.Pmc.Contracts;
using LocalDataApi.Dto;
using LocalDataApi.Domain.Pmc;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

using LocalDataApi.Utils;

namespace LocalDataApi.Application.Pmc.Services;

/// <summary>
/// 工单销控用例实现。
/// </summary>
public class PmcWorkOrderService : PmcServiceBase, IPmcWorkOrderService
{
    private readonly ERPBaseService _erpBaseService;

    public PmcWorkOrderService(AppDbContext context, ERPBaseService erpBaseService)
        : base(context)
    {
        _erpBaseService = erpBaseService;
    }

    #region 工单销控表

    /// <summary>批量添加或更新工单销控表数据(存在则覆盖,不存在则新增)</summary>
    public async Task<List<WorkOrderSalesControl>> AddOrUpdateWorkOrderSalesControlList(List<WorkOrderSalesControl> list)
    {
        if (list == null || list.Count == 0)
        {
            throw new ArgumentException("工单销控表数据不能为空", nameof(list));
        }

        var result = new List<WorkOrderSalesControl>();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var itemNos = list
            .Where(x => !string.IsNullOrWhiteSpace(x.货号))
            .Select(x => x.货号!)
            .Distinct()
            .ToList();

        // 查询已存在的记录
        var existingItems = await _context.工单销控表
            .Where(x => itemNos.Contains(x.货号) && x.货号 != null)
            .ToListAsync();

        var existingDict = existingItems
            .Where(x => x.货号 != null)
            .ToDictionary(x => x.货号!);

        foreach (var item in list)
        {
            if (string.IsNullOrWhiteSpace(item.货号))
            {
                continue;
            }
            if (existingDict.TryGetValue(item.货号, out var existing))
            {
                ApplyClientRowVersion(existing, item.RowVersion);
                // 货号已存在:工单总数累加(先读原值,再覆盖)
                var newTotal = ParseDouble(existing.工单总数) + ParseDouble(item.工单总数);
                // 保留原创建时间;若原为空则用当前时间回填
                item.创建时间 = string.IsNullOrWhiteSpace(existing.创建时间) ? now : existing.创建时间;

                // 仅复制 item 中非空的字段,避免 SetValues 把未传字段清空
                var entry = _context.Entry(existing);
                foreach (var prop in entry.CurrentValues.Properties)
                {
                    var incoming = entry.Entity.GetType().GetProperty(prop.Name)!.GetValue(item);
                    if (incoming != null)
                    {
                        entry.CurrentValues[prop.Name] = incoming;
                    }
                }

                existing.工单总数 = newTotal.ToString();
                existing.在产数量 = (newTotal - ParseDouble(existing.已入库数)).ToString();
                result.Add(existing);
            }
            else
            {
                // 新增记录
                if (string.IsNullOrWhiteSpace(item.编号))
                {
                    item.编号 = Guid.NewGuid().ToString();
                }
                // 实时更新在产数量 = 工单总数 - 已入库数
                item.在产数量 = (ParseDouble(item.工单总数) - ParseDouble(item.已入库数)).ToString();
                item.创建时间 = now;
                await _context.工单销控表.AddAsync(item);
                result.Add(item);
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    /// <summary>根据货号查询工单销控表列表(分页)</summary>
    public async Task<PagedResult<WorkOrderSalesControl>> GetWorkOrderSalesControlList(
        PMCRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.工单销控表.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.编号)) query = query.Where(e => e.编号 == request.编号);

        if (!string.IsNullOrWhiteSpace(request.货号))
        {
            query = query.Where(e => e.货号 == request.货号);
        }
        if (!string.IsNullOrWhiteSpace(request.补充数据))
        {
            var keyword = request.补充数据.Trim();
            query = query.Where(e =>
                (e.货号 != null && e.货号.Contains(keyword))
                || (e.品名 != null && e.品名.Contains(keyword))
                || (e.规格 != null && e.规格.Contains(keyword)));
        }
        if (!string.IsNullOrWhiteSpace(request.排产编号))
        {
            var schedulingNo = request.排产编号.Trim();
            query = query.Where(main => _context.工单销控表明细.Any(detail =>
                detail.货号 == main.货号
                && detail.排产编号 != null
                && detail.排产编号.Contains(schedulingNo)));
        }

        return await query.OrderByDescending(e => e.创建时间).ThenBy(e => e.编号)
            .ToPagedResultAsync(request, cancellationToken);
    }

    /// <summary>批量删除工单销控表数据</summary>
    public async Task DeleteWorkOrderSalesControlList(List<string> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            return;
        }

        var items = await _context.工单销控表
            .Where(x => ids.Contains(x.编号))
            .ToListAsync();

        if (items.Count > 0)
        {
            _context.工单销控表.RemoveRange(items);
            await _context.SaveChangesAsync();
        }
    }

    #endregion

    #region 工单销控表明细

    /// <summary>批量添加或更新工单销控表明细数据(存在则覆盖,不存在则新增)</summary>
    public async Task<List<WorkOrderSalesControlDetail>> AddOrUpdateWorkOrderSalesControlDetailList(List<WorkOrderSalesControlDetail> list)
    {
        if (list == null || list.Count == 0)
        {
            throw new ArgumentException("工单销控表明细数据不能为空", nameof(list));
        }

        var result = new List<WorkOrderSalesControlDetail>();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 仅以"货号 + 分析单号"作为唯一判定键
        var itemNos = list
            .Where(x => !string.IsNullOrWhiteSpace(x.货号))
            .Select(x => x.货号!)
            .Distinct()
            .ToList();

        // 查询已存在的记录(按货号拉取,再在内存中按 货号+分析单号 判定)
        var existingItems = await _context.工单销控表明细
            .Where(x => itemNos.Contains(x.货号) && x.货号 != null)
            .ToListAsync();

        var existingDict = existingItems
            .Where(x => x.货号 != null)
            .ToDictionary(x => (x.货号!, x.分析单号));

        foreach (var item in list)
        {
            if (string.IsNullOrWhiteSpace(item.货号) || string.IsNullOrWhiteSpace(item.编号))
            {
                throw new ArgumentException("工单销控表明细中的编号不能为空", nameof(item));
            }

            var dictKey = (item.货号!, item.分析单号);
            if (existingDict.TryGetValue(dictKey, out var existing))
            {
                ApplyClientRowVersion(existing, item.RowVersion);
                // 更新已有记录:先对齐主键编号,再 SetValues
                item.编号 = existing.编号;
                // 保留原创建时间;若原为空则用当前时间回填
                item.创建时间 = string.IsNullOrWhiteSpace(existing.创建时间) ? now : existing.创建时间;
                _context.Entry(existing).CurrentValues.SetValues(item);
                // 实时更新待产数 = 生产数 - 入库数
                existing.待产数 = (ParseDouble(existing.生产数) - ParseDouble(existing.入库数)).ToString();
                result.Add(existing);
            }
            else
            {
                item.工单单号 = _erpBaseService.CalculateWorkOrder(item.编号);
                // 实时更新待产数 = 生产数 - 入库数
                item.待产数 = (ParseDouble(item.生产数) - ParseDouble(item.入库数)).ToString();
                item.创建时间 = now;
                await _context.工单销控表明细.AddAsync(item);
                result.Add(item);
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    /// <summary>根据货号查询工单销控表明细列表(分页)</summary>
    public async Task<PagedResult<WorkOrderSalesControlDetail>> GetWorkOrderSalesControlDetailList(
        PMCRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.工单销控表明细.AsNoTracking().AsQueryable();

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

    /// <summary>批量删除工单销控表明细数据</summary>
    public async Task DeleteWorkOrderSalesControlDetailList(List<string> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            throw new ArgumentException("删除列表不能为空", nameof(ids));
        }

        var items = await _context.工单销控表明细
            .Where(x => ids.Contains(x.编号))
            .ToListAsync();

        if (items.Count > 0)
        {
            _context.工单销控表明细.RemoveRange(items);
            await _context.SaveChangesAsync();
        }
    }

    #endregion
}
