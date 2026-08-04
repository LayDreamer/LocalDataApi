using LocalDataApi.Data;
using LocalDataApi.Dto;
using LocalDataApi.Exceptions;
using LocalDataApi.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using LocalDataApi.Utils;
using Microsoft.Extensions.Caching.Memory;

namespace LocalDataApi.Services
{
    public class PMCService : IPMCService
    {
        private readonly AppDbContext _context;
        private readonly ERPBaseService _erpBaseService;
        private readonly IMemoryCache _cache;

        public PMCService(AppDbContext context, ERPBaseService erpBaseService, IMemoryCache cache)
        {
            _context = context;
            _erpBaseService = erpBaseService;
            _cache = cache;
        }

        // 获取外销合同产品列表(测试根据分析单号来获取)
        public async Task<PagedResult<PMCProductInfo>> GetPMCProductListInfo(
            PMCRequestDto request, CancellationToken cancellationToken = default)
        {
            var query = _context.外销合同产品
                .AsNoTracking()
                .Where(e => e.层 == "0")
                .AsQueryable();
            if (!string.IsNullOrEmpty(request.分析单号))
            {
                query = query.Where(e => e.分析单号 == request.分析单号);
            }
            if (!string.IsNullOrEmpty(request.合同号))
            {
                query = query.Where(e => e.合同号 == request.合同号);
            }
            if (!string.IsNullOrEmpty(request.排产编号))
            {
                query = query.Where(e => e.排产编号 == request.排产编号);
            }
            if (!string.IsNullOrEmpty(request.货号))
            {
                query = query.Where(e => e.货号 == request.货号);
            }
            if (!string.IsNullOrEmpty(request.线圈货号))
            {
                query = query.Where(e => e.线圈 == request.线圈货号);
            }

            return await query
                .OrderByDescending(e => e.创建时间)
                .ThenBy(e => e.编号)
                .ToPagedResultAsync(request, cancellationToken);
        }

        #region PMC交期评审相关

        // 获取外销合同客户产品列表
        public async Task<IReadOnlyList<PMCUserProductInfo>> GetPMCUserProductInfoList(
            PMCRequestDto request, CancellationToken cancellationToken = default)
        {
            var query = _context.外销合同客户产品
               .AsNoTracking()
               .AsQueryable();

            // 只获取最近6个月的数据
            var sixMonthsAgo = DateTime.Now.AddMonths(-6);
            var sixMonthsAgoStr = sixMonthsAgo.ToString("yyyy-MM-dd");

            // query = query.Where(e => e.合同号 == "SNZJY2603117");

            // 使用字符串比较来过滤日期
            query = query.Where(e => string.Compare(e.创建时间, sixMonthsAgoStr) >= 0);

            // 必须在分页前排除已评审订单，否则页面条数和 Total 都会不准确。
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
            // 根据生产类型过滤：覆盖值优先，未覆盖时取外销合同基本信息中的生产类型
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


        public async Task<PagedResult<PMCDeliveryReview>> ConvertToPMCDeliveryReviewList(
            PMCRequestDto request, CancellationToken cancellationToken = default)
        {
            var sixMonthsAgoStr = DateTime.Now.AddMonths(-6).ToString("yyyy-MM-dd");
            var countQuery = _context.外销合同客户产品.AsNoTracking()
                .Where(e => string.Compare(e.创建时间, sixMonthsAgoStr) >= 0)
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
            // 根据生产类型过滤：覆盖值优先，未覆盖时取外销合同基本信息中的生产类型
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

        // 根据PMC客户产品列表转换为交期评审信息
        public async Task<List<PMCDeliveryReview>> ConvertToPMCDeliveryReviewList(List<PMCUserProductInfo> userProductInfos)
        {
            try
            {
                // 预分配List容量，避免动态扩容
                List<PMCDeliveryReview> data = new List<PMCDeliveryReview>(userProductInfos.Count);
                // string orderUser = "ZY1[张圆]";
                if (userProductInfos == null || userProductInfos.Count == 0)
                {
                    return data;
                }

                // 提取查询需要的货号列表
                var itemNos = userProductInfos.Select(e => e.货号).Where(e => !string.IsNullOrEmpty(e)).Distinct().ToList();
                var itemNoSet = itemNos.ToHashSet(StringComparer.Ordinal);

                // 根据货号查询外销合同产品表中的排产用户：取实际完成日期有值且最近的第一个，构建 货号 -> 排产用户 字典
                var schedulingUserDict = new Dictionary<string, string>();
                if (itemNos.Count > 0)
                {
                    var productUsers = await _context.外销合同产品
                        .AsNoTracking()
                        .Where(e => !string.IsNullOrEmpty(e.货号)
                            && !string.IsNullOrEmpty(e.排产用户)
                            && !string.IsNullOrEmpty(e.实际完成日期))
                        .WhereInBatchesAsync(itemNos, e => e.货号, e => new { e.货号, e.排产用户, e.实际完成日期 });

                    // 每个货号取实际完成日期最近的一条对应的排产用户（日期为字符串，按序数比较降序）
                    schedulingUserDict = productUsers
                        .GroupBy(x => x.货号!)
                        .ToDictionary(
                            g => g.Key,
                            g => g.OrderByDescending(x => x.实际完成日期, StringComparer.Ordinal).First().排产用户!);
                }

                // 提前查询货号对应的合同号在外销合同基本信息中的生产类型，构建 合同号 -> 生产类型 字典
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

                // 提前查询生产类型修改表（交期评审生产类型手动覆盖），构建 (合同号,排产编号,货号) -> 生产类型 字典
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

                    // 构建字典，便于快速查询
                    foreach (var info in sourceInfo)
                    {
                        if (!string.IsNullOrEmpty(info.货号) && itemNoSet.Contains(info.货号))
                        {
                            sourceInfoDict[info.货号] = info.制造方式 ?? "";
                        }
                    }
                }

                // var analysisNum = await GenerateAnalysisOrderNumberAsync(orderUser);

                // ===== 在内存中处理数据转换 =====
                foreach (var item in userProductInfos)
                {
                    string schedulingNumber = $"{item.合同号}-{item.序号}";
                    string source = sourceInfoDict.GetValueOrDefault(item.货号) ?? "";
                    var overrideKey = (item.合同号 ?? "", schedulingNumber, item.货号 ?? "");
                    var hasOverride = overrideDict.TryGetValue(overrideKey, out var overrideValue);
                    //string source="";
                    // 已评审订单已在数据库分页查询前排除，此处只负责本页数据转换。
                    // 计算工单工号
                    // string workOrder = _erpBaseService.CalculateWorkOrder(item.编号);

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
                        特殊要求=item.备注,                        
                        // 工单单号 = workOrder,
                        线圈货号 = coilNumber,
                        来源 = source,
                        // 默认取外销合同基本信息的生产类型；若生产类型修改表存在 (合同号,排产编号,货号) 一致的记录，则以其覆盖
                        // 注意：合同号或货号为 null/空 时不会匹配到覆盖表（overrideDict 仅收录非空记录），故保持原生产类型
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
            catch (Exception ex)
            {
                Console.WriteLine($"ConvertToPMCDeliveryReviewList 方法异常: {ex.Message}");
                throw;
            }
        }

        // 新增PMC交期评审信息
        public async Task<PMCDeliveryReview> AddPMCDeliveryReview(PMCDeliveryReview deliveryReview)
        {
            if (deliveryReview == null)
            {
                throw new ArgumentNullException(nameof(deliveryReview), "产品信息不能为空");
            }

            // 可根据业务需求增加字段非空校验，例如：
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
                // 更新现有实体：将传入实体的所有属性值复制到现有实体
                // 保留原创建时间；若原为空则用当前时间回填
                deliveryReview.创建时间 = string.IsNullOrWhiteSpace(existing.创建时间) ? now : existing.创建时间;
                _context.Entry(existing).CurrentValues.SetValues(deliveryReview);
                // 注意：如果某些字段不需要更新，可以单独赋值
                // 例如：existing.状态 = deliveryReview.状态; existing.最终交期 = deliveryReview.最终交期; ...
            }
            else
            {
                deliveryReview.编号 = Guid.NewGuid().ToString();
                deliveryReview.创建时间 = now;
                // 新增
                await _context.外产_订单.AddAsync(deliveryReview);
            }

            // 如果状态为「评审驳回」，将备注作为驳回原因回写到外销合同客户产品（按 合同号+货号 匹配）
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

            // 一次性保存：主记录（新增/更新）与驳回原因回写，处于同一事务
            await _context.SaveChangesAsync();

            // 返回更新或新增后的实体（如果是更新，返回 existing 更准确）
            return existing ?? deliveryReview;
        }

        // 新增/修改生产类型覆盖（交期评审生产类型手动覆盖）
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
                // 更新：仅覆盖业务字段，保留原有主键与创建时间
                // （不能用 SetValues(overrideEntity)，否则会把 detach 实体的空 编号 写回，导致主键冲突）
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
                    throw new ConflictException($"该评审已产生下游实绩（{string.Join("、", blockers)}），不能退回");
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
                    throw new ConflictException("外产生产数据删除不完整，退回操作已回滚，请重试");
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
            throw new ConflictException($"{fieldName}存在无法识别的数量，不能退回");
        }

        private static string FormatQuantity(double value)
        {
            return value.ToString("0.################", System.Globalization.CultureInfo.InvariantCulture);
        }

        //PMC交期评审列表
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
            // 根据生产类型过滤：直接匹配 PMCDeliveryReview 自身生产类型字段
            if (!string.IsNullOrEmpty(request.生产类型))
            {
                query = query.Where(e => e.生产类型 == request.生产类型);
            }

            return await query.OrderByDescending(e => e.创建时间).ThenBy(e => e.编号)
                .ToPagedResultAsync(request, cancellationToken);
        }

