using LocalDataApi.Dto;
using LocalDataApi.Domain.Ppc;

namespace LocalDataApi.Application.Ppc.Contracts;

/// <summary>
/// 产品资料与装配清单查询用例。
/// </summary>
public interface IPmcProductService
{
    /// <summary>获取PMC产品信息列表(按条件分页)</summary>
    Task<PagedResult<PMCProductInfo>> GetPMCProductListInfo(PMCRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>根据货号获取产品资料装配清单</summary>
    Task<List<ProductDataAssemblyList>> GetProductDataAssemblyList(string? itemNo);

    /// <summary>根据货号获取产品资料装配清单中中间件等于 0 的记录</summary>
    Task<List<ProductDataAssemblyList>> GetProductDataAssemblyListByItemNo(string? itemNo);

    /// <summary>获取产品资料</summary>
    Task<ProductData?> GetProductData(string? itemNo);

    /// <summary>校验线圈货号是否存在于装配清单中</summary>
    Task<bool> SearchCoils(string? keyword);

    /// <summary>按关键字模糊查询产品资料中的线圈(货号包含关键字即可),最多返回 50 条</summary>
    Task<List<ProductData>> SearchCoilsByKeyword(string? keyword);

    /// <summary>按货号模糊查询产品资料(不区分线圈),最多返回 50 条</summary>
    Task<List<ProductData>> SearchProductDataByKeyword(string? keyword);

    /// <summary>获取合同状态</summary>
    Task<PMCBasicInfo> GetContractStatus(string num);
}
