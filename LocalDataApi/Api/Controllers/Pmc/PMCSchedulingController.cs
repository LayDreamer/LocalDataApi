using LocalDataApi.Application.Common;
using LocalDataApi.Application.Pmc.Contracts;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using LocalDataApi.Api.Attributes;

namespace LocalDataApi.Api.Controllers.Pmc;

/// <summary>
/// 排产分析接口(路由保持 api/PMC)。
/// </summary>
[ApiController]
[Route("api/PMC")]
[EnableRateLimiting("DatabaseHeavy")]
public class PMCSchedulingController : ControllerBase
{
    private readonly IPmcSchedulingService _schedulingService;

    public PMCSchedulingController(IPmcSchedulingService schedulingService)
    {
        _schedulingService = schedulingService;
    }

    /// <summary>获取排产分析列表</summary>
    [HttpPost("SchedulingAnalysisList")]
    [HasPermission(PermissionCodes.ScheduleView)]
    public async Task<ActionResult<ApiResponse<object>>> GetSchedulingAnalysisList(PMCRequestDto requestDto)
    {
        var productData = await _schedulingService.GetSchedulingAnalysisList(requestDto);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "获取成功！",
            Data = productData // 空列表也正常返回
        });
    }
}
