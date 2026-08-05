using LocalDataApi.Application.Common;
using LocalDataApi.Application.Ppc.Contracts;
using LocalDataApi.Dto;
using LocalDataApi.Domain.Ppc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocalDataApi.Api.Controllers.Pmc;

/// <summary>
/// PMC 产品资料与装配查询接口(路由保持 api/PMC)。
/// </summary>
[ApiController]
[Route("api/PMC")]
[EnableRateLimiting("DatabaseHeavy")]
public class PMCProductController : ControllerBase
{
    private readonly IPmcProductService _productService;

    public PMCProductController(IPmcProductService productService)
    {
        _productService = productService;
    }

    /// <summary>获取产品信息列表</summary>
    [HttpPost("ProductListInfo")]
    public async Task<ActionResult<ApiResponse<PagedResult<PMCProductInfo>>>> GetPMCProductInfo(PMCRequestDto requestDto)
    {
        var basicInfo = await _productService.GetPMCProductListInfo(requestDto, HttpContext.RequestAborted);
        // 无论是否有数据,都视为查询成功
        return Ok(new ApiResponse<PagedResult<PMCProductInfo>>
        {
            Success = true,
            Message = "查询成功！",
            Data = basicInfo
        });
    }

    /// <summary>根据货号获取产品资料</summary>
    [HttpPost("GetPMCProductData")]
    public async Task<ActionResult<ApiResponse<object>>> GetPMCProductData(PMCRequestDto requestDto)
    {
        if (string.IsNullOrWhiteSpace(requestDto.货号))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "货号不能为空！"
            });
        }

        var productData = await _productService.GetProductData(requestDto.货号);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "获取成功！",
            Data = productData // 空列表也正常返回
        });
    }

    /// <summary>根据货号获取产品资料装配清单</summary>
    [HttpPost("ProductDataAssemblyList")]
    public async Task<ActionResult<ApiResponse<object>>> GetProductDataAssemblyList(PMCRequestDto requestDto)
    {
        if (string.IsNullOrWhiteSpace(requestDto.货号))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "货号不能为空！"
            });
        }

        try
        {
            var result = await _productService.GetProductDataAssemblyList(requestDto.货号);
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "查询成功！",
                Data = result
            });
        }
        catch (InvalidOperationException)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = $"未找到货号 {requestDto.货号} 对应的产品资料装配信息"
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "服务器内部错误"
            });
        }
    }

    /// <summary>根据货号获取产品资料装配清单中中间件等于 0 的记录</summary>
    [HttpPost("ProductDataAssemblyListByItemNo")]
    public async Task<ActionResult<ApiResponse<object>>> GetProductDataAssemblyListByItemNo(PMCRequestDto requestDto)
    {
        if (string.IsNullOrWhiteSpace(requestDto.货号))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "货号不能为空！"
            });
        }

        var result = await _productService.GetProductDataAssemblyListByItemNo(requestDto.货号);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "查询成功！",
            Data = result
        });
    }

    /// <summary>检查线圈货号是否存在于装配清单中</summary>
    [HttpPost("CheckAssemblyList")]
    public async Task<ActionResult<ApiResponse<object>>> CheckIsExistInAssemblyList(PMCRequestDto requestDto)
    {
        if (string.IsNullOrWhiteSpace(requestDto.线圈货号))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "线圈货号不能为空！"
            });
        }

        var result = await _productService.SearchCoils(requestDto.线圈货号);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "查询成功！",
            Data = result
        });
    }

    /// <summary>按关键字模糊查询产品资料中的线圈(货号包含关键字即可),最多返回 50 条</summary>
    [HttpPost("SearchCoilsByKeyword")]
    public async Task<ActionResult<ApiResponse<object>>> SearchCoilsByKeyword(PMCRequestDto requestDto)
    {
        if (string.IsNullOrWhiteSpace(requestDto.线圈货号))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "线圈货号(关键字)不能为空！"
            });
        }

        var result = await _productService.SearchCoilsByKeyword(requestDto.线圈货号);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "查询成功！",
            Data = result
        });
    }

    /// <summary>按货号模糊查询产品资料(不区分线圈),最多返回 50 条</summary>
    [HttpPost("SearchProductDataByKeyword")]
    public async Task<ActionResult<ApiResponse<object>>> SearchProductDataByKeyword(PMCRequestDto requestDto)
    {
        if (string.IsNullOrWhiteSpace(requestDto.货号))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "货号不能为空！"
            });
        }

        var result = await _productService.SearchProductDataByKeyword(requestDto.货号);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "查询成功！",
            Data = result
        });
    }
}
