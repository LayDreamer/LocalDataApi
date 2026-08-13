using LocalDataApi.Application.Blf;
using LocalDataApi.Application.Common;
using LocalDataApi.Domain.Blf;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;
using LocalDataApi.Api.Attributes;

namespace LocalDataApi.Api.Controllers;

/// <summary>
/// 比例阀(BLF)参数接口。
/// </summary>
[ApiController]
[Route("api/blfParameter")]
public class BLFParameterController : ControllerBase
{
    private readonly IBLFParameterService _blfService;

    public BLFParameterController(IBLFParameterService blfService)
    {
        _blfService = blfService;
    }

    /// <summary>查询所有比例阀参数</summary>
    [HttpPost("list")]
    [HasPermission(PermissionCodes.BlfParameterView)]
    public async Task<ActionResult<IEnumerable<BLFParameter>>> GeBLFParameters()
    {
        var blfParameters = await _blfService.GetAllParameters();
        if (blfParameters == null || !blfParameters.Any())
        {
            return Ok(new ApiResponse<object>
            {
                Success = false,
                Message = "数据列表为空！",
            });
        }
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "查询成功！",
            Data = blfParameters
        });
    }

    /// <summary>按比例阀编号查询</summary>
    [HttpPost("detail")]
    [HasPermission(PermissionCodes.BlfParameterView)]
    public async Task<IActionResult> GetBLFParameter([FromBody] GetBLFParameterRequest getBLFParameter)
    {
        var blfParameter = await _blfService.GetBLFParameter(getBLFParameter);
        if (blfParameter == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = $"未找到比例阀编号: {getBLFParameter.Number} 相关数据！",
            });
        }
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "查询成功！",
            Data = blfParameter
        });
    }

    /// <summary>创建比例阀参数</summary>
    [HttpPost("create")]
    [HasPermission(PermissionCodes.BlfParameterCreate)]
    public async Task<IActionResult> CreateBLFParameter(BLFParameter blfParameter)
    {
        await _blfService.CreateBLFParameter(blfParameter);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "创建成功！",
            Data = new { create = blfParameter }
        });
    }

    /// <summary>更新比例阀参数(局部更新非空字段)</summary>
    [HttpPost("update")]
    [HasPermission(PermissionCodes.BlfParameterUpdate)]
    public async Task<IActionResult> UpdateBLFParameter(BLFParameter blfParameter)
    {
        await _blfService.UpdateBLFParameter(blfParameter);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "更新成功！",
            Data = new { update = blfParameter }
        });
    }

    /// <summary>批量删除比例阀参数</summary>
    [HttpPost("delete")]
    [HasPermission(PermissionCodes.BlfParameterDelete)]
    public async Task<IActionResult> DeleteUser(List<string> numbers)
    {
        await _blfService.DeleteBLFParameter(numbers);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "删除成功！",
            Data = new { deleted = numbers }
        });
    }
}
