using LocalDataApi.Application.Ppc.Contracts;
using LocalDataApi.Dto;
using LocalDataApi.Domain.Ppc;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

using LocalDataApi.Utils;

namespace LocalDataApi.Application.Ppc.Services;

/// <summary>
/// 排产分析用例实现:外产 BOM 扁平记录 → 装配树 → 嵌套排产分析 DTO。
/// </summary>
public class PmcSchedulingService : PpcServiceBase, IPmcSchedulingService
{
    private readonly IPmcBomService _bomService;

    public PmcSchedulingService(AppDbContext context, IPmcBomService bomService)
        : base(context)
    {
        _bomService = bomService;
    }

    /// <summary>获取排产分析列表(嵌套树形结构)</summary>
    public async Task<List<SchedulingAnalysisDto>> GetSchedulingAnalysisList(PMCRequestDto request)
    {
        var result = new List<SchedulingAnalysisDto>();

        // 第一步:获取外产 BOM 扁平列表,再转换成树形结构
        var bomRecords = await _bomService.GetBomByItemNo(request.货号);
        var assemblyTree = await ConvertBomToAssemblyTreeAsync(bomRecords);

        // 第二步:收集所有货号(父 + 所有层级子级)
        var itemNos = new HashSet<string>();
        var processedItemNo = request.货号 ?? string.Empty;
        int bracketIndex = processedItemNo.IndexOf('(');
        if (bracketIndex > 0)
            processedItemNo = processedItemNo.Substring(0, bracketIndex);
        if (!string.IsNullOrEmpty(request.货号))
            itemNos.Add(request.货号);
        if (!string.IsNullOrEmpty(processedItemNo))
            itemNos.Add(processedItemNo);

        CollectAllItemNos(assemblyTree, itemNos);

        // 第三步:批量查询所有数据(只查询一次)
        var productDataDict = await GetProductDataBatchAsync(itemNos.ToList());
        var warehouseGoodsDict = await GetWarehouseGoodsBatchAsync(itemNos.ToList(), request.货号);
        // 在产需求(来源:外产_领料,按货号分组:需求量之和 - 出库数量之和)
        var productionDemandDict = await GetProductionDemandFromPickMaterialBatchAsync(itemNos.ToList());
        // 在途数(来源:外产_入库,按货号分组:需求量之和 - 入库数量之和)
        var inTransitQuantityDict = await GetInTransitQuantityFromWarehousingBatchAsync(itemNos.ToList());
        // 第四步:构建嵌套树形结果
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
    /// 返回的树:顶层为 level=1 的节点(线圈、半成品);
    /// 所有层级的子级通过关联编号查询装配清单补充 用量/来源/单位(支持任意深度)。
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

        // 从 ALL 层级收集所有 关联编号(支持 level >= 2 的深层节点)
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

        // 递归构建树:从顶层(level=1 且 父级是成品节点)开始
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
                // 没有关联编号的节点(线圈、半成品本身),构造一个占位 assembly 以保留货号
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

    /// <summary>递归收集所有货号</summary>
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

    /// <summary>构建单个Dto</summary>
    private SchedulingAnalysisDto BuildDto(
        string? itemNo,
        int level,
        ProductDataAssemblyList? assembly,
        string? 生产数,
        Dictionary<string, ProductData> productDataDict,
        Dictionary<string, WarehouseGoods> warehouseGoodsDict,
        Dictionary<string, ProductionDemand> productionDemandDict,
        Dictionary<string, InTransitQuantity> inTransitQuantityDict)
    {
        productDataDict.TryGetValue(itemNo ?? string.Empty, out var productData);
        warehouseGoodsDict.TryGetValue(itemNo ?? string.Empty, out var goodsData);
        productionDemandDict.TryGetValue(itemNo ?? string.Empty, out var productionDemand);
        inTransitQuantityDict.TryGetValue(itemNo ?? string.Empty, out var inTransit);

        return new SchedulingAnalysisDto
        {
            货号 = itemNo,
            层 = level.ToString(),
            品名 = productData?.中文品名,
            规格 = productData?.中文规格,
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

    /// <summary>递归构建嵌套Dto列表</summary>
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
            var dto = BuildDto(node.Assembly.货号, level, node.Assembly, node.生产数,
                productDataDict, warehouseGoodsDict, productionDemandDict, inTransitQuantityDict);

            // 递归构建子集
            dto.子集 = BuildNestedDtoList(node.Children, level + 1,
                productDataDict, warehouseGoodsDict, productionDemandDict, inTransitQuantityDict);

            result.Add(dto);
        }
        return result;
    }

    /// <summary>批量获取产品资料</summary>
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

        // 使用 GroupBy + First() 处理重复货号
        return productDataList.Where(e => e.货号 != null)
            .GroupBy(e => e.货号!)
            .ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>批量获取仓库货品数据</summary>
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

        // 根节点(货号 == rootItemNo)不参与仓库信息筛选,直接按数量取最大的一条
        var rootDict = warehouseGoodsList
            .Where(e => e.货号 != null && e.货号 == rootItemNo)
            .GroupBy(e => e.货号!)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => ParseDouble(e.数量)).First());

        // 非根节点的记录,按仓库信息筛选(仓库类型=存货仓 且 纳入需求计算=T)
        var otherList = warehouseGoodsList
            .Where(e => e.货号 != rootItemNo)
            .ToList();
        var otherDict = await FilterWarehouseGoodsByWarehouseInfoAsync(otherList);

        // 合并(根节点优先级更高,避免被覆盖)
        var result = new Dictionary<string, WarehouseGoods>(otherDict);
        foreach (var kvp in rootDict)
        {
            result[kvp.Key] = kvp.Value;
        }
        return result;
    }

    /// <summary>根据仓库信息筛选有效的仓库货品数据</summary>
    private async Task<Dictionary<string, WarehouseGoods>> FilterWarehouseGoodsByWarehouseInfoAsync(List<WarehouseGoods> warehouseGoodsList)
    {
        if (warehouseGoodsList == null || warehouseGoodsList.Count == 0)
        {
            return new Dictionary<string, WarehouseGoods>();
        }

        // 1) 收集仓库货品中出现的仓库名
        var warehouseNames = warehouseGoodsList
            .Where(e => !string.IsNullOrWhiteSpace(e.仓库名))
            .Select(e => e.仓库名!)
            .Distinct()
            .ToList();

        // 2) 查询仓库信息,筛选 仓库类型=存货仓 且 纳入需求计算=T 的有效仓库名
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

        // 3) 只保留仓库名在有效集合中的仓库货品
        var filtered = warehouseGoodsList
            .Where(e => !string.IsNullOrWhiteSpace(e.仓库名) && validWarehouseNames.Contains(e.仓库名!))
            .ToList();

        // 4) 按货号分组,每组取数量最大的一条
        return filtered.Where(e => e.货号 != null)
            .GroupBy(e => e.货号!)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => ParseDouble(e.数量)).First());
    }

    /// <summary>批量获取在途数(来源:外产_入库,需求量之和 - 入库数量之和)</summary>
    private async Task<Dictionary<string, InTransitQuantity>> GetInTransitQuantityFromWarehousingBatchAsync(List<string> itemNos)
    {
        if (itemNos == null || itemNos.Count == 0)
        {
            return new Dictionary<string, InTransitQuantity>();
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

    /// <summary>批量获取在产需求(来源:外产_领料,需求量之和 - 出库数量之和)</summary>
    private async Task<Dictionary<string, ProductionDemand>> GetProductionDemandFromPickMaterialBatchAsync(List<string> itemNos)
    {
        if (itemNos == null || itemNos.Count == 0)
        {
            return new Dictionary<string, ProductionDemand>();
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

    /// <summary>装配节点(用于构建树形结构)</summary>
    private class AssemblyNode
    {
        public ProductDataAssemblyList Assembly { get; set; } = null!;
        public string? 生产数 { get; set; }
        public List<AssemblyNode>? Children { get; set; }
    }
}
