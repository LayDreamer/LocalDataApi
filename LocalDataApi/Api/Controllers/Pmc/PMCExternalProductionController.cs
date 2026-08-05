using LocalDataApi.Application.Common;
using LocalDataApi.Application.Ppc.Contracts;
using LocalDataApi.Dto;
using LocalDataApi.Domain.Ppc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocalDataApi.Api.Controllers.Pmc;

/// <summary>
/// 外产管理接口(发运 / 领料 / 生产 / 入库,路由保持 api/PMC)。
/// </summary>
[ApiController]
[Route("api/PMC")]
[EnableRateLimiting("DatabaseHeavy")]
public class PMCExternalProductionController : ControllerBase
{
    private readonly IPmcExternalProductionService _externalProductionService;

    public PMCExternalProductionController(IPmcExternalProductionService externalProductionService)
    {
        _externalProductionService = externalProductionService;
    }

    #region 外产发运

    /// <summary>批量添加或更新外产发运数据(存在则覆盖,不存在则新增)</summary>
    [HttpPost("AddOrUpdateExternalProductionShipmentList")]
    public async Task<ActionResult<ApiResponse<object>>> AddOrUpdateExternalProductionShipmentList(List<ExternalProductionShipment> list)
    {
        var result = await _externalProductionService.AddOrUpdateExternalProductionShipmentList(list);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "批量保存成功！",
            Data = result
        });
    }

    /// <summary>获取外产发运列表</summary>
    [HttpPost("GetExternalProductionShipmentList")]
    public async Task<ActionResult<ApiResponse<PagedResult<ExternalProductionShipment>>>> GetExternalProductionShipmentList(PMCRequestDto requestDto)
    {
        var result = await _externalProductionService.GetExternalProductionShipmentList(requestDto, HttpContext.RequestAborted);
        return Ok(new ApiResponse<PagedResult<ExternalProductionShipment>>
        {
            Success = true,
            Message = "查询成功！",
            Data = result
        });
    }

    /// <summary>批量删除外产发运数据</summary>
    [HttpPost("DeleteExternalProductionShipmentList")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteExternalProductionShipmentList(List<string> ids)
    {
        await _externalProductionService.DeleteExternalProductionShipmentList(ids);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "删除成功！"
        });
    }

    #endregion

    #region 外产领料

    /// <summary>批量添加或更新外产领料数据(存在则覆盖,不存在则新增)</summary>
    [HttpPost("AddOrUpdateExternalProductionPickMaterialList")]
    public async Task<ActionResult<ApiResponse<object>>> AddOrUpdateExternalProductionPickMaterialList(List<ExternalProductionPickMaterial> list)
    {
        var result = await _externalProductionService.AddOrUpdateExternalProductionPickMaterialList(list);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "批量保存成功！",
            Data = result
        });
    }

    /// <summary>获取外产领料列表</summary>
    [HttpPost("GetExternalProductionPickMaterialList")]
    public async Task<ActionResult<ApiResponse<PagedResult<ExternalProductionPickMaterial>>>> GetExternalProductionPickMaterialList(PMCRequestDto requestDto)
    {
        var result = await _externalProductionService.GetExternalProductionPickMaterialList(requestDto, HttpContext.RequestAborted);
        return Ok(new ApiResponse<PagedResult<ExternalProductionPickMaterial>>
        {
            Success = true,
            Message = "查询成功！",
            Data = result
        });
    }

    /// <summary>批量删除外产领料数据</summary>
    [HttpPost("DeleteExternalProductionPickMaterialList")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteExternalProductionPickMaterialList(List<string> ids)
    {
        await _externalProductionService.DeleteExternalProductionPickMaterialList(ids);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "删除成功！"
        });
    }

    #endregion

    #region 外产生产

    /// <summary>批量添加或更新外产生产数据(存在则覆盖,不存在则新增)</summary>
    [HttpPost("AddOrUpdateExternalProductionList")]
    public async Task<ActionResult<ApiResponse<object>>> AddOrUpdateExternalProductionList(List<ExternalProduction> list)
    {
        var result = await _externalProductionService.AddOrUpdateExternalProductionList(list);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "批量保存成功！",
            Data = result
        });
    }

    /// <summary>获取外产生产列表</summary>
    [HttpPost("GetExternalProductionList")]
    public async Task<ActionResult<ApiResponse<PagedResult<ExternalProduction>>>> GetExternalProductionList(PMCRequestDto requestDto)
    {
        var result = await _externalProductionService.GetExternalProductionList(requestDto, HttpContext.RequestAborted);
        return Ok(new ApiResponse<PagedResult<ExternalProduction>>
        {
            Success = true,
            Message = "查询成功！",
            Data = result
        });
    }

    /// <summary>根据编号查询单条外产生产数据</summary>
    [HttpPost("GetExternalProductionByNo")]
    public async Task<ActionResult<ApiResponse<ExternalProduction>>> GetExternalProductionByNo([FromBody] string 编号)
    {
        var result = await _externalProductionService.GetExternalProductionByNo(编号);
        if (result == null)
        {
            return Ok(new ApiResponse<ExternalProduction>
            {
                Success = false,
                Message = "未找到指定编号的数据",
                Data = null
            });
        }
        return Ok(new ApiResponse<ExternalProduction>
        {
            Success = true,
            Message = "查询成功！",
            Data = result
        });
    }

    /// <summary>批量删除外产生产数据</summary>
    [HttpPost("DeleteExternalProductionList")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteExternalProductionList(List<string> ids)
    {
        await _externalProductionService.DeleteExternalProductionList(ids);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "删除成功！"
        });
    }

    #endregion

    #region 外产入库

    /// <summary>批量添加或更新外产入库数据(存在则覆盖,不存在则新增)</summary>
    [HttpPost("AddOrUpdateExternalProductionWarehousingList")]
    public async Task<ActionResult<ApiResponse<object>>> AddOrUpdateExternalProductionWarehousingList(List<ExternalProductionWarehousing> list)
    {
        var result = await _externalProductionService.AddOrUpdateExternalProductionWarehousingList(list);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "批量保存成功！",
            Data = result
        });
    }

    /// <summary>获取外产入库列表</summary>
    [HttpPost("GetExternalProductionWarehousingList")]
    public async Task<ActionResult<ApiResponse<PagedResult<ExternalProductionWarehousing>>>> GetExternalProductionWarehousingList(PMCRequestDto requestDto)
    {
        var result = await _externalProductionService.GetExternalProductionWarehousingList(requestDto, HttpContext.RequestAborted);
        return Ok(new ApiResponse<PagedResult<ExternalProductionWarehousing>>
        {
            Success = true,
            Message = "查询成功！",
            Data = result
        });
    }

    /// <summary>批量删除外产入库数据</summary>
    [HttpPost("DeleteExternalProductionWarehousingList")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteExternalProductionWarehousingList(List<string> ids)
    {
        await _externalProductionService.DeleteExternalProductionWarehousingList(ids);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "删除成功！"
        });
    }

    #endregion
}
