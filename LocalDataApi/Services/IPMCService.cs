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
        Task<List<PMCProductInfo>> GetPMCProductListInfo(PMCRequestDto request);
        Task<List<ProductDataAssemblyList>> GetProductDataAssemblyList(string? itemNo);
        Task<bool> SearchCoils(string? keyword);
         Task<List<PMCDeliveryReview>> GetPMCDeliveryReviewList(PMCRequestDto request);      
        Task<List<PMCDeliveryReview>> ConvertToPMCDeliveryReviewList(PMCRequestDto request);         
        Task<PMCDeliveryReview> AddPMCDeliveryReview(PMCDeliveryReview review);
        Task<List<PMCSalesControl>> AddPMCSalesControlList();
        Task<List<PMCSalesControl>> GetPMCSalesControlList(string? number);
        Task<ProductData?> GetProductData(string? itemNo);
        Task<List<SchedulingAnalysisDto>> GetSchedulingAnalysisListDto(PMCRequestDto request);
        Task<List<PMCWorkOrder>> GetPMCWorkOrderList();
        Task<PMCWorkOrder> AddPMCWorkOrder(PMCWorkOrder workOrder);
        Task<PMCWorkOrder> AddPMCWorkOrder(PMCRequestDto request);
        Task<PMCWorkOrder> UpdatePMCWorkOrder(PMCWorkOrder workOrder);
        Task<List<PMCUserProductInfo>> GetPMCUserProductInfoList(PMCRequestDto requestDto);

        Task<Warehousing> ScanWarehousingAsync(ScanWarehousingDto dto);
    }
}