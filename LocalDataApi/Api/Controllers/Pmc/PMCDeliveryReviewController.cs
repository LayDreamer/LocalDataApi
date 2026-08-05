using LocalDataApi.Application.Common;
using LocalDataApi.Application.Pmc.Contracts;
using LocalDataApi.Dto;
using LocalDataApi.Domain.Pmc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocalDataApi.Api.Controllers.Pmc;

/// <summary>
/// 交期评审接口(路由保持 api/PMC)。
/// </summary>
[ApiController]
[Route("api/PMC")]
[EnableRateLimiting("DatabaseHeavy")]
public class PMCDeliveryReviewController : ControllerBase
{
    private readonly IPmcDeliveryReviewService _deliveryReviewService;

    public PMCDeliveryReviewController(IPmcDeliveryReviewService deliveryReviewService)
    {
        _deliveryReviewService = deliveryReviewService;
    }

    /// <summary>转换交期评审列表(根据外销合同客户产品表)</summary>
    [HttpPost("ConvertToPMCDeliveryReviewList")]
    public async Task<ActionResult<ApiResponse<PagedResult<PMCDeliveryReview>>>>
        ConvertToPMCDeliveryReviewList(PMCRequestDto requestDto)
    {
        var userProductList = await _deliveryReviewService.ConvertToPMCDeliveryReviewList(requestDto, HttpContext.RequestAborted);
        return Ok(new ApiResponse<PagedResult<PMCDeliveryReview>>()
        {
            Success = true,
            Message = "获取成功！",
            Data = userProductList // 空列表也正常返回
        });
    }

    /// <summary>获取交期评审列表</summary>
    [HttpPost("PMCDeliveryReviewList")]
    public async Task<ActionResult<ApiResponse<PagedResult<PMCDeliveryReview>>>> GetPMCDeliveryReviewList(PMCRequestDto requestDto)
    {
        var reviewList = await _deliveryReviewService.GetPMCDeliveryReviewList(requestDto, HttpContext.RequestAborted);
        return Ok(new ApiResponse<PagedResult<PMCDeliveryReview>>
        {
            Success = true,
            Message = "获取成功！",
            Data = reviewList // 空列表也正常返回
        });
    }

    /// <summary>新增交期评审记录</summary>
    [HttpPost("AddPMCDeliveryReview")]
    public async Task<ActionResult<ApiResponse<object>>> AddPMCDeliveryReview(PMCDeliveryReview review)
    {
        var result = await _deliveryReviewService.AddPMCDeliveryReview(review);
        if (result == null)
        {
            return Ok(new ApiResponse<object>
            {
                Success = false,
                Message = "新增失败,未返回有效数据！"
            });
        }

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "新增成功！",
            Data = result
        });
    }

    /// <summary>新增或修改生产类型覆盖(按合同号+排产编号+货号匹配)</summary>
    [HttpPost("SaveProductionTypeOverride")]
    public async Task<ActionResult<ApiResponse<object>>> SaveProductionTypeOverride(ProductionTypeOverride overrideEntity)
    {
        var result = await _deliveryReviewService.SaveProductionTypeOverride(overrideEntity);
        if (result == null)
        {
            return Ok(new ApiResponse<object>
            {
                Success = false,
                Message = "保存失败,未返回有效数据！"
            });
        }

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "保存成功！",
            Data = result
        });
    }

    /// <summary>将已通过的交期评审退回待评审,并删除本次分析关联数据</summary>
    [HttpPost("ReturnDeliveryReview")]
    public async Task<ActionResult<ApiResponse<ReturnDeliveryReviewResultDto>>> ReturnDeliveryReview(
        ReturnDeliveryReviewRequestDto request)
    {
        var result = await _deliveryReviewService.ReturnDeliveryReview(request);
        return Ok(new ApiResponse<ReturnDeliveryReviewResultDto>
        {
            Success = true,
            Message = "已退回待评审",
            Data = result
        });
    }
}