        #endregion







        #region 排产分析单相关




        // 根据排产用户生成分析单号        
        public async Task<string> GenerateAnalysisOrderNumberAsync(string productionUser)
        {
            // 1. 提取排产用户代码（取 '[' 之前的部分）
            string userCode = productionUser?.Split('[')[0];
            if (string.IsNullOrWhiteSpace(userCode))
            {
                throw new ArgumentException("排产用户格式不正确，无法提取代码", nameof(productionUser));
            }

            // 2. 当前年月，格式 yyMM（如 2603）
            string yearMonth = DateTime.Now.ToString("yyMM");

            // 3. 构造前缀：PC + 用户代码 + 年月
            string prefix = $"PC{userCode}{yearMonth}";

            // 4. 查询数据库中最大的流水码（即最后4位数字）
            var existingNumbers = await _context.排产分析单
                .Where(x => x.分析单号.StartsWith(prefix))
                .Select(x => x.分析单号)
                .ToListAsync();

            int maxSerial = 0;
            foreach (var number in existingNumbers)
            {
                // 提取前缀之后的剩余部分
                string suffix = number.Substring(prefix.Length);

                // 尝试将剩余部分转换为整数（忽略非数字部分，但这里假设剩余部分全是数字）
                if (int.TryParse(suffix, out int serial) && serial > maxSerial)
                {
                    maxSerial = serial;
                }
            }
            int newSerial = maxSerial + 1;
            string serialStr = newSerial.ToString("D4");
            return $"{prefix}{serialStr}";
        }


        public async Task<SchedulingAnalysis> SaveSchedulingAnalysisAsync(PMCRequestDto request)
        {
            // 从外产_订单中查找对应货号的数据
            var deliveryReview = await _context.外产_订单
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.货号 == request.货号);

            if (deliveryReview != null)
            {
                // 如果存在则调用另一个重载方法进行保存
                return await SaveSchedulingAnalysisAsync(deliveryReview);
            }

            return null;
        }



        // 根据排产用户和分析单号保存排产分析单信息
        public async Task<SchedulingAnalysis> SaveSchedulingAnalysisAsync(PMCDeliveryReview deliveryReview)
        {
            string nowss = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string nowdd = DateTime.Now.ToString("yyyy-MM-dd");
            string productionUser = deliveryReview.排产用户;
            string userNum = await GetUserNumberAsync(productionUser);
            var analysisNum = await GenerateAnalysisOrderNumberAsync(deliveryReview.排产用户);
            string prefix = $"{userNum}UNT";
            var existingNumbers = await _context.排产分析单
                .Where(x => x.编号.StartsWith(prefix))
                .Select(x => x.编号)
                .ToListAsync();

            int maxSerial = 0;
            foreach (var number in existingNumbers)
            {
                // 提取前缀之后的剩余部分
                string suffix = number.Substring(prefix.Length);

                // 尝试将剩余部分转换为整数（忽略非数字部分，但这里假设剩余部分全是数字）
                if (int.TryParse(suffix, out int serial) && serial > maxSerial)
                {
                    maxSerial = serial;
                }
            }

            // 新流水码 = 最大值 + 1，可根据业务补足位数（例如补零到5位）
            int newSerial = maxSerial + 1;
            string serialStr = newSerial.ToString("D5"); // 假设需要5位，不足补零
            string newNumber = $"{prefix}{serialStr}";

            var scheduling = new SchedulingAnalysis
            {
                编号 = newNumber,
                用户铭 = productionUser,
                用户编号 = userNum,
                修改状态 = "0",
                创建时间 = nowss,
                锁定用户 = productionUser?.Split('[')[0],
                审核过程 = $"{productionUser}-{nowss}",
                分析单号 = analysisNum,
                分析人 = productionUser,
                分析日期 = nowdd,
                排产编号 = deliveryReview.排产编号,
            };
            //测试时先不保存到数据库，等后续逻辑完善后再保存
            await _context.排产分析单.AddAsync(scheduling);
            return scheduling;
        }

        // 根据排产用户获取用户编号
        public async Task<string> GetUserNumberAsync(string productionUser)
        {
            // 1. 提取排产用户代码（取 '[' 之前的部分）
            string userCode = productionUser?.Split('[')[0];
            if (string.IsNullOrWhiteSpace(userCode))
            {
                throw new ArgumentException("排产用户格式不正确，无法提取代码", nameof(productionUser));
            }
            var userNumber = await _context.tb_control_user
               .Where(x => x.usercode == userCode)
               .Select(x => x.ID).FirstOrDefaultAsync();
            return userNumber.Trim();
        }

        #endregion


        #region 转换的排产分析详情列表

        // 根据销控表中的货号获取排产分析详情列表（嵌套树形结构）
        // public async Task<List<SchedulingAnalysisDto>> ConvertToSchedulingAnalysisList(PMCRequestDto request)
        // {
        //     var result = new List<SchedulingAnalysisDto>();

        //     // 第一步：获取外产 BOM 扁平列表，再转换成树形结构
        //     var bomRecords = await GetBomByItemNo(request.货号);
        //     var assemblyTree = await ConvertBomToAssemblyTreeAsync(bomRecords);

        //     // 第二步：收集所有货号（父 + 所有层级子级）
        //     var itemNos = new HashSet<string>();
        //     var processedItemNo = request.货号;
        //     int bracketIndex = processedItemNo.IndexOf('(');
        //     if (bracketIndex > 0)
        //         processedItemNo = processedItemNo.Substring(0, bracketIndex);
        //     if (!string.IsNullOrEmpty(request.货号))
        //         itemNos.Add(request.货号);
        //     if (!string.IsNullOrEmpty(processedItemNo))
        //         itemNos.Add(processedItemNo);

        //     CollectAllItemNos(assemblyTree, itemNos);

