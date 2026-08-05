using LocalDataApi.Dto;
using LocalDataApi.Domain.Pmc;

namespace LocalDataApi.Application.Pmc.Contracts;

/// <summary>
/// 工单销控用例:工单销控表与其明细的批量增改、分页查询、批量删除。
/// </summary>
public interface IPmcWorkOrderService
{
    #region 工单销控表
    Task<PagedResult<WorkOrderSalesControl>> GetWorkOrderSalesControlList(PMCRequestDto request, CancellationToken cancellationToken = default);
    Task<List<WorkOrderSalesControl>> AddOrUpdateWorkOrderSalesControlList(List<WorkOrderSalesControl> list);
    Task DeleteWorkOrderSalesControlList(List<string> ids);
    #endregion

    #region 工单销控表明细
    Task<PagedResult<WorkOrderSalesControlDetail>> GetWorkOrderSalesControlDetailList(PMCRequestDto request, CancellationToken cancellationToken = default);
    Task<List<WorkOrderSalesControlDetail>> AddOrUpdateWorkOrderSalesControlDetailList(List<WorkOrderSalesControlDetail> list);
    Task DeleteWorkOrderSalesControlDetailList(List<string> ids);
    #endregion
}
