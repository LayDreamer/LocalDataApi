using LocalDataApi.Application.Common;
using LocalDataApi.Application.Platform;
using LocalDataApi.Application.Pmc.Contracts;
using LocalDataApi.Dto;
using LocalDataApi.Domain.Pmc;
using LocalDataApi.Infrastructure.Data;
using LocalDataApi.Utils;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace LocalDataApi.Application.Pmc.Services;

/// <summary>
/// 交期评审用例实现。
/// </summary>
public class PmcDeliveryReviewService : PmcServiceBase, IPmcDeliveryReviewService
{
    private readonly INumberRuleService _numberRuleService;

    public PmcDeliveryReviewService(AppDbContext context, INumberRuleService numberRuleService) : base(context)
    {
        _numberRuleService = numberRuleService;
    }

    /// <summary>获取外销合同客户产品列表(排除已评审订单,支持生产类型过滤)</summary>
    public async Task<IReadOnlyList<PMCUserProductInfo>> GetPMCUserProductInfoList(
        PMCRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.外销合同客户产品
           .AsNoTracking()
           .AsQueryable();

        // 只获取最近一年的数据
        var oneYearAgo = DateTime.Now.AddYears(-1);
        var oneYearAgoStr = oneYearAgo.ToString("yyyy-MM-dd");

        // 使用字符串比较来过滤日期
        query = query.Where(e => string.Compare(e.创建时间, oneYearAgoStr) >= 0);

        // 必须在分页前排除已评审订单,否则页面条数和 Total 都会不准确。
        query = query.Where(product => !_context.外产_订单.Any(order =>
            order.状态 == "评审通过"
            && order.排产编号 == product.合同号 + "-" + product.序号));

        query = query.Where(product =>
            _context.生产类型修改.Any(overrideItem =>
                overrideItem.合同号 == product.合同号
                && overrideItem.排产编号 == product.合同号 + "-" + product.序号
                && overrideItem.货号 == product.货号
                && (overrideItem.生产类型 == "普通订单" || overrideItem.生产类型 == "样品"))
            || (!_context.生产类型修改.Any(overrideItem =>
                    overrideItem.合同号 == product.合同号
                    && overrideItem.排产编号 == product.合同号 + "-" + product.序号
                    && overrideItem.货号 == product.货号)
                && _context.外销合同基本信息.Any(contract =>
                    contract.合同号 == product.合同号
                    && (contract.生产类型 == "普通订单" || contract.生产类型 == "样品"))));

        if (!string.IsNullOrEmpty(request.合同号)) query = query.Where(e => e.合同号 == request.合同号);
        if (!string.IsNullOrEmpty(request.货号)) query = query.Where(e => e.货号 == request.货号);
        // 根据生产类型过滤:覆盖值优先,未覆盖时取外销合同基本信息中的生产类型
        if (!string.IsNullOrEmpty(request.生产类型))
        {
            query = query.Where(product =>
                _context.生产类型修改.Any(overrideItem =>
                    overrideItem.合同号 == product.合同号
                    && overrideItem.排产编号 == product.合同号 + "-" + product.序号
                    && overrideItem.货号 == product.货号
                    && overrideItem.生产类型 == request.生产类型)
                || (!_context.生产类型修改.Any(overrideItem =>
                        overrideItem.合同号 == product.合同号
                        && overrideItem.排产编号 == product.合同号 + "-" + product.序号
                        && overrideItem.货号 == product.货号)
                    && _context.外销合同基本信息.Any(contract =>
                        contract.合同号 == product.合同号
                        && contract.生产类型 == request.生产类型)));
        }

        var data = await query
            .OrderByDescending(e => e.创建时间)
            .ThenBy(e => e.编号)
            .ToPageItemsAsync(request, cancellationToken);
        return data;
    }

