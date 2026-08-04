using LocalDataApi.Dto;
using LocalDataApi.Models;

namespace LocalDataApi.Services
{
    public interface IPMCService
    {
        /// <summary>
        /// 获取PMC产品列表信息
        /// </summary>
        /// <param name="request">PMC请求参数对象</param>
        /// <returns>返回PMC产品信息列表的异步任务</returns>
        Task<PagedResult<PMCProductInfo>> GetPMCProductListInfo(PMCRequestDto request, CancellationToken cancellationToken = default);
        Task<List<ProductDataAssemblyList>> GetProductDataAssemblyList(string? itemNo);
        Task<List<ProductDataAssemblyList>> GetProductDataAssemblyListByItemNo(string? itemNo);
        Task<bool> SearchCoils(string? keyword);
        Task<List<ProductData>> SearchCoilsByKeyword(string? keyword);
        Task<List<ProductData>> SearchProductDataByKeyword(string? keyword);
        Task<PagedResult<PMCDeliveryReview>> GetPMCDeliveryReviewList(PMCRequestDto request, CancellationToken cancellationToken = default);
        Task<PagedResult<PMCDeliveryReview>> ConvertToPMCDeliveryReviewList(PMCRequestDto request, CancellationToken cancellationToken = default);
        Task<PMCDeliveryReview> AddPMCDeliveryReview(PMCDeliveryReview review);
        Task<ProductionTypeOverride> SaveProductionTypeOverride(ProductionTypeOverride overrideEntity);
        Task<ReturnDeliveryReviewResultDto> ReturnDeliveryReview(ReturnDeliveryReviewRequestDto request);
        Task<ProductData?> GetProductData(string? itemNo);

        Task<List<SchedulingAnalysisDto>> GetSchedulingAnalysisList(PMCRequestDto request);
        Task<IReadOnlyList<PMCUserProductInfo>> GetPMCUserProductInfoList(
            PMCRequestDto requestDto, CancellationToken cancellationToken = default);
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

        #region 外产BOM

        // Task<List<ExternalProductionBOM>> SaveExternalProductionBOM(string? itemNo);
        Task<List<ExternalProductionBOM>> SaveExternalProductionBOM(List<ExternalProductionBOM> bomList, string username, string schedulingNo);
        Task<PagedResult<ExternalProductionBOM>> GetExternalProductionBOMList(PMCRequestDto request, CancellationToken cancellationToken = default);
        Task DeleteExternalProductionBOMList(List<string> ids);
        #endregion

        #region BOM结构工序
        Task<List<BOMStructureProcess>> GetBOMStructureProcessList();
        #endregion
    }
}
