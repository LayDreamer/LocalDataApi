using LocalDataApi.Dto;
using LocalDataApi.Domain.Ppc;

namespace LocalDataApi.Application.Ppc.Contracts;

/// <summary>
/// 交期评审用例:外销合同客户产品 → 交期评审、生产类型覆盖、退回待评审。
/// </summary>
public interface IPmcDeliveryReviewService
{
    /// <summary>获取外销合同客户产品列表(排除已评审,支持生产类型过滤)</summary>
    Task<IReadOnlyList<PMCUserProductInfo>> GetPMCUserProductInfoList(
        PMCRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>转换交期评审列表(分页)</summary>
    Task<PagedResult<PMCDeliveryReview>> ConvertToPMCDeliveryReviewList(
        PMCRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>获取交期评审列表(分页)</summary>
    Task<PagedResult<PMCDeliveryReview>> GetPMCDeliveryReviewList(
        PMCRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>新增或更新交期评审记录(状态为"评审驳回"时回写驳回原因)</summary>
    Task<PMCDeliveryReview> AddPMCDeliveryReview(PMCDeliveryReview review);

    /// <summary>新增或修改生产类型覆盖(按合同号+排产编号+货号匹配)</summary>
    Task<ProductionTypeOverride> SaveProductionTypeOverride(ProductionTypeOverride overrideEntity);

    /// <summary>将已通过的交期评审退回待评审,并删除本次分析关联数据</summary>
    Task<ReturnDeliveryReviewResultDto> ReturnDeliveryReview(ReturnDeliveryReviewRequestDto request);
}
