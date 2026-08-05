using LocalDataApi.Application.Erp;
using LocalDataApi.Application.Ppc.Contracts;
using LocalDataApi.Dto;
using LocalDataApi.Domain.Ppc;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using LocalDataApi.Utils;

namespace LocalDataApi.Application.Ppc.Services;

/// <summary>
/// 外产 BOM 用例实现。
/// 含"排产分析单号生成"(SaveSchedulingAnalysisAsync 系列),因为 BOM 保存必须先取分析单号,
/// 且排产分析服务会反向依赖 GetBomByItemNo,二者放同一服务避免循环依赖。
/// </summary>
public class PmcBomService : PpcServiceBase, IPmcBomService
{
    private readonly ERPBaseService _erpBaseService;
    private readonly IMemoryCache _cache;

    public PmcBomService(AppDbContext context, ERPBaseService erpBaseService, IMemoryCache cache)
        : base(context)
    {
        _erpBaseService = erpBaseService;
        _cache = cache;
    }

    /// <summary>根据成品货号生成并保存外产BOM结构(事务 + sp_getapplock 原子取号)</summary>
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
                "IF @lockResult < 0 RAISERROR(N'无法获得BOM原子取号锁,请稍后重试。', 16, 1);");

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
        // 根据传入的用户名和排产编号构造参数,调用 SaveSchedulingAnalysisAsync 获取排产分析单号
        var deliveryReview = new PMCDeliveryReview
        {
            排产用户 = username,
            排产编号 = schedulingNo,
        };
        var scheduling = await SaveSchedulingAnalysisAsync(deliveryReview);
        string? analysisNo = scheduling?.分析单号;

        // 将传入的用户名、排产编号与排产分析单号同步到每条 BOM 记录
        bomList.ForEach(item => item.分析单号 = analysisNo);

        // 1) 确定每条记录的最终编号,全部作为新增,并建立 {层}_{货号} -> 编号 映射(供父级重映射使用)
        const string bomTableName = "外产_BOM";
        // 循环前预加载一次被跟踪的控制ID记录,循环内复用(内存自增,末尾统一提交)
        var controlId = await _erpBaseService.GetControlIdTrackedAsync(userid, bomTableName);
        var itemNoToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var newItems = new List<ExternalProductionBOM>();

        foreach (var item in bomList)
        {
            if (string.IsNullOrWhiteSpace(item.货号))
            {
                continue;
            }

            // 全部作为新增:基于预加载的控制ID记录生成新编号(内存自增)
            var newId = _erpBaseService.GenerateCodeFromRecord(controlId, userid) ?? Guid.NewGuid().ToString();
            item.编号 = newId;
            item.创建时间 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            newItems.Add(item);
            if (!string.IsNullOrWhiteSpace(item.层))
                itemNoToId[$"{item.层}_{item.货号}"] = newId;
        }

        // 2) 根据层级关系:父级编号原值为父级货号,替换为对应的父级 GUID
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

        // 3) 新记录:批量新增
        if (newItems.Count > 0)
        {
            await _context.外产_BOM.AddRangeAsync(newItems);
        }

        await _context.SaveChangesAsync();

        return bomList;
    }

    /// <summary>查询父级编号关联的外产BOM列表(分页)</summary>
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

    /// <summary>批量删除外产BOM数据</summary>
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

    /// <summary>获取所有BOM结构工序数据(带缓存)</summary>
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

    /// <summary>根据成品货号获取外产 BOM 扁平记录</summary>
    public async Task<List<ExternalProductionBOM>> GetBomByItemNo(string? itemNo)
    {
        var empty = new List<ExternalProductionBOM>();

        if (string.IsNullOrWhiteSpace(itemNo))
            return empty;

        // 必须是带括号的成品货号格式,否则返回空
        int bracketIndex = itemNo.IndexOf('(');
        int closeBracketIndex = itemNo.IndexOf(')');
        if (bracketIndex <= 0 || closeBracketIndex <= bracketIndex)
            return empty;

        // 去掉括号内容,得到用于查询装配结构的基础货号
        string baseItemNo = itemNo.Substring(0, bracketIndex).Trim();
        if (string.IsNullOrWhiteSpace(baseItemNo))
            return empty;

        // 提取括号内的内容作为线圈货号
        string coilItemNo = itemNo.Substring(bracketIndex + 1, closeBracketIndex - bracketIndex - 1).Trim();

        // 最终写入「外产_BOM」的扁平记录集合
        var bomRecords = new List<ExternalProductionBOM>();

        // 1) 成品节点:带括号的完整货号,level=0,作为顶层
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

        // 2) 线圈货号节点:括号里的内容,level=1,与半成品平级,父级=成品节点
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
        // 在装配链中向下找,直到遇到"多条清单"的节点即为半成品
        string? semiItemNo = await FindSemiFinishedItemNo(baseItemNo);
        if (semiItemNo == null)
        {
            return bomRecords.Count == 0 ? empty : bomRecords;
        }

        // 3) 半成品本身:level=1,父级=成品节点
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

        // 4) 递归展开半成品的子级(level=2 起):遇到自制/外协则继续深入
        await ExpandAssemblyChildrenRecursive(semiItemNo, semiBomId, startLevel: 2, bomRecords,
            visitedItemNos: new HashSet<string> { semiItemNo });

        return bomRecords.Count == 0 ? empty : bomRecords;
    }

    /// <summary>在装配链中向下查找"半成品货号"节点</summary>
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

    /// <summary>递归展开装配子级</summary>
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

        // 批量查询子级货号的"制造方式",用于判断是否需要继续递归
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

        // 写入当前层的所有子项,并对需要递归的子项继续深入
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

            // 判断制造方式:自制或外协 → 继续递归(且防止循环引用)
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

    #region 排产分析单号生成(供 BOM 保存使用)

    /// <summary>根据排产用户生成分析单号</summary>
    private async Task<string> GenerateAnalysisOrderNumberAsync(string productionUser)
    {
        // 1. 提取排产用户代码(取 '[' 之前的部分)
        string userCode = productionUser?.Split('[')[0] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userCode))
        {
            throw new ArgumentException("排产用户格式不正确,无法提取代码", nameof(productionUser));
        }

        // 2. 当前年月,格式 yyMM(如 2603)
        string yearMonth = DateTime.Now.ToString("yyMM");

        // 3. 构造前缀:PC + 用户代码 + 年月
        string prefix = $"PC{userCode}{yearMonth}";

        // 4. 查询数据库中最大的流水码(即最后4位数字)
        var existingNumbers = await _context.排产分析单
            .Where(x => x.分析单号.StartsWith(prefix))
            .Select(x => x.分析单号)
            .ToListAsync();

        int maxSerial = 0;
        foreach (var number in existingNumbers)
        {
            string suffix = number.Substring(prefix.Length);

            if (int.TryParse(suffix, out int serial) && serial > maxSerial)
            {
                maxSerial = serial;
            }
        }
        int newSerial = maxSerial + 1;
        string serialStr = newSerial.ToString("D4");
        return $"{prefix}{serialStr}";
    }

    /// <summary>根据排产用户获取用户编号</summary>
    private async Task<string> GetUserNumberAsync(string productionUser)
    {
        // 1. 提取排产用户代码(取 '[' 之前的部分)
        string userCode = productionUser?.Split('[')[0] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userCode))
        {
            throw new ArgumentException("排产用户格式不正确,无法提取代码", nameof(productionUser));
        }
        var userNumber = await _context.tb_control_user
           .Where(x => x.usercode == userCode)
           .Select(x => x.ID).FirstOrDefaultAsync();
        return userNumber.Trim();
    }

    /// <summary>从外产_订单中查找对应货号的数据并保存排产分析单</summary>
    public async Task<SchedulingAnalysis?> SaveSchedulingAnalysisAsync(PMCRequestDto request)
    {
        var deliveryReview = await _context.外产_订单
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.货号 == request.货号);

        if (deliveryReview != null)
        {
            return await SaveSchedulingAnalysisAsync(deliveryReview);
        }

        return null;
    }

    /// <summary>根据排产用户和分析单号保存排产分析单信息</summary>
    public async Task<SchedulingAnalysis> SaveSchedulingAnalysisAsync(PMCDeliveryReview deliveryReview)
    {
        string nowss = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string nowdd = DateTime.Now.ToString("yyyy-MM-dd");
        string productionUser = deliveryReview.排产用户 ?? string.Empty;
        string userNum = await GetUserNumberAsync(productionUser);
        var analysisNum = await GenerateAnalysisOrderNumberAsync(productionUser);
        string prefix = $"{userNum}UNT";
        var existingNumbers = await _context.排产分析单
            .Where(x => x.编号.StartsWith(prefix))
            .Select(x => x.编号)
            .ToListAsync();

        int maxSerial = 0;
        foreach (var number in existingNumbers)
        {
            string suffix = number.Substring(prefix.Length);

            if (int.TryParse(suffix, out int serial) && serial > maxSerial)
            {
                maxSerial = serial;
            }
        }

        // 新流水码 = 最大值 + 1,补零到5位
        int newSerial = maxSerial + 1;
        string serialStr = newSerial.ToString("D5");
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
        await _context.排产分析单.AddAsync(scheduling);
        return scheduling;
    }

    #endregion
}