    /// <summary>转换交期评审列表(分页)</summary>
    public async Task<PagedResult<PMCDeliveryReview>> ConvertToPMCDeliveryReviewList(
        PMCRequestDto request, CancellationToken cancellationToken = default)
    {
        var oneYearAgoStr = DateTime.Now.AddYears(-1).ToString("yyyy-MM-dd");
        var countQuery = _context.外销合同客户产品.AsNoTracking()
            .Where(e => string.Compare(e.创建时间, oneYearAgoStr) >= 0)
            .Where(product => !_context.外产_订单.Any(order =>
                order.状态 == "评审通过"
                && order.排产编号 == product.合同号 + "-" + product.序号))
            .Where(product =>
                _context.生产类型修改.Any(overrideItem =>
                    overrideItem.合同号 == product.合同号
                    && overrideItem.排产编号 == product.合同号 + "-" + product.序号
                    && overrideItem.货号 == product.货号
                    && (overrideItem.生产类型 == "普通订单" || overrideItem.生产类型 == "样品"))
                || (!_context.生产类型修改.Any(overrideItem =>
                        overrideItem.合同号 == product.合同号
                        && overrideItem.排产编号 == product.合同号 + "-" + product.序号
                        && overrideItem.货号 == product.货号)
                    && _context.外销合同基本信息.Any(contract =>
                        contract.合同号 == product.合同号
                        && (contract.生产类型 == "普通订单" || contract.生产类型 == "样品"))));
        if (!string.IsNullOrEmpty(request.合同号)) countQuery = countQuery.Where(e => e.合同号 == request.合同号);
        if (!string.IsNullOrEmpty(request.货号)) countQuery = countQuery.Where(e => e.货号 == request.货号);
        // 根据生产类型过滤:覆盖值优先,未覆盖时取外销合同基本信息中的生产类型
        if (!string.IsNullOrEmpty(request.生产类型))
        {
            countQuery = countQuery.Where(product =>
                _context.生产类型修改.Any(overrideItem =>
                    overrideItem.合同号 == product.合同号
                    && overrideItem.排产编号 == product.合同号 + "-" + product.序号
                    && overrideItem.货号 == product.货号
                    && overrideItem.生产类型 == request.生产类型)
                || (!_context.生产类型修改.Any(overrideItem =>
                        overrideItem.合同号 == product.合同号
                        && overrideItem.排产编号 == product.合同号 + "-" + product.序号
                        && overrideItem.货号 == product.货号)
                    && _context.外销合同基本信息.Any(contract =>
                        contract.合同号 == product.合同号
                        && contract.生产类型 == request.生产类型)));
        }

        var total = await countQuery.CountAsync(cancellationToken);
        var userProductInfos = await GetPMCUserProductInfoList(request, cancellationToken);
        var items = await ConvertToPMCDeliveryReviewList(userProductInfos.ToList());
        return new PagedResult<PMCDeliveryReview>
        {
            Items = items,
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    /// <summary>根据PMC客户产品列表转换为交期评审信息</summary>
    public async Task<List<PMCDeliveryReview>> ConvertToPMCDeliveryReviewList(List<PMCUserProductInfo> userProductInfos)
    {
        List<PMCDeliveryReview> data = new List<PMCDeliveryReview>(userProductInfos.Count);
        if (userProductInfos == null || userProductInfos.Count == 0)
        {
            return data;
        }

        // 提取查询需要的货号列表
        var itemNos = userProductInfos.Select(e => e.货号).Where(e => !string.IsNullOrEmpty(e)).Distinct().ToList();
        var itemNoSet = itemNos.ToHashSet(StringComparer.Ordinal);

        // 根据货号查询外销合同产品表中的排产用户:取实际完成日期有值且最近的第一个,构建 货号 -> 排产用户 字典
        var schedulingUserDict = new Dictionary<string, string>();
        if (itemNos.Count > 0)
        {
            var productUsers = await _context.外销合同产品
                .AsNoTracking()
                .Where(e => !string.IsNullOrEmpty(e.货号)
                    && !string.IsNullOrEmpty(e.排产用户)
                    && !string.IsNullOrEmpty(e.实际完成日期))
                .WhereInBatchesAsync(itemNos, e => e.货号, e => new { e.货号, e.排产用户, e.实际完成日期 });

            // 每个货号取实际完成日期最近的一条对应的排产用户(日期为字符串,按序数比较降序)
            schedulingUserDict = productUsers
                .GroupBy(x => x.货号!)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.实际完成日期, StringComparer.Ordinal).First().排产用户!);
        }

        // 提前查询货号对应的合同号在外销合同基本信息中的生产类型,构建 合同号 -> 生产类型 字典
        var contractNos = userProductInfos.Select(e => e.合同号).Where(e => !string.IsNullOrEmpty(e)).Distinct().ToList();
        var productionTypeDict = new Dictionary<string, string>();
        if (contractNos.Count > 0)
        {
            var contractInfos = await _context.外销合同基本信息
                .AsNoTracking()
                .Where(e => !string.IsNullOrEmpty(e.合同号))
                .WhereInBatchesAsync(contractNos, e => e.合同号, e => new { e.合同号, e.生产类型 });

            foreach (var info in contractInfos)
            {
                if (!string.IsNullOrEmpty(info.合同号))
                {
                    productionTypeDict[info.合同号] = info.生产类型 ?? "";
                }
            }
        }

        // 提前查询生产类型修改表(交期评审生产类型手动覆盖),构建 (合同号,排产编号,货号) -> 生产类型 字典
        var overrideDict = new Dictionary<(string, string, string), (string Type, byte[]? RowVersion)>();
        var overrideContracts = userProductInfos.Select(e => e.合同号).Where(e => !string.IsNullOrEmpty(e)).Distinct().ToList();
        var overrideSchedulingNos = userProductInfos.Select(e => $"{e.合同号}-{e.序号}").Where(e => !string.IsNullOrEmpty(e)).Distinct().ToList();
        var overrideItemNos = userProductInfos.Select(e => e.货号).Where(e => !string.IsNullOrEmpty(e)).Distinct().ToList();
        if (overrideContracts.Count > 0 && overrideSchedulingNos.Count > 0 && overrideItemNos.Count > 0)
        {
            var overrides = await _context.生产类型修改
                .AsNoTracking()
                .WhereInBatchesAsync(
                    overrideContracts, e => e.合同号,
                    overrideItemNos, e => e.货号,
                    e => new { e.合同号, e.排产编号, e.货号, e.生产类型, e.RowVersion });

            foreach (var o in overrides)
            {
                if (!string.IsNullOrEmpty(o.合同号) && !string.IsNullOrEmpty(o.排产编号) && !string.IsNullOrEmpty(o.货号))
                {
                    overrideDict[(o.合同号!, o.排产编号!, o.货号!)] = (o.生产类型 ?? "", o.RowVersion);
                }
            }
        }

        var sourceInfoDict = new Dictionary<string, string>();
        if (itemNos.Count > 0)
        {
            var sourceInfo = await _context.产品资料
                .AsNoTracking()
                .WhereInBatchesAsync(itemNos, e => e.货号, e => new { e.货号, e.制造方式 });

            foreach (var info in sourceInfo)
            {
                if (!string.IsNullOrEmpty(info.货号) && itemNoSet.Contains(info.货号))
                {
                    sourceInfoDict[info.货号] = info.制造方式 ?? "";
                }
            }
        }

        // ===== 在内存中处理数据转换 =====
        foreach (var item in userProductInfos)
        {
            string schedulingNumber = $"{item.合同号}-{item.序号}";
            string source = sourceInfoDict.GetValueOrDefault(item.货号) ?? "";
            var overrideKey = (item.合同号 ?? "", schedulingNumber, item.货号 ?? "");
            var hasOverride = overrideDict.TryGetValue(overrideKey, out var overrideValue);

            // 计算线圈货号
            string coilNumber = string.Empty;
            if (!string.IsNullOrEmpty(item.货号))
            {
                // 提取括号内的内容
                int startIndex = item.货号.IndexOf('(');
                int endIndex = item.货号.IndexOf(')');
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    coilNumber = item.货号.Substring(startIndex + 1, endIndex - startIndex - 1);
                }
            }
            PMCDeliveryReview review = new PMCDeliveryReview
            {
                编号 = item.编号,
                用户编号 = item.用户编号,
                用户铭 = item.用户铭,
                修改状态 = item.修改状态,
                锁定用户 = item.锁定用户,
                审核过程 = item.审核过程,
                打印 = item.打印,
                合同号 = item.合同号,
                货号 = item.货号,
                中文品名 = item.中文品名,
                中文规格 = item.中文规格,
                创建时间 = item.创建时间,
                电压 = item.电压,
                排产编号 = schedulingNumber,
                交货日期 = item.货好日期,
                数量 = item.数量,
                特殊要求 = item.备注,
                线圈货号 = coilNumber,
                来源 = source,
                // 默认取外销合同基本信息的生产类型;若生产类型修改表存在 (合同号,排产编号,货号) 一致的记录,则以其覆盖
                生产类型 = hasOverride
                    ? overrideValue.Type
                    : productionTypeDict.GetValueOrDefault(item.合同号) ?? "",
                ProductionTypeOverrideRowVersion = hasOverride ? overrideValue.RowVersion : null,
                排产用户 = schedulingUserDict.GetValueOrDefault(item.货号) ?? "",
                状态 = "待评审",
            };
            data.Add(review);
        }
        return data;
    }

    /// <summary>新增或更新交期评审记录(状态为"评审驳回"时回写驳回原因)</summary>
    public async Task<PMCDeliveryReview> AddPMCDeliveryReview(PMCDeliveryReview deliveryReview)
    {
        if (deliveryReview == null)
        {
            throw new ArgumentNullException(nameof(deliveryReview), "产品信息不能为空");
        }

        // 可根据业务需求增加字段非空校验,例如:
        if (string.IsNullOrWhiteSpace(deliveryReview.合同号))
        {
            throw new ArgumentException("合同号不能为空");
        }
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        // 根据编号查询是否已存在
        var existing = await _context.外产_订单
            .FirstOrDefaultAsync(x => x.编号 == deliveryReview.编号);
        if (existing != null)
        {
            ApplyClientRowVersion(existing, deliveryReview.RowVersion);
            // 更新现有实体:将传入实体的所有属性值复制到现有实体
            // 保留原创建时间;若原为空则用当前时间回填
            deliveryReview.创建时间 = string.IsNullOrWhiteSpace(existing.创建时间) ? now : existing.创建时间;
            _context.Entry(existing).CurrentValues.SetValues(deliveryReview);
        }
        else
        {
            // 新增: 统一编码中心生成评审单号(不再使用 Guid)
            deliveryReview.编号 = await _numberRuleService.GetNextCodeAsync("DeliveryReview", ct: default);
            deliveryReview.创建时间 = now;
            // 新增
            await _context.外产_订单.AddAsync(deliveryReview);
        }

        // 如果状态为「评审驳回」,将备注作为驳回原因回写到外销合同客户产品(按 合同号+货号 匹配)
        if (deliveryReview.状态 == "评审驳回")
        {
            var userProducts = await _context.外销合同客户产品
                .Where(e => e.合同号 == deliveryReview.合同号 && e.货号 == deliveryReview.货号)
                .ToListAsync();

            foreach (var up in userProducts)
            {
                up.驳回原因 = deliveryReview.备注;
            }
        }

        // 一次性保存:主记录(新增/更新)与驳回原因回写,处于同一事务
        await _context.SaveChangesAsync();

        // 返回更新或新增后的实体(如果是更新,返回 existing 更准确)
        return existing ?? deliveryReview;
    }

    /// <summary>新增或修改生产类型覆盖(按合同号+排产编号+货号匹配)</summary>
    public async Task<ProductionTypeOverride> SaveProductionTypeOverride(ProductionTypeOverride overrideEntity)
    {
        if (overrideEntity == null)
        {
            throw new ArgumentNullException(nameof(overrideEntity), "生产类型覆盖信息不能为空");
        }
        if (string.IsNullOrWhiteSpace(overrideEntity.合同号))
        {
            throw new ArgumentException("合同号不能为空");
        }
        if (string.IsNullOrWhiteSpace(overrideEntity.排产编号))
        {
            throw new ArgumentException("排产编号不能为空");
        }
        if (string.IsNullOrWhiteSpace(overrideEntity.货号))
        {
            throw new ArgumentException("货号不能为空");
        }

        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 按 合同号 + 排产编号 + 货号 匹配是否已存在
        var existing = await _context.生产类型修改
            .FirstOrDefaultAsync(x => x.合同号 == overrideEntity.合同号
                && x.排产编号 == overrideEntity.排产编号
                && x.货号 == overrideEntity.货号);

        if (existing != null)
        {
            ApplyClientRowVersion(existing, overrideEntity.RowVersion);
            // 更新:仅覆盖业务字段,保留原有主键与创建时间
            existing.合同号 = overrideEntity.合同号;
            existing.排产编号 = overrideEntity.排产编号;
            existing.货号 = overrideEntity.货号;
            existing.生产类型 = overrideEntity.生产类型;
            existing.修改人 = overrideEntity.修改人;
            existing.修改时间 = string.IsNullOrWhiteSpace(overrideEntity.修改时间) ? now : overrideEntity.修改时间;
            if (string.IsNullOrWhiteSpace(existing.创建时间))
            {
                existing.创建时间 = now;
            }

            _context.Entry(existing).State = EntityState.Modified;
        }
        else
        {
            overrideEntity.编号 = Guid.NewGuid().ToString();
            overrideEntity.创建时间 = now;
            await _context.生产类型修改.AddAsync(overrideEntity);
        }

        await _context.SaveChangesAsync();

        return existing ?? overrideEntity;
    }

    /// <summary>将已通过的交期评审退回待评审,并删除本次分析关联数据</summary>
    public async Task<ReturnDeliveryReviewResultDto> ReturnDeliveryReview(ReturnDeliveryReviewRequestDto request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ReviewId))
        {
            throw new ValidationException("评审编号不能为空");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var review = await _context.外产_订单
                    .FirstOrDefaultAsync(x => x.编号 == request.ReviewId);
                if (review == null)
                {
                    throw new NotFoundException("评审记录不存在或已退回");
                }
                if (review.状态 != "评审通过")
                {
                    throw new ConflictException("仅评审通过的数据可以退回待评审");
                }

                var schedulingNo = review.排产编号 ?? string.Empty;
                var schedulingAnalyses = string.IsNullOrWhiteSpace(schedulingNo)
                    ? new List<SchedulingAnalysis>()
                    : await _context.排产分析单.Where(x => x.排产编号 == schedulingNo).ToListAsync();
                var analysisNos = schedulingAnalyses
                    .Select(x => x.分析单号)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .Distinct()
                    .ToList();

                var bomItems = analysisNos.Count == 0
                    ? new List<ExternalProductionBOM>()
                    : await _context.外产_BOM.Where(x => x.分析单号 != null && analysisNos.Contains(x.分析单号)).ToListAsync();
                var details = await _context.工单销控表明细
                    .Where(x => (!string.IsNullOrEmpty(schedulingNo) && x.排产编号 == schedulingNo)
                        || (x.分析单号 != null && analysisNos.Contains(x.分析单号)))
                    .ToListAsync();
                var pickMaterials = analysisNos.Count == 0
                    ? new List<ExternalProductionPickMaterial>()
                    : await _context.外产_领料.Where(x => x.分析单号 != null && analysisNos.Contains(x.分析单号)).ToListAsync();
                var warehousingItems = analysisNos.Count == 0
                    ? new List<ExternalProductionWarehousing>()
                    : await _context.外产_入库.Where(x => x.分析单号 != null && analysisNos.Contains(x.分析单号)).ToListAsync();
                var productionItems = await _context.外产_生产
                    .Where(x => (!string.IsNullOrEmpty(schedulingNo) && x.排产编号 == schedulingNo)
                        || (x.分析单号 != null && analysisNos.Contains(x.分析单号)))
                    .ToListAsync();
                var shipmentItems = await _context.外产_发运
                    .Where(x => (!string.IsNullOrEmpty(schedulingNo) && x.排产编号 == schedulingNo)
                        || (x.分析单号 != null && analysisNos.Contains(x.分析单号)))
                    .ToListAsync();

                var blockers = new List<string>();
                AddBlocker(blockers, "领料", pickMaterials.Select(x => x.出库数量));
                AddBlocker(blockers, "入库", warehousingItems.Select(x => x.入库数量));
                AddBlocker(blockers, "生产", productionItems.Select(x => x.生产数量));
                AddBlocker(blockers, "发运", shipmentItems.Select(x => x.发运数量));
                AddBlocker(blockers, "工单入库", details.Select(x => x.入库数));
                if (blockers.Count > 0)
                {
                    throw new ConflictException($"该评审已产生下游实绩({string.Join("、", blockers)}),不能退回");
                }

                var detailIds = details.Select(x => x.编号).Where(x => x != null).Select(x => x!).ToList();
                var parentIds = details.Select(x => x.父级编号).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct().ToList();
                var workOrders = parentIds.Count == 0
                    ? new List<WorkOrderSalesControl>()
                    : await _context.工单销控表.Where(x => parentIds.Contains(x.编号)).ToListAsync();
                var remainingParentIds = parentIds.Count == 0
                    ? new HashSet<string>()
                    : (await _context.工单销控表明细
                        .Where(x => x.父级编号 != null && parentIds.Contains(x.父级编号)
                            && (x.编号 == null || !detailIds.Contains(x.编号)))
                        .Select(x => x.父级编号!)
                        .Distinct()
                        .ToListAsync())
                        .ToHashSet();
                var quantityByParent = details
                    .Where(x => !string.IsNullOrWhiteSpace(x.父级编号))
                    .GroupBy(x => x.父级编号!)
                    .ToDictionary(g => g.Key, g => g.Sum(x => ParseRequiredQuantity(x.生产数, "工单明细生产数")));

                var updatedWorkOrderCount = 0;
                var deletedWorkOrderCount = 0;
                foreach (var workOrder in workOrders)
                {
                    var oldTotal = ParseRequiredQuantity(workOrder.工单总数, "工单总数");
                    var returnedQuantity = quantityByParent.GetValueOrDefault(workOrder.编号 ?? string.Empty);
                    var newTotal = Math.Max(0d, oldTotal - returnedQuantity);
                    if (newTotal <= 0d && !remainingParentIds.Contains(workOrder.编号 ?? string.Empty))
                    {
                        _context.工单销控表.Remove(workOrder);
                        deletedWorkOrderCount++;
                        continue;
                    }

                    workOrder.工单总数 = FormatQuantity(newTotal);
                    workOrder.在产数量 = FormatQuantity(Math.Max(0d, newTotal - ParseRequiredQuantity(workOrder.已入库数, "工单已入库数")));
                    updatedWorkOrderCount++;
                }

                var productionIds = productionItems
                    .Select(x => x.编号)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .Distinct()
                    .ToList();
                var productionDeletedCount = productionIds.Count == 0
                    ? 0
                    : await _context.外产_生产
                        .Where(x => productionIds.Contains(x.编号))
                        .ExecuteDeleteAsync();
                if (productionDeletedCount != productionIds.Count)
                {
                    throw new ConflictException("外产生产数据删除不完整,退回操作已回滚,请重试");
                }

                _context.外产_发运.RemoveRange(shipmentItems);
                _context.外产_入库.RemoveRange(warehousingItems);
                _context.外产_领料.RemoveRange(pickMaterials);
                _context.工单销控表明细.RemoveRange(details);
                _context.外产_BOM.RemoveRange(bomItems);
                _context.排产分析单.RemoveRange(schedulingAnalyses);
                _context.外产_订单.Remove(review);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new ReturnDeliveryReviewResultDto
                {
                    ReviewId = request.ReviewId,
                    SchedulingNo = schedulingNo,
                    AnalysisNos = analysisNos,
                    ReviewDeletedCount = 1,
                    SchedulingAnalysisDeletedCount = schedulingAnalyses.Count,
                    BomDeletedCount = bomItems.Count,
                    WorkOrderDetailDeletedCount = details.Count,
                    PickMaterialDeletedCount = pickMaterials.Count,
                    WarehousingDeletedCount = warehousingItems.Count,
                    ProductionDeletedCount = productionDeletedCount,
                    ShipmentDeletedCount = shipmentItems.Count,
                    WorkOrderUpdatedCount = updatedWorkOrderCount,
                    WorkOrderDeletedCount = deletedWorkOrderCount
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private static void AddBlocker(List<string> blockers, string name, IEnumerable<string?> values)
    {
        if (values.Any(HasBusinessQuantity))
        {
            blockers.Add(name);
        }
    }

    private static bool HasBusinessQuantity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return !double.TryParse(value, out var quantity) || quantity > 0d;
    }

    private static double ParseRequiredQuantity(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0d;
        if (double.TryParse(value, out var quantity)) return quantity;
        throw new ConflictException($"{fieldName}存在无法识别的数量,不能退回");
    }

    private static string FormatQuantity(double value)
    {
        return value.ToString("0.################", CultureInfo.InvariantCulture);
    }

    /// <summary>获取交期评审列表(分页)</summary>
    public async Task<PagedResult<PMCDeliveryReview>> GetPMCDeliveryReviewList(
        PMCRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.外产_订单
           .AsNoTracking()
           .AsQueryable();
        if (!string.IsNullOrEmpty(request.合同号))
        {
            query = query.Where(e => e.合同号 == request.合同号);
        }
        if (!string.IsNullOrEmpty(request.排产编号)) query = query.Where(e => e.排产编号 == request.排产编号);
        if (!string.IsNullOrEmpty(request.分析单号)) query = query.Where(e => e.分析单号 == request.分析单号);
        if (!string.IsNullOrEmpty(request.货号)) query = query.Where(e => e.货号 == request.货号);
        // 根据生产类型过滤:直接匹配 PMCDeliveryReview 自身生产类型字段
        if (!string.IsNullOrEmpty(request.生产类型))
        {
            query = query.Where(e => e.生产类型 == request.生产类型);
        }

        return await query.OrderByDescending(e => e.创建时间).ThenBy(e => e.编号)
            .ToPagedResultAsync(request, cancellationToken);
    }
}
