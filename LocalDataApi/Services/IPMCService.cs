using LocalDataApi.Dto;
using LocalDataApi.Models;

namespace LocalDataApi.Services
{
    public interface IPMCService
    {
        Task<List<PMCProductInfo>> GetPMCProductListInfo(PMCRequestDto request);
        Task<List<ProductDataAssemblyList>> GetProductDataAssemblyList(string? itemNo);
        Task<bool> SearchCoils(string? keyword);
        Task<List<PMCDeliveryReview>> GetPMCDeliveryReviewList();
        Task<PMCDeliveryReview> AddPMCDeliveryReview(PMCDeliveryReview review);
        Task<List<PMCSalesControl>> AddPMCSalesControlList();
        Task<List<PMCSalesControl>> GetPMCSalesControlList(string? number);
        Task<ProductData?> GetProductData(string? itemNo);
        Task<List<SchedulingAnalysisDto>> GetSchedulingAnalysisListDto(PMCRequestDto request);
    }
}
