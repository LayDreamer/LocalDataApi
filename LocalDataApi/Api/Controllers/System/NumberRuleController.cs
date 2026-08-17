using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Platform;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;

namespace LocalDataApi.Api.Controllers.System;

[ApiController]
[Route("api/system/number-rule")]
public sealed class NumberRuleController(INumberRuleService numberRuleService) : ControllerBase
{
    // ========== 查询(需登录且有 View 权限;业务模块取号走服务,不暴露此管理接口) ==========

    /// <summary>获取全部编码规则(管理列表)。</summary>
    [HttpGet("list")]
    [HasPermission(PermissionCodes.PlatformNumberRuleView)]
    public async Task<ActionResult<ApiResponse<List<NumberRuleDto>>>> GetList()
        => Ok(new ApiResponse<List<NumberRuleDto>>
        {
            Success = true,
            Data = await numberRuleService.GetRulesAsync(HttpContext.RequestAborted)
        });

    /// <summary>按 Id 获取编码规则。</summary>
    [HttpGet("{id:long}")]
    [HasPermission(PermissionCodes.PlatformNumberRuleView)]
    public async Task<ActionResult<ApiResponse<NumberRuleDto>>> GetById(long id)
        => Ok(new ApiResponse<NumberRuleDto>
        {
            Success = true,
            Data = await numberRuleService.GetRuleAsync(id, HttpContext.RequestAborted)
        });

    // ========== 规则管理(写操作挂权限) ==========

    [HttpPost]
    [HasPermission(PermissionCodes.PlatformNumberRuleCreate)]
    public async Task<ActionResult<ApiResponse<NumberRuleDto>>> Create(NumberRuleCreateDto dto)
        => Ok(new ApiResponse<NumberRuleDto>
        {
            Success = true,
            Message = "编码规则创建成功",
            Data = await numberRuleService.CreateRuleAsync(dto, HttpContext.RequestAborted)
        });

    [HttpPut("{id:long}")]
    [HasPermission(PermissionCodes.PlatformNumberRuleUpdate)]
    public async Task<ActionResult<ApiResponse<NumberRuleDto>>> Update(long id, NumberRuleUpdateDto dto)
        => Ok(new ApiResponse<NumberRuleDto>
        {
            Success = true,
            Message = "编码规则修改成功",
            Data = await numberRuleService.UpdateRuleAsync(id, dto, HttpContext.RequestAborted)
        });

    [HttpPost("{id:long}/reset")]
    [HasPermission(PermissionCodes.PlatformNumberRuleUpdate)]
    public async Task<ActionResult<ApiResponse<NumberRuleDto>>> Reset(long id, NumberRuleResetDto dto)
        => Ok(new ApiResponse<NumberRuleDto>
        {
            Success = true,
            Message = "流水号重置成功",
            Data = await numberRuleService.ResetSequenceAsync(id, dto.StartFrom, HttpContext.RequestAborted)
        });
}
