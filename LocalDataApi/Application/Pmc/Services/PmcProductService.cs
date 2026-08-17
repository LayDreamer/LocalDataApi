using LocalDataApi.Application.Pmc.Contracts;
using LocalDataApi.Dto;
using LocalDataApi.Domain.Pmc;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

using LocalDataApi.Utils;

namespace LocalDataApi.Application.Pmc.Services;

/// <summary>
/// 产品资料与装配清单查询用例实现。
/// </summary>
public class PmcProductService : PmcServiceBase, IPmcProductService
{
    public PmcProductService(AppDbContext context) : base(context)
    {
    }

    /// <summary>获取外销合同产品列表(按条件分页)</summary>
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

    /// <summary>获取产品资料装配清单</summary>
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

        return await _context.产品资料装配清单
            .AsNoTracking()
            .Where(e => e.主编号 == productData.编号)
            .ToListAsync();
    }

    /// <summary>根据货号匹配产品资料装配清单,返回货号一致且中间件字段等于 0 的记录</summary>
    public async Task<List<ProductDataAssemblyList>> GetProductDataAssemblyListByItemNo(string? itemNo)
    {
        if (string.IsNullOrWhiteSpace(itemNo))
        {
            return new List<ProductDataAssemblyList>();
        }

        return await _context.产品资料装配清单
            .AsNoTracking()
            .Where(e => e.货号 == itemNo && e.中间件 == "0")
            .ToListAsync();
    }

    /// <summary>获取产品资料</summary>
    public async Task<ProductData?> GetProductData(string? itemNo)
    {
        if (string.IsNullOrWhiteSpace(itemNo))
        {
            return null;
        }
        return await _context.产品资料
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.货号 == itemNo);
    }

    /// <summary>校验线圈货号是否存在</summary>
    public async Task<bool> SearchCoils(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }
        var query = _context.产品资料.AsNoTracking()
            .Where(p => !string.IsNullOrEmpty(p.产品类别) && p.产品类别.StartsWith("线圈") && p.停用 != "1")
            .Where(p => p.货号 == keyword);
        return await query.AnyAsync();
    }

    /// <summary>按关键字模糊查询产品资料中的线圈,最多返回 50 条</summary>
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

    /// <summary>按关键字模糊查询产品资料(不区分线圈),最多返回 50 条</summary>
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

    /// <summary>获取合同状态</summary>
    public async Task<PMCBasicInfo> GetContractStatus(string num)
    {
        if (string.IsNullOrEmpty(num))
        {
            return new PMCBasicInfo();
        }

        PMCBasicInfo contract = new();
        List<PMCBasicInfo> contracts = await _context.外销合同基本信息
            .AsNoTracking()
            .Where(e => e.合同号 == num).ToListAsync();

        if (contracts.Count > 0)
        {
            contract = contracts[0];
        }
        return contract;
    }
}
