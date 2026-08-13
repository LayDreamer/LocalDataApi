using LocalDataApi.Application.Common;
using LocalDataApi.Application.Pmc.Contracts;
using LocalDataApi.Dto;
using LocalDataApi.Domain.Pmc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using LocalDataApi.Api.Attributes;

namespace LocalDataApi.Api.Controllers.Pmc;

/// <summary>
/// 外产 BOM 接口(路由保持 api/PMC)。
/// </summary>
[ApiController]
[Route("api/PMC")]
[EnableRateLimiting("DatabaseHeavy")]
public class PMCBomController : ControllerBase
{
    private readonly IPmcBomService _bomService;

    public PMCBomController(IPmcBomService bomService)
    {
        _bomService = bomService;
    }

    /// <summary>根据成品货号生成并保存外产BOM结构</summary>
    [HttpPost("SaveExternalProductionBOM")]
    [HasPermission(PermissionCodes.ExternalProductionCreate, PermissionCodes.ExternalProductionUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> SaveExternalProductionBOM(List<ExternalProductionBOM> bomList, string username, string schedulingNo)
    {
        var savedList = await _bomService.SaveExternalProductionBOM(bomList, username, schedulingNo);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "保存成功！",
            Data = savedList
        });
    }

    /// <summary>获取外产BOM列表</summary>
    [HttpPost("GetExternalProductionBOMList")]
    [HasPermission(PermissionCodes.ExternalProductionView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ExternalProductionBOM>>>> GetExternalProductionBOMList(PMCRequestDto requestDto)
    {
        var result = await _bomService.GetExternalProductionBOMList(requestDto, HttpContext.RequestAborted);
        return Ok(new ApiResponse<PagedResult<ExternalProductionBOM>>
        {
            Success = true,
            Message = "查询成功！",
            Data = result
        });
    }

    /// <summary>批量删除外产BOM数据</summary>
    [HttpPost("DeleteExternalProductionBOMList")]
    [HasPermission(PermissionCodes.ExternalProductionDelete)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteExternalProductionBOMList(List<string> ids)
    {
        await _bomService.DeleteExternalProductionBOMList(ids);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "删除成功！"
        });
    }

    /// <summary>获取所有BOM结构工序数据</summary>
    [HttpPost("GetBOMStructureProcessList")]
    [HasPermission(PermissionCodes.ExternalProductionView)]
    public async Task<ActionResult<ApiResponse<object>>> GetBOMStructureProcessList()
    {
        var result = await _bomService.GetBOMStructureProcessList();
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "查询成功！",
            Data = result
        });
    }
}