        //     // 第三步：批量查询所有数据（只查询一次）
        //     var productDataDict = await GetProductDataBatchAsync(itemNos.ToList());
        //     var warehouseGoodsDict = await GetWarehouseGoodsBatchAsync(itemNos.ToList(), request.货号);
        //     // var productionDemandDict = await GetProductionDemandBatchAsync(itemNos.ToList(), schedulingNo);
        //     // var inTransitQuantityDict = await GetInTransitQuantityBatchAsync(itemNos.ToList(), schedulingNo);
        //     var productionDemandDict=new Dictionary<string, ProductionDemand>();
        //      var inTransitQuantityDict=new Dictionary<string, InTransitQuantity>();
        //     // 第四步：构建嵌套树形结果
        //     var parentDto = BuildDto(request.货号,  0, null,
        //         productDataDict, warehouseGoodsDict, productionDemandDict, inTransitQuantityDict);

        //     // 构建子级嵌套结构
        //     parentDto.子集 = BuildNestedDtoList(assemblyTree, 1,
        //     productDataDict, warehouseGoodsDict, productionDemandDict, inTransitQuantityDict);
        //     result.Add(parentDto);
        //     return result;
        // }


        public async Task<List<SchedulingAnalysisDto>> GetSchedulingAnalysisList(PMCRequestDto request)
        {
            var result = new List<SchedulingAnalysisDto>();

            // 第一步：获取外产 BOM 扁平列表，再转换成树形结构
            var bomRecords = await GetBomByItemNo(request.货号);
            var assemblyTree = await ConvertBomToAssemblyTreeAsync(bomRecords);

            // 第二步：收集所有货号（父 + 所有层级子级）
            var itemNos = new HashSet<string>();
            var processedItemNo = request.货号;
            int bracketIndex = processedItemNo.IndexOf('(');
            if (bracketIndex > 0)
                processedItemNo = processedItemNo.Substring(0, bracketIndex);
            if (!string.IsNullOrEmpty(request.货号))
                itemNos.Add(request.货号);
            if (!string.IsNullOrEmpty(processedItemNo))
                itemNos.Add(processedItemNo);

            CollectAllItemNos(assemblyTree, itemNos);

            // 第三步：批量查询所有数据（只查询一次）
            var productDataDict = await GetProductDataBatchAsync(itemNos.ToList());
            var warehouseGoodsDict = await GetWarehouseGoodsBatchAsync(itemNos.ToList(), request.货号);
            // var productionDemandDict = await GetProductionDemandBatchAsync(itemNos.ToList(), schedulingNo);
            // var productionDemandDict = await GetProductionDemandBatchAsync(itemNos.ToList(), schedulingNo);
            //在产需求（来源：外产_领料，按货号分组：需求量之和 - 出库数量之和）
            var productionDemandDict = await GetProductionDemandFromPickMaterialBatchAsync(itemNos.ToList());
            //在途数（来源：外产_入库，按货号分组：需求量之和 - 入库数量之和）
            var inTransitQuantityDict = await GetInTransitQuantityFromWarehousingBatchAsync(itemNos.ToList());
            // 第四步：构建嵌套树形结果
            var parentDto = BuildDto(request.货号, 0, null, null,
                productDataDict, warehouseGoodsDict, productionDemandDict, inTransitQuantityDict);

            // 构建子级嵌套结构
            parentDto.子集 = BuildNestedDtoList(assemblyTree, 1,
            productDataDict, warehouseGoodsDict, productionDemandDict, inTransitQuantityDict);
            result.Add(parentDto);
            return result;
        }



        /// <summary>
        /// 将外产 BOM 扁平记录集合转换为 AssemblyNode 树形结构。
        /// 返回的树：顶层为 level=1 的节点（线圈、半成品）；
        /// 所有层级的子级通过关联编号查询装配清单补充 用量/来源/单位（支持任意深度）。
        /// </summary>
        private async Task<List<AssemblyNode>> ConvertBomToAssemblyTreeAsync(List<ExternalProductionBOM> bomRecords)
        {
            var result = new List<AssemblyNode>();

            if (bomRecords == null || bomRecords.Count == 0)
                return result;

            // 按编号建立索引
            var bomById = bomRecords.ToDictionary(b => b.编号 ?? string.Empty, b => b);

            // 按父级编号分组
            var childrenByParent = bomRecords
                .Where(b => !string.IsNullOrEmpty(b.父级编号))
                .GroupBy(b => b.父级编号!)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 从 ALL 层级收集所有 关联编号（支持 level >= 2 的深层节点）
            var assemblyDict = new Dictionary<string, ProductDataAssemblyList>();
            var allRelIds = bomRecords
                .Where(b => !string.IsNullOrEmpty(b.关联编号))
                .Select(b => b.关联编号!)
                .Distinct()
                .ToList();

            if (allRelIds.Count > 0)
            {
                var assemblies = await _context.产品资料装配清单
                    .AsNoTracking()
                    .Where(e => allRelIds.Contains(e.编号))
                    .ToListAsync();

                // 批量取 制造方式、数量单位
                var asmItemNos = assemblies
                    .Where(a => !string.IsNullOrEmpty(a.货号))
                    .Select(a => a.货号!)
                    .Distinct()
                    .ToList();

                if (asmItemNos.Count > 0)
                {
                    var productData = await _context.产品资料
                        .AsNoTracking()
                        .Where(e => asmItemNos.Contains(e.货号))
                        .Select(e => new { e.货号, e.制造方式, e.数量单位 })
                        .ToListAsync();

                    var sourceDict = productData.ToDictionary(e => e.货号!, e => e.制造方式 ?? "");
                    var unitDict = productData.ToDictionary(e => e.货号!, e => e.数量单位 ?? "");

                    foreach (var a in assemblies)
                    {
                        if (!string.IsNullOrEmpty(a.货号))
                        {
                            if (sourceDict.TryGetValue(a.货号, out var src)) a.来源 = src;
                            if (unitDict.TryGetValue(a.货号, out var unit)) a.单位 = unit;
                        }
                    }
                }

                foreach (var a in assemblies)
                {
                    if (!string.IsNullOrEmpty(a.编号))
                        assemblyDict[a.编号!] = a;
                }
            }

            // 递归构建树：从顶层（level=1 且 父级是成品节点）开始
            var rootNodes = bomRecords
                .Where(b =>
                    b.层 == "1" &&
                    !string.IsNullOrEmpty(b.父级编号) &&
                    bomById.ContainsKey(b.父级编号!) &&
                    bomById[b.父级编号!].层 == "0")
                .ToList();

            AssemblyNode BuildNode(ExternalProductionBOM bom)
            {
                ProductDataAssemblyList? assembly = null;
                if (!string.IsNullOrEmpty(bom.关联编号) && assemblyDict.TryGetValue(bom.关联编号!, out var asm))
                {
                    assembly = asm;
                }
                else
                {
                    // 没有关联编号的节点（线圈、半成品本身），构造一个占位 assembly 以保留货号
                    assembly = new ProductDataAssemblyList
                    {
                        货号 = bom.货号
                    };
                }

                var node = new AssemblyNode { Assembly = assembly, 生产数 = bom.生产数 };

                if (childrenByParent.TryGetValue(bom.编号 ?? string.Empty, out var childBoms))
                {
                    node.Children = childBoms.Select(BuildNode).ToList();
                }

                return node;
            }

            foreach (var b in rootNodes)
                result.Add(BuildNode(b));

            return result;
        }



        /// <summary>
        /// 递归收集所有货号
        /// </summary>
        private void CollectAllItemNos(List<AssemblyNode>? nodes, HashSet<string> itemNos)
        {
            if (nodes == null || nodes.Count == 0)
                return;

            foreach (var node in nodes)
            {
                if (!string.IsNullOrEmpty(node.Assembly.货号))
                    itemNos.Add(node.Assembly.货号);

                // 递归收集子级货号
                CollectAllItemNos(node.Children, itemNos);
            }
        }

