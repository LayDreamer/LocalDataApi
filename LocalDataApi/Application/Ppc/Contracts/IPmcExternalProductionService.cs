using LocalDataApi.Dto;
using LocalDataApi.Domain.Ppc;

namespace LocalDataApi.Application.Ppc.Contracts;

/// <summary>
/// 外产管理用例:发运 / 领料 / 生产 / 入库 的批量增改、分页查询、批量删除。
/// </summary>
public interface IPmcExternalProductionService
{
    #region 外产发运
    Task<PagedResult<ExternalProductionShipment>> GetExternalProductionShipmentList(PMCRequestDto request, CancellationToken cancellationToken = default);
    Task<List<ExternalProductionShipment>> AddOrUpdateExternalProductionShipmentList(List<ExternalProductionShipment> list);
    Task DeleteExternalProductionShipmentList(List<string> ids);
    #endregion

    #region 外产领料
    Task<PagedResult<ExternalProductionPickMaterial>> GetExternalProductionPickMaterialList(PMCRequestDto request, CancellationToken cancellationToken = default);
    Task<List<ExternalProductionPickMaterial>> AddOrUpdateExternalProductionPickMaterialList(List<ExternalProductionPickMaterial> list);
    Task DeleteExternalProductionPickMaterialList(List<string> ids);
    #endregion

    #region 外产生产
    Task<PagedResult<ExternalProduction>> GetExternalProductionList(PMCRequestDto request, CancellationToken cancellationToken = default);
    Task<ExternalProduction?> GetExternalProductionByNo(string 编号);
    Task<List<ExternalProduction>> AddOrUpdateExternalProductionList(List<ExternalProduction> list);
    Task DeleteExternalProductionList(List<string> ids);
    #endregion

    #region 外产入库
    Task<PagedResult<ExternalProductionWarehousing>> GetExternalProductionWarehousingList(PMCRequestDto request, CancellationToken cancellationToken = default);
    Task<List<ExternalProductionWarehousing>> AddOrUpdateExternalProductionWarehousingList(List<ExternalProductionWarehousing> list);
    Task DeleteExternalProductionWarehousingList(List<string> ids);
    #endregion
}
