using LocalDataApi.Application.Common;
using LocalDataApi.Application.Ppc.Contracts;
using LocalDataApi.Dto;
using LocalDataApi.Domain.Ppc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocalDataApi.Api.Controllers.Pmc;

/// <summary>
/// 工单销控接口(路由保持 api/PMC)。
/// </summary>
[ApiController]
[Route("api/PMC")]
[EnableRateLimiting("DatabaseHeavy")]
public class PMCWorkOrderController : ControllerBase
{
    private readonly IPmcWorkOrderService _workOrderService;

    public PMCWorkOrderController(IPmcWorkOrderService workOrderService)
    {
        _workOrderService = workOrderService;
    }

    /// <summary>批量添加或更新工单销控表数据(存在则覆盖,不存在则新增)</summary>
    [HttpPost("AddOrUpdateWorkOrderSalesControlList")]
    public async Task<ActionResult<ApiResponse<object>>> AddOrUpdateWorkOrderSalesControlList(List<WorkOrderSalesControl> list)
    {
        var result = await _workOrderService.AddOrUpdateWorkOrderSalesControlList(list);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "批量保存成功！",
            Data = result
        });
    }

    /// <summary>获取工单销控表列表</summary>
    [HttpPost("GetWorkOrderSalesControlList")]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderSalesControl>>>> GetWorkOrderSalesControlList(PMCRequestDto requestDto)
    {
        var result = await _workOrderService.GetWorkOrderSalesControlList(requestDto, HttpContext.RequestAborted);
        return Ok(new ApiResponse<PagedResult<WorkOrderSalesControl>>
        {
            Success = true,
            Message = "查询成功！",
            Data = result
        });
    }

    /// <summary>批量删除工单销控表数据</summary>
    [HttpPost("DeleteWorkOrderSalesControlList")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteWorkOrderSalesControlList(List<string> ids)
    {
        await _workOrderService.DeleteWorkOrderSalesControlList(ids);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "删除成功！"
        });
    }

    /// <summary>批量添加或更新工单销控表明细数据(存在则覆盖,不存在则新增)</summary>
    [HttpPost("AddOrUpdateWorkOrderSalesControlDetailList")]
    public async Task<ActionResult<ApiResponse<object>>> AddOrUpdateWorkOrderSalesControlDetailList(List<WorkOrderSalesControlDetail> list)
    {
        var result = await _workOrderService.AddOrUpdateWorkOrderSalesControlDetailList(list);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "批量保存成功！",
            Data = result
        });
    }

    /// <summary>获取工单销控表明细列表</summary>
    [HttpPost("GetWorkOrderSalesControlDetailList")]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkOrderSalesControlDetail>>>> GetWorkOrderSalesControlDetailList(PMCRequestDto requestDto)
    {
        var result = await _workOrderService.GetWorkOrderSalesControlDetailList(requestDto, HttpContext.RequestAborted);
        return Ok(new ApiResponse<PagedResult<WorkOrderSalesControlDetail>>
        {
            Success = true,
            Message = "查询成功！",
            Data = result
        });
    }

    /// <summary>批量删除工单销控表明细数据</summary>
    [HttpPost("DeleteWorkOrderSalesControlDetailList")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteWorkOrderSalesControlDetailList(List<string> ids)
    {
        await _workOrderService.DeleteWorkOrderSalesControlDetailList(ids);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "删除成功！"
        });
    }
}