        /// <summary>
        /// 构建单个Dto
        /// </summary>
        private SchedulingAnalysisDto BuildDto(
            string itemNo,
          
            int level,
            ProductDataAssemblyList? assembly,
            string? 生产数,
            Dictionary<string, ProductData> productDataDict,
            Dictionary<string, WarehouseGoods> warehouseGoodsDict,
            Dictionary<string, ProductionDemand> productionDemandDict,
            Dictionary<string, InTransitQuantity> inTransitQuantityDict)
        {
            productDataDict.TryGetValue(itemNo, out var productData);
            warehouseGoodsDict.TryGetValue(itemNo, out var goodsData);
            productionDemandDict.TryGetValue(itemNo, out var productionDemand);
            inTransitQuantityDict.TryGetValue(itemNo, out var inTransit);

            // 计算仓库可用数
            // string AvailableQuantity = (double.Parse(goodsData?.数量 ?? "0") + (inTransit?.在产量 ?? 0) - (productionDemand?.需求量 ?? 0)).ToString();

            return new SchedulingAnalysisDto
            {
                货号 = itemNo,
                层 = level.ToString(),
                品名 = productData?.中文品名,
                规格 =  productData?.中文规格,
                来源 = assembly?.来源 ?? productData?.制造方式 ?? "",
                用量 = assembly?.用量 ?? "",
                中间件 = assembly?.中间件 ?? "",
                单位 = assembly?.单位 ?? productData?.数量单位 ?? "",
                仓库名称 = goodsData?.仓库名,
                仓库数 = goodsData?.数量,
                库存上限 = goodsData?.库存上限,
                库存下限 = goodsData?.库存下限,
                产品属性 = productData?.产品属性,
                工序名称 = productData?.工序名称,
                工序车间 = productData?.生产车间,
                在产需求 = productionDemand?.在产需求?.ToString(),
                在途数 = inTransit?.在途数?.ToString(),
                生产数 = 生产数
            };
        }

        /// <summary>
        /// 递归构建嵌套Dto列表
        /// </summary>
        private List<SchedulingAnalysisDto>? BuildNestedDtoList(
            List<AssemblyNode>? nodes,
            int level,
            Dictionary<string, ProductData> productDataDict,
            Dictionary<string, WarehouseGoods> warehouseGoodsDict,
            Dictionary<string, ProductionDemand> productionDemandDict,
            Dictionary<string, InTransitQuantity> inTransitQuantityDict)

        {
            if (nodes == null || nodes.Count == 0)
                return null;

            var result = new List<SchedulingAnalysisDto>();

            foreach (var node in nodes)
            {
                var dto = BuildDto(node.Assembly.货号,level, node.Assembly, node.生产数,
                    productDataDict, warehouseGoodsDict, productionDemandDict, inTransitQuantityDict);

                // 递归构建子集
                dto.子集 = BuildNestedDtoList(node.Children, level + 1,
                    productDataDict, warehouseGoodsDict, productionDemandDict, inTransitQuantityDict);

                result.Add(dto);
            }
            return result;
        }

        /// <summary>
        /// 装配节点（用于构建树形结构）
        /// </summary>
        private class AssemblyNode
        {
            public ProductDataAssemblyList Assembly { get; set; } = null!;
            public string? 生产数 { get; set; }
            public List<AssemblyNode>? Children { get; set; }
        }

        /// <summary>
        /// 递归获取装配清单树（嵌套树形结构）
        /// </summary>
        private async Task<List<AssemblyNode>> GetAssemblyTreeNested(
            string? itemNo,
            HashSet<string>? visitedItemNos = null)
        {
            var result = new List<AssemblyNode>();

            if (string.IsNullOrWhiteSpace(itemNo))
                return result;

            // 初始化已访问集合
            if (visitedItemNos == null)
                visitedItemNos = new HashSet<string>();

            // 预处理货号（去掉括号内容）
            string processedItemNo = itemNo;
            int bracketIndex = itemNo.IndexOf('(');
            if (bracketIndex > 0)
                processedItemNo = itemNo.Substring(0, bracketIndex);

            // // 防止循环引用
            if (!visitedItemNos.Add(itemNo))
                return result;

            // 获取当前货号的装配信息
            var assemblyList = await GetProductDataAssemblyList(processedItemNo);


            // 批量收集所有货号，一次性查询产品资料获取制造方式和数量单位
            var assemblyItemNos = assemblyList
                .Where(a => !string.IsNullOrEmpty(a.货号))
                .Select(a => a.货号!)
                .Distinct()
                .ToList();

            var sourceDict = new Dictionary<string, string>();
            var unitDict = new Dictionary<string, string>();
            if (assemblyItemNos.Count > 0)
            {
                var productDataList = await _context.产品资料
                    .AsNoTracking()
                    .Where(e => assemblyItemNos.Contains(e.货号))
                    .Select(e => new { e.货号, e.制造方式, e.数量单位 })
                    .ToListAsync();

                foreach (var pd in productDataList)
                {
                    if (!string.IsNullOrEmpty(pd.货号))
                    {
                        sourceDict[pd.货号] = pd.制造方式 ?? "";
                        unitDict[pd.货号] = pd.数量单位 ?? "";
                    }
                }
            }

            foreach (var assembly in assemblyList)
            {
                if (string.IsNullOrEmpty(assembly.货号))
                    continue;

                // 从字典获取制造方式并赋值给来源
                if (sourceDict.TryGetValue(assembly.货号, out var source))
                {
                    assembly.来源 = source;
                }
                // 从字典获取数量单位并赋值给单位
                if (unitDict.TryGetValue(assembly.货号, out var unit))
                {
                    assembly.单位 = unit;
                }

                var node = new AssemblyNode { Assembly = assembly };

                // 如果来源是自制或外协，继续递归获取其子级
                if (!string.IsNullOrEmpty(assembly.来源) &&
                    (assembly.来源 == "自制" || assembly.来源 == "外协"))
                {
                    // 递归获取下一层级
                    node.Children = await GetAssemblyTreeNested(assembly.货号, visitedItemNos);
                }
                result.Add(node);
            }

            return result;
        }


        /// <summary>
        /// 批量获取产品资料
        /// </summary>
        private async Task<Dictionary<string, ProductData>> GetProductDataBatchAsync(List<string> itemNos)
        {
            if (itemNos == null || itemNos.Count == 0)
            {
                return new Dictionary<string, ProductData>();
            }

            var productDataList = await _context.产品资料
                .AsNoTracking()
                .Where(e => itemNos.Contains(e.货号) && !string.IsNullOrEmpty(e.货号))
                .ToListAsync();

            // 使用 GroupBy + First() 处理重复货号，保持与 GetProductData 行为一致
            return productDataList.Where(e => e.货号 != null)
                .GroupBy(e => e.货号!)
                .ToDictionary(g => g.Key, g => g.First());
        }

