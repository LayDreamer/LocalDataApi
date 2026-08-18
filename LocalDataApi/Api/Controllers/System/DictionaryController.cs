using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Dictionary;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalDataApi.Api.Controllers.System;

[ApiController]
[Route("api/system/dictionary")]
public sealed class DictionaryController(IDictionaryService dictionaryService) : ControllerBase
{
    // ========== 查询(需登录,供业务模块动态加载) ==========

    /// <summary>获取全部字典类型列表(管理用)。</summary>
    [HttpGet("list")]
    [Authorize]
    [AuthenticatedOnly]
    public async Task<ActionResult<ApiResponse<List<DictionaryTypeDto>>>> GetList()
        => Ok(new ApiResponse<List<DictionaryTypeDto>> { Success = true, Data = await dictionaryService.GetTypesAsync(HttpContext.RequestAborted) });

    /// <summary>按字典编码获取字典(含字典项,业务下拉动态加载)。</summary>
    [HttpGet("{code}")]
    [Authorize]
    [AuthenticatedOnly]
    public async Task<ActionResult<ApiResponse<DictionaryDataDto?>>> GetByCode(string code)
        => Ok(new ApiResponse<DictionaryDataDto?> { Success = true, Data = await dictionaryService.GetByCodeAsync(code, HttpContext.RequestAborted) });

    /// <summary>批量获取字典(code1,code2 或 ?codes=a&codes=b)。</summary>
    [HttpGet("batch")]
    [Authorize]
    [AuthenticatedOnly]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<DictionaryItemDto>>>>> GetBatch([FromQuery] string? codes)
    {
        var list = (codes ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var data = await dictionaryService.GetBatchAsync(list, HttpContext.RequestAborted);
        return Ok(new ApiResponse<Dictionary<string, List<DictionaryItemDto>>> { Success = true, Data = data });
    }

    // ========== 字典类型管理 ==========

    [HttpPost("type")]
    [HasPermission(PermissionCodes.SystemDictionaryCreate)]
    public async Task<ActionResult<ApiResponse<DictionaryTypeDto>>> CreateType(DictionaryTypeCreateDto dto)
        => Ok(new ApiResponse<DictionaryTypeDto> { Success = true, Message = "字典类型创建成功", Data = await dictionaryService.CreateTypeAsync(dto, HttpContext.RequestAborted) });

    [HttpPut("type/{id:long}")]
    [HasPermission(PermissionCodes.SystemDictionaryUpdate)]
    public async Task<ActionResult<ApiResponse<DictionaryTypeDto>>> UpdateType(long id, DictionaryTypeUpdateDto dto)
        => Ok(new ApiResponse<DictionaryTypeDto> { Success = true, Message = "字典类型修改成功", Data = await dictionaryService.UpdateTypeAsync(id, dto, HttpContext.RequestAborted) });

    [HttpDelete("type/{id:long}")]
    [HasPermission(PermissionCodes.SystemDictionaryDelete)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteType(long id)
    {
        await dictionaryService.DeleteTypeAsync(id, HttpContext.RequestAborted);
        return Ok(new ApiResponse<object> { Success = true, Message = "字典类型删除成功" });
    }

    // ========== 字典项管理 ==========

    [HttpPost("item")]
    [HasPermission(PermissionCodes.SystemDictionaryCreate)]
    public async Task<ActionResult<ApiResponse<DictionaryItemDto>>> CreateItem(DictionaryItemCreateDto dto)
        => Ok(new ApiResponse<DictionaryItemDto> { Success = true, Message = "字典项创建成功", Data = await dictionaryService.CreateItemAsync(dto, HttpContext.RequestAborted) });

    [HttpPut("item/{id:long}")]
    [HasPermission(PermissionCodes.SystemDictionaryUpdate)]
    public async Task<ActionResult<ApiResponse<DictionaryItemDto>>> UpdateItem(long id, DictionaryItemUpdateDto dto)
        => Ok(new ApiResponse<DictionaryItemDto> { Success = true, Message = "字典项修改成功", Data = await dictionaryService.UpdateItemAsync(id, dto, HttpContext.RequestAborted) });

    [HttpDelete("item/{id:long}")]
    [HasPermission(PermissionCodes.SystemDictionaryDelete)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteItem(long id)
    {
        await dictionaryService.DeleteItemAsync(id, HttpContext.RequestAborted);
        return Ok(new ApiResponse<object> { Success = true, Message = "字典项删除成功" });
    }
}