        /// <summary>
        /// 批量获取仓库货品数据
        /// </summary>
        private async Task<Dictionary<string, WarehouseGoods>> GetWarehouseGoodsBatchAsync(List<string> itemNos, string? rootItemNo)
        {
            if (itemNos == null || itemNos.Count == 0)
            {
                return new Dictionary<string, WarehouseGoods>();
            }

            var warehouseGoodsList = await _context.仓库货品
                .AsNoTracking()
                .Where(e => itemNos.Contains(e.货号) && !string.IsNullOrEmpty(e.货号))
                .ToListAsync();

            static double ParseDouble(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return 0d;
                return double.TryParse(value, out var d) ? d : 0d;
            }

            // 根节点（货号 == rootItemNo）不参与仓库信息筛选，直接按数量取最大的一条
            var rootDict = warehouseGoodsList
                .Where(e => e.货号 != null && e.货号 == rootItemNo)
                .GroupBy(e => e.货号!)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => ParseDouble(e.数量)).First());

            // 非根节点的记录，按仓库信息筛选（仓库类型=存货仓 且 纳入需求计算=T），
            // 同一货号多条时取数量最大的一条
            var otherList = warehouseGoodsList
                .Where(e => e.货号 != rootItemNo)
                .ToList();
            var otherDict = await FilterWarehouseGoodsByWarehouseInfoAsync(otherList);

            // 合并（根节点优先级更高，避免被覆盖）
            var result = new Dictionary<string, WarehouseGoods>(otherDict);
            foreach (var kvp in rootDict)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        /// <summary>
        /// 根据仓库信息筛选有效的仓库货品数据。
        /// 规则：仓库货品.仓库名 需匹配 仓库信息.仓库名（动态比较），
        ///       且对应仓库信息满足 仓库类型 == "存货仓" 且 纳入需求计算 == "T"；
        ///       同一货号有多条满足条件的记录时，按数量降序取最大的一条。
        /// </summary>
        private async Task<Dictionary<string, WarehouseGoods>> FilterWarehouseGoodsByWarehouseInfoAsync(List<WarehouseGoods> warehouseGoodsList)
        {
            if (warehouseGoodsList == null || warehouseGoodsList.Count == 0)
            {
                return new Dictionary<string, WarehouseGoods>();
            }

            static double ParseDouble(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return 0d;
                return double.TryParse(value, out var d) ? d : 0d;
            }

            // 1）收集仓库货品中出现的仓库名
            var warehouseNames = warehouseGoodsList
                .Where(e => !string.IsNullOrWhiteSpace(e.仓库名))
                .Select(e => e.仓库名!)
                .Distinct()
                .ToList();

            // 2）查询仓库信息，筛选 仓库类型=存货仓 且 纳入需求计算=T 的有效仓库名
            var validWarehouseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (warehouseNames.Count > 0)
            {
                var validNames = await _context.仓库信息
                    .AsNoTracking()
                    .Where(w => warehouseNames.Contains(w.名称)
                                && w.仓库类型 == "存货仓"
                                && w.纳入需求计算 == "T")
                    .Select(w => w.名称!)
                    .ToListAsync();

                validWarehouseNames = new HashSet<string>(validNames, StringComparer.OrdinalIgnoreCase);
            }

            // 3）只保留仓库名在有效集合中的仓库货品
            var filtered = warehouseGoodsList
                .Where(e => !string.IsNullOrWhiteSpace(e.仓库名) && validWarehouseNames.Contains(e.仓库名!))
                .ToList();

            // 4）按货号分组，每组取数量最大的一条
            return filtered.Where(e => e.货号 != null)
                .GroupBy(e => e.货号!)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => ParseDouble(e.数量)).First());
        }

        /// <summary>
        /// 批量获取在产需求量
        /// </summary>
        private async Task<Dictionary<string, ProductionDemand>> GetProductionDemandBatchAsync(List<string> itemNos, string? schedulingNo)
        {
            if (itemNos == null || itemNos.Count == 0)
            {
                return new Dictionary<string, ProductionDemand>();
            }

            var query = _context.在产需求量
                .AsNoTracking()
                .Where(e => itemNos.Contains(e.货号) && !string.IsNullOrEmpty(e.货号));

            if (!string.IsNullOrWhiteSpace(schedulingNo))
            {
                query = query.Where(e => e.排产编号 == schedulingNo);
            }

            var productionDemandList = await query.ToListAsync();

            // 使用 GroupBy + First() 处理重复货号，保持与 GetProductionDemand 行为一致
            return productionDemandList.Where(e => e.货号 != null)
                .GroupBy(e => e.货号!)
                .ToDictionary(g => g.Key, g => g.First());
        }
        /// <summary>
        /// 批量获取在途数
        /// </summary>
        /// <summary>
        /// 批量获取在途数
        /// </summary>
        private async Task<Dictionary<string, InTransitQuantity>> GetInTransitQuantityBatchAsync(List<string> itemNos, string? schedulingNo)
        {
            if (itemNos == null || itemNos.Count == 0)
            {
                return new Dictionary<string, InTransitQuantity>();
            }

            var query = _context.在途数
                .AsNoTracking()
                .Where(e => itemNos.Contains(e.货号) && !string.IsNullOrEmpty(e.货号));

            if (!string.IsNullOrWhiteSpace(schedulingNo))
            {
                query = query.Where(e => e.排产编号 == schedulingNo);
            }

            var inTransitQuantityList = await query.ToListAsync();

            // 使用 GroupBy + First() 处理重复货号，保持与 GetInTransitQuantity 行为一致
            return inTransitQuantityList.Where(e => e.货号 != null)
                .GroupBy(e => e.货号!)
                .ToDictionary(g => g.Key, g => g.First());
        }

        /// <summary>
        /// 批量获取在途数（来源：外产_入库）
        /// 按货号分组，计算 需求量之和 - 入库数量之和
        /// </summary>
        private async Task<Dictionary<string, InTransitQuantity>> GetInTransitQuantityFromWarehousingBatchAsync(List<string> itemNos)
        {
            if (itemNos == null || itemNos.Count == 0)
            {
                return new Dictionary<string, InTransitQuantity>();
            }

            static double ParseDouble(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return 0d;
                return double.TryParse(value, out var d) ? d : 0d;
            }

            var list = await _context.外产_入库
                .AsNoTracking()
                .Where(e => itemNos.Contains(e.货号) && !string.IsNullOrEmpty(e.货号))
                .ToListAsync();

            return list
                .Where(e => !string.IsNullOrEmpty(e.货号))
                .GroupBy(e => e.货号!)
                .ToDictionary(g => g.Key, g => new InTransitQuantity
                {
                    货号 = g.Key,
                    在途数 = g.Sum(e => ParseDouble(e.需求量)) - g.Sum(e => ParseDouble(e.入库数量))
                });
        }

        /// <summary>
        /// 批量获取在产需求（来源：外产_领料）
        /// 按货号分组，计算 需求量之和 - 出库数量之和
        /// </summary>
        private async Task<Dictionary<string, ProductionDemand>> GetProductionDemandFromPickMaterialBatchAsync(List<string> itemNos)
        {
            if (itemNos == null || itemNos.Count == 0)
            {
                return new Dictionary<string, ProductionDemand>();
            }

            static double ParseDouble(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return 0d;
                return double.TryParse(value, out var d) ? d : 0d;
            }

            var list = await _context.外产_领料
                .AsNoTracking()
                .Where(e => itemNos.Contains(e.货号) && !string.IsNullOrEmpty(e.货号))
                .ToListAsync();

            return list
                .Where(e => !string.IsNullOrEmpty(e.货号))
                .GroupBy(e => e.货号!)
                .ToDictionary(g => g.Key, g => new ProductionDemand
                {
                    货号 = g.Key,
                    在产需求 = g.Sum(e => ParseDouble(e.需求量)) - g.Sum(e => ParseDouble(e.出库数量))
                });
        }

        #endregion




        #region 工单销控表

        /// <summary>
        /// 批量添加或更新工单销控表数据（存在则覆盖，不存在则新增）
        /// </summary>
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
                    // 货号已存在：工单总数累加（先读原值，再覆盖）
                    var newTotal = ParseDouble(existing.工单总数) + ParseDouble(item.工单总数);
                    // 保留原创建时间；若原为空则用当前时间回填
                    item.创建时间 = string.IsNullOrWhiteSpace(existing.创建时间) ? now : existing.创建时间;

                    // 仅复制 item 中非空的字段，避免 SetValues 把未传字段清空
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

        /// <summary>
        /// 根据货号查询工单销控表列表
        /// </summary>
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

        /// <summary>
        /// 批量删除工单销控表数据
        /// </summary>
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

        /// <summary>
        /// 批量添加或更新工单销控表明细数据（存在则覆盖，不存在则新增）
        /// </summary>
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

            // 查询已存在的记录（按货号拉取，再在内存中按 货号+分析单号 判定）
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
                    // 更新已有记录：先对齐主键编号，再 SetValues
                    item.编号 = existing.编号;
                    // 保留原创建时间；若原为空则用当前时间回填
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

        /// <summary>
        /// 根据货号查询工单销控表明细列表
        /// </summary>
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

        /// <summary>
        /// 批量删除工单销控表明细数据
        /// </summary>
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

        #region 外产发运

        /// <summary>
        /// 批量添加或更新外产发运数据（存在则覆盖，不存在则新增）
        /// </summary>
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

            // 按 (分析单号, 货号) 一次性查出已存在记录，建立匹配索引
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
                    // 货号和分析单号一致：更新已有数据
                    item.编号 = existing.编号;
                    // 保留原创建时间；若原为空则用当前时间回填
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

        /// <summary>
        /// 根据货号查询外产发运列表
        /// </summary>
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

        /// <summary>
        /// 批量删除外产发运数据
        /// </summary>
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

        /// <summary>
        /// 批量添加或更新外产领料数据（存在则覆盖，不存在则新增）
        /// </summary>
        public async Task<List<ExternalProductionPickMaterial>> AddOrUpdateExternalProductionPickMaterialList(List<ExternalProductionPickMaterial> list)
        {
            if (list == null || list.Count == 0)
            {
                throw new ArgumentException("外产领料数据不能为空", nameof(list));
            }

            var result = new List<ExternalProductionPickMaterial>();
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 收集用于查询已存在记录的编号（编号唯一，外产_领料 以编号关联）
            var keys = list
                .Where(x => !string.IsNullOrWhiteSpace(x.编号))
                .Select(x => x.编号!)
                .Distinct()
                .ToList();

            // 按编号批量拉取已存在记录（EF Core 可翻译）
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
                // 编号存在：更新已存在记录的出库数量（需求量不在后端计算）
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

        /// <summary>
        /// 根据货号查询外产领料列表
        /// </summary>
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

        /// <summary>
        /// 批量删除外产领料数据
        /// </summary>
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

        #region 外产入库

        /// <summary>
        /// 批量添加或更新外产入库数据（存在则覆盖，不存在则新增）
        /// </summary>
        public async Task<List<ExternalProductionWarehousing>> AddOrUpdateExternalProductionWarehousingList(List<ExternalProductionWarehousing> list)
        {
            if (list == null || list.Count == 0)
            {
                throw new ArgumentException("外产入库数据不能为空", nameof(list));
            }
            var result = new List<ExternalProductionWarehousing>();
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 收集用于查询已存在记录的编号（编号唯一，外产_入库 与 工单销控表明细 均以编号关联）
            var keys = list
                .Where(x => !string.IsNullOrWhiteSpace(x.编号))
                .Select(x => x.编号!)
                .Distinct()
                .ToList();

            // 按编号批量拉取已存在记录（EF Core 可翻译）
            var existingItems = new List<ExternalProductionWarehousing>();
            var workOrderDetailDict = new Dictionary<string, WorkOrderSalesControlDetail>();
            if (keys.Count > 0)
            {
                existingItems = await _context.外产_入库
                    .Where(x => keys.Contains(x.编号))
                    .ToListAsync();

                // 同步拉取对应的工单销控表明细（按编号匹配），用于同步入库数
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

            // 记录受影响的明细父级编号，用于主表汇总
            var affectedParentNos = new HashSet<string>();

            foreach (var item in list)
            {
                // 编号存在，且入库数量>0：更新已存在记录的入库数量
                if (!string.IsNullOrWhiteSpace(item.编号)
                    && ParseDouble(item.入库数量) > 0
                    && existingDict.TryGetValue(item.编号!, out var existing))
                {
                    ApplyClientRowVersion(existing, item.RowVersion);
                    // 原创建时间为空则回填当前时间
                    existing.创建时间 = string.IsNullOrWhiteSpace(existing.创建时间) ? now : existing.创建时间;
                    // 同步更新工单销控表明细的入库数（按编号匹配）；入库数>=生产数则删除该明细
                    if (workOrderDetailDict.TryGetValue(item.编号!, out var detail))
                    {
                        detail.入库数 = item.入库数量;
                        // 实时更新待产数 = 生产数 - 入库数
                        detail.待产数 = (ParseDouble(detail.生产数) - ParseDouble(detail.入库数)).ToString();
                        if (!string.IsNullOrWhiteSpace(detail.父级编号))
                        {
                            affectedParentNos.Add(detail.父级编号!);
                        }
                        // 入库数>=生产数 时暂不删除明细
                    }

                    // 入库数量 >= 需求数量：订单已满足，直接删除该记录
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
                    if (string.IsNullOrWhiteSpace(item.编号)||string.IsNullOrWhiteSpace(item.货号))
                    {
                        throw new ArgumentException("外产入库数据中的编号或者货号不能为空", nameof(item));
                    }

                    // 同步更新工单销控表明细的入库数（按编号匹配）
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

            // 联动更新工单销控表主表：按父级编号汇总明细入库数 -> 已入库数；在产数量 = 工单总数 - 已入库数
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

        /// <summary>
        /// 将字符串安全解析为 double（空或无法解析时返回 0）
        /// </summary>
        private static double ParseDouble(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0d;
            return double.TryParse(value, out var d) ? d : 0d;
        }

        private void ApplyClientRowVersion<TEntity>(TEntity trackedEntity, byte[]? clientRowVersion)
            where TEntity : class
        {
            if (clientRowVersion is { Length: > 0 })
            {
                _context.Entry(trackedEntity).Property("RowVersion").OriginalValue = clientRowVersion;
            }
        }

        /// <summary>
        /// 根据货号查询外产入库列表
        /// </summary>
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

        /// <summary>
        /// 批量删除外产入库数据
        /// </summary>
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


        #region 外产生产

        /// <summary>
        /// 批量添加或更新外产生产数据（存在则覆盖，不存在则新增）
        /// </summary>
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

            // 按 (分析单号, 货号) 一次性查出已存在记录，建立匹配索引
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
                    // 货号和分析单号一致：更新已有数据
                    item.编号 = existing.编号;
                    // 保留原创建时间；若原为空则用当前时间回填
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
                    await _context.外产_生产.AddAsync(item);
                    result.Add(item);
                }
            }
           

            await _context.SaveChangesAsync();
            return result;
        }

        /// <summary>
        /// 根据货号查询外产生产列表
        /// </summary>
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

        /// <summary>
        /// 根据编号查询单条外产生产数据
        /// </summary>
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

        /// <summary>
        /// 批量删除外产生产数据
        /// </summary>
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

     
        #region 外产BOM

        // public async Task<List<ExternalProductionBOM>> SaveExternalProductionBOM(string? itemNo)
        // {
        //     var bomRecords = await GetExternalProductionBOM(itemNo);
        //     await _context.外产_BOM.AddRangeAsync(bomRecords);
        //     await _context.SaveChangesAsync();
        //     return bomRecords;
        // }

        public async Task<List<ExternalProductionBOM>> SaveExternalProductionBOM(
            List<ExternalProductionBOM> bomList, string username, string schedulingNo)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                await _context.Database.ExecuteSqlRawAsync(
                    "DECLARE @lockResult int; " +
                    "EXEC @lockResult = sp_getapplock @Resource = N'PMC:BOM:AtomicNumber', " +
                    "@LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000; " +
                    "IF @lockResult < 0 RAISERROR(N'无法获得BOM原子取号锁，请稍后重试。', 16, 1);");

                var result = await SaveExternalProductionBOMCore(bomList, username, schedulingNo);
                await transaction.CommitAsync();
                return result;
            });
        }

        private async Task<List<ExternalProductionBOM>> SaveExternalProductionBOMCore(
            List<ExternalProductionBOM> bomList, string username, string schedulingNo)
        {
            if (bomList == null || bomList.Count == 0 || string.IsNullOrEmpty(schedulingNo))
            {
                return new List<ExternalProductionBOM>();
            }

            //默认
            username = "GLY[管理员]";
            string userid = "USR01522";
            // 根据传入的用户名和排产编号构造参数，调用 SaveSchedulingAnalysisAsync 获取排产分析单号
            var deliveryReview = new PMCDeliveryReview
            {
                排产用户 = username,
                排产编号 = schedulingNo,
            };
            var scheduling = await SaveSchedulingAnalysisAsync(deliveryReview);
            string? analysisNo = scheduling?.分析单号;

            // 将传入的用户名、排产编号与排产分析单号同步到每条 BOM 记录
            bomList.ForEach(item => item.分析单号 = analysisNo);

            // 1）确定每条记录的最终编号，全部作为新增，并建立 {层}_{货号} -> 编号 映射（供父级重映射使用）
            const string bomTableName = "外产_BOM";
            // 循环前预加载一次被跟踪的控制ID记录，循环内复用（内存自增，末尾统一提交）
            var controlId = await _erpBaseService.GetControlIdTrackedAsync(userid, bomTableName);
            var itemNoToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var newItems = new List<ExternalProductionBOM>();

            foreach (var item in bomList)
            {
                if (string.IsNullOrWhiteSpace(item.货号))
                {
                    continue;
                }

                // 全部作为新增：基于预加载的控制ID记录生成新编号（内存自增）
                var newId = _erpBaseService.GenerateCodeFromRecord(controlId, userid) ?? Guid.NewGuid().ToString();
                item.编号 = newId;
                item.创建时间 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                newItems.Add(item);
                if (!string.IsNullOrWhiteSpace(item.层))
                    itemNoToId[$"{item.层}_{item.货号}"] = newId;
            }

            // 3）根据层级关系：父级编号原值为父级货号，替换为对应的父级 GUID
            foreach (var item in bomList)
            {
                if (!string.IsNullOrWhiteSpace(item.父级编号) && !string.IsNullOrWhiteSpace(item.层))
                {
                    if (int.TryParse(item.层, out int level) && level > 0)
                    {
                        string parentKey = $"{level - 1}_{item.父级编号}";
                        if (itemNoToId.TryGetValue(parentKey, out var parentId))
                        {
                            item.父级编号 = parentId;
                        }
                    }
                }
            }

            // 4）新记录：批量新增
            if (newItems.Count > 0)
            {
                await _context.外产_BOM.AddRangeAsync(newItems);
            }

            await _context.SaveChangesAsync();

            return bomList;
        }

        /// <summary>
        /// 查询父级编号关联的外产BOM列表
        /// </summary>
        public async Task<PagedResult<ExternalProductionBOM>> GetExternalProductionBOMList(
            PMCRequestDto request, CancellationToken cancellationToken = default)
        {
            var query = _context.外产_BOM.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.编号)) query = query.Where(e => e.编号 == request.编号);

            if (!string.IsNullOrWhiteSpace(request.货号))
            {
                var parentId = await query.Where(e => e.货号 == request.货号)
                    .Select(e => e.编号)
                    .FirstOrDefaultAsync(cancellationToken);
                if (parentId == null)
                {
                    return new PagedResult<ExternalProductionBOM>
                    {
                        Items = Array.Empty<ExternalProductionBOM>(), Total = 0,
                        Page = request.Page, PageSize = request.PageSize
                    };
                }
                query = query.Where(e => e.父级编号 == parentId);
            }
            if (!string.IsNullOrWhiteSpace(request.分析单号)) query = query.Where(e => e.分析单号 == request.分析单号);
            return await query.OrderByDescending(e => e.创建时间).ThenBy(e => e.编号)
                .ToPagedResultAsync(request, cancellationToken);
        }

        /// <summary>
        /// 批量删除外产BOM数据
        /// </summary>
        public async Task DeleteExternalProductionBOMList(List<string> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                throw new ArgumentException("删除列表不能为空", nameof(ids));
            }

            var items = await _context.外产_BOM
                .Where(x => ids.Contains(x.编号))
                .ToListAsync();

            if (items.Count > 0)
            {
                _context.外产_BOM.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
        }



        public async Task<List<ExternalProductionBOM>> GetBomByItemNo(string? itemNo)
        {
            var empty = new List<ExternalProductionBOM>();

            if (string.IsNullOrWhiteSpace(itemNo))
                return empty;

            // 必须是带括号的成品货号格式，否则返回空
            int bracketIndex = itemNo.IndexOf('(');
            int closeBracketIndex = itemNo.IndexOf(')');
            if (bracketIndex <= 0 || closeBracketIndex <= bracketIndex)
                return empty;

            // 去掉括号内容，得到用于查询装配结构的基础货号
            string baseItemNo = itemNo.Substring(0, bracketIndex).Trim();
            if (string.IsNullOrWhiteSpace(baseItemNo))
                return empty;

            // 提取括号内的内容作为线圈货号
            string coilItemNo = itemNo.Substring(bracketIndex + 1, closeBracketIndex - bracketIndex - 1).Trim();

            // 最终写入「外产_BOM」的扁平记录集合
            var bomRecords = new List<ExternalProductionBOM>();

            // 1）成品节点：带括号的完整货号，level=0，作为顶层
            var finishedBom = new ExternalProductionBOM
            {
                编号 = Guid.NewGuid().ToString(),
                货号 = itemNo,
                层 = "0",
                关联编号 = null,
                父级编号 = null
            };
            bomRecords.Add(finishedBom);
            string finishedBomId = finishedBom.编号;

            // 2）线圈货号节点：括号里的内容，level=1，与半成品平级，父级=成品节点
            if (!string.IsNullOrWhiteSpace(coilItemNo))
            {
                bomRecords.Add(new ExternalProductionBOM
                {
                    编号 = Guid.NewGuid().ToString(),
                    货号 = coilItemNo,
                    层 = "1",
                    关联编号 = null,
                    父级编号 = finishedBomId
                });
            }

            // ============ 查找半成品货号 ============
            // 在装配链中向下找，直到遇到"多条清单"的节点即为半成品
            string? semiItemNo = await FindSemiFinishedItemNo(baseItemNo);
            if (semiItemNo == null)
            {
                return bomRecords.Count == 0 ? empty : bomRecords;
            }

            // 3）半成品本身：level=1，父级=成品节点
            var semiBom = new ExternalProductionBOM
            {
                编号 = Guid.NewGuid().ToString(),
                货号 = semiItemNo,
                层 = "1",
                关联编号 = null,
                父级编号 = finishedBomId
            };
            bomRecords.Add(semiBom);
            string semiBomId = semiBom.编号;

            // 4）递归展开半成品的子级（level=2 起）：遇到自制/外协则继续深入
            await ExpandAssemblyChildrenRecursive(semiItemNo, semiBomId, startLevel: 2, bomRecords,
                visitedItemNos: new HashSet<string> { semiItemNo });

            return bomRecords.Count == 0 ? empty : bomRecords;
        }

        /// <summary>
        /// 在装配链中向下查找"半成品货号"节点。
        /// 规则：沿"只有一条清单"的节点向下推进，遇到"多条清单"的节点即为半成品。
        /// 未找到则返回 null。
        /// </summary>
        private async Task<string?> FindSemiFinishedItemNo(string itemNo)
        {
            const int maxDepth = 50;
            string currentItemNo = itemNo;
            int currentDepth = 0;

            while (currentDepth < maxDepth)
            {
                currentDepth++;

                var assemblyData = await _context.产品资料装配
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.货号 == currentItemNo);

                if (assemblyData == null || string.IsNullOrWhiteSpace(assemblyData.编号))
                    return null;

                var assemblyList = await _context.产品资料装配清单
                    .AsNoTracking()
                    .Where(e => e.主编号 == assemblyData.编号)
                    .ToListAsync();

                if (assemblyList == null || assemblyList.Count == 0)
                    return null;

                if (assemblyList.Count == 1)
                {
                    var onlyItem = assemblyList[0];
                    if (string.IsNullOrWhiteSpace(onlyItem.货号))
                        return null;

                    currentItemNo = onlyItem.货号!;
                    continue;
                }

                // 多条清单 → 当前货号即为半成品
                return currentItemNo;
            }

            return null;
        }

        /// <summary>
        /// 递归展开装配子级。
        /// 逻辑：查询指定货号的装配清单 → 批量查询制造方式 → 写入当前层 →
        ///       对制造方式为"自制/外协"的子项继续递归深入。
        /// </summary>
        private async Task ExpandAssemblyChildrenRecursive(
            string itemNo,
            string parentId,
            int startLevel,
            List<ExternalProductionBOM> bomRecords,
            HashSet<string> visitedItemNos)
        {
            const int maxDepth = 50;
            if (startLevel > maxDepth)
                return;

            // 获取当前货号的装配清单
            var assemblyData = await _context.产品资料装配
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.货号 == itemNo);

            if (assemblyData == null || string.IsNullOrWhiteSpace(assemblyData.编号))
                return;

            var assemblyList = await _context.产品资料装配清单
                .AsNoTracking()
                .Where(e => e.主编号 == assemblyData.编号)
                .ToListAsync();

            if (assemblyList == null || assemblyList.Count == 0)
                return;

            // 批量查询子级货号的"制造方式"，用于判断是否需要继续递归
            var childItemNos = assemblyList
                .Where(a => !string.IsNullOrWhiteSpace(a.货号))
                .Select(a => a.货号!)
                .Distinct()
                .ToList();

            var sourceDict = new Dictionary<string, string>();
            if (childItemNos.Count > 0)
            {
                var childProducts = await _context.产品资料
                    .AsNoTracking()
                    .Where(e => childItemNos.Contains(e.货号))
                    .Select(e => new { e.货号, e.制造方式 })
                    .ToListAsync();

                foreach (var pd in childProducts)
                {
                    if (!string.IsNullOrEmpty(pd.货号))
                        sourceDict[pd.货号] = pd.制造方式 ?? "";
                }
            }

            // 写入当前层的所有子项，并对需要递归的子项继续深入
            foreach (var child in assemblyList)
            {
                if (string.IsNullOrWhiteSpace(child.货号))
                    continue;

                var childBom = new ExternalProductionBOM
                {
                    编号 = Guid.NewGuid().ToString(),
                    货号 = child.货号,
                    层 = startLevel.ToString(),
                    关联编号 = child.编号,
                    父级编号 = parentId
                };
                bomRecords.Add(childBom);

                // 判断制造方式：自制或外协 → 继续递归（且防止循环引用）
                if (sourceDict.TryGetValue(child.货号!, out var source)
                    && !string.IsNullOrEmpty(source)
                    && (source == "自制" || source == "外协"))
                {
                    if (visitedItemNos.Add(child.货号!))
                    {
                        await ExpandAssemblyChildrenRecursive(
                            child.货号!,
                            childBom.编号,
                            startLevel + 1,
                            bomRecords,
                            visitedItemNos);
                    }
                }
            }
        }






        #endregion


        // 获取合同状态
        public async Task<PMCBasicInfo> GetContractStatus(string num)
        {
            if (string.IsNullOrEmpty(num))
            {
                Console.WriteLine("合同号不能为空。");
                return new PMCBasicInfo();
            }

            PMCBasicInfo contract = new();
            List<PMCBasicInfo> contracts = await _context.外销合同基本信息
                 .AsNoTracking()
                 .Where(e => e.合同号 == num).ToListAsync();


            if (contracts.Count > 0)
            {
                contract = contracts[0];
                Console.WriteLine($"合同号: {contract.合同号}, 合同状态: {contract.合同状态}");
            }
            else
            {
                Console.WriteLine("未找到对应的合同信息。");
            }
            return contract;
        }




        // 获取产品资料装配清单
        public async Task<List<ProductDataAssemblyList>> GetProductDataAssemblyList(string? itemNo)
        {
            if (string.IsNullOrWhiteSpace(itemNo))
            {
                return new List<ProductDataAssemblyList>();
            }


            // 获取产品资料装配信息
            ProductDataAssembly? productData = await _context.产品资料装配
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.货号 == itemNo);

            if (productData == null)
            {
                return new List<ProductDataAssemblyList>();
            }

            List<ProductDataAssemblyList> productDataList = await _context.产品资料装配清单
                .AsNoTracking()
                .Where(e => e.主编号 == productData.编号)
                .ToListAsync();

            return productDataList;
        }

        // 根据货号匹配产品资料装配清单，返回货号一致且中间件字段等于 0 的记录
        public async Task<List<ProductDataAssemblyList>> GetProductDataAssemblyListByItemNo(string? itemNo)
        {
            if (string.IsNullOrWhiteSpace(itemNo))
            {
                return new List<ProductDataAssemblyList>();
            }

            List<ProductDataAssemblyList> productDataList = await _context.产品资料装配清单
                .AsNoTracking()
                .Where(e => e.货号 == itemNo && e.中间件 == "0")
                .ToListAsync();

            return productDataList;
        }

        // 获取产品资料装配信息
        public async Task<ProductDataAssembly> GetProductDataAssembly(string itemNo)
        {

            var productData = await _context.产品资料装配
                   .AsNoTracking()
                   .FirstOrDefaultAsync(e => e.货号 == itemNo);
            return productData;
        }

        // 获取产品资料
        public async Task<ProductData?> GetProductData(string? itemNo)
        {
            if (string.IsNullOrWhiteSpace(itemNo))
            {
                return null;
            }
            var productData = await _context.产品资料
                   .AsNoTracking()
                   .FirstOrDefaultAsync(e => e.货号 == itemNo);
            return productData;
        }

        // 校验线圈货号
        public async Task<bool> SearchCoils(string? keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }
            var query = _context.产品资料.AsNoTracking()
                .Where(p => !string.IsNullOrEmpty(p.产品类别) && p.产品类别.StartsWith("线圈") && p.停用 != "1")
                .Where(p => p.货号 == keyword);
            if (!await query.AnyAsync())
            {
                return false;
            }
            return true;
        }

        // 按关键字模糊查询产品资料中的线圈（货号包含 keyword 即可），最多返回 50 条
        public async Task<List<ProductData>> SearchCoilsByKeyword(string? keyword)
        {
            var empty = new List<ProductData>();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return empty;
            }

            var query = _context.产品资料.AsNoTracking()
                .Where(p => !string.IsNullOrEmpty(p.产品类别) && p.产品类别.StartsWith("线圈") && p.停用 != "1")
                .Where(p => !string.IsNullOrEmpty(p.货号) && p.货号.Contains(keyword!))
                .OrderBy(p => p.货号)
                .Take(50);

            var list = await query.ToListAsync();
            return list.Count == 0 ? empty : list;
        }

        // 按关键字模糊查询产品资料（不区分线圈，货号包含 keyword 即可），最多返回 50 条
        public async Task<List<ProductData>> SearchProductDataByKeyword(string? keyword)
        {
            var empty = new List<ProductData>();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return empty;
            }

            var query = _context.产品资料.AsNoTracking()
                .Where(p => !string.IsNullOrEmpty(p.货号) && p.货号.Contains(keyword!))
                .Where(p => p.停用 != "1")
                .OrderBy(p => p.货号)
                .Take(50);

            var list = await query.ToListAsync();
            return list.Count == 0 ? empty : list;
        }


        #region BOM结构工序

        /// <summary>
        /// 获取所有BOM结构工序数据
        /// </summary>
        public async Task<List<BOMStructureProcess>> GetBOMStructureProcessList()
        {
            const string cacheKey = "pmc:bom-structure-processes";
            if (_cache.TryGetValue(cacheKey, out List<BOMStructureProcess>? cached) && cached != null)
            {
                return cached;
            }

            var data = await _context.BOM结构工序.AsNoTracking().ToListAsync();
            _cache.Set(cacheKey, data, TimeSpan.FromMinutes(5));
            return data;
        }

        #endregion



        // 定义一个简单的内部结构体，避免频繁解析字符串
        private class IntermediateData
        {
            public string? 合同号 { get; set; }
            public string? 排产编号 { get; set; }
            public string? 货号 { get; set; }
            public string? 父级货号 { get; set; }
            public string? 物料货号 { get; set; }
            public string? 中文品名 { get; set; }
            public string? 中文规格 { get; set; }
            public string? 分析单号 { get; set; }
            public string? 商品属性 { get; set; }
            public string? 关联编号 { get; set; }
            //在产数
            public int 在产数 { get; set; }
            //需求量
            public int 需求量 { get; set; }
            //发运数量
            public int 发运数量 { get; set; }
            //仓库数
            public int 仓库数 { get; set; }
            public string? 排产用户 { get; set; }
            public List<DeliveryPlan>? 交货计划 { get; set; }
        }
    }
}
