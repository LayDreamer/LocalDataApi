using LocalDataApi.Dto;
using LocalDataApi.Models;
using LocalDataApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalDataApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PMCController : ControllerBase
    {
        private readonly PMCService _pmcService;

        public PMCController(PMCService pmcService)
        {
            _pmcService = pmcService;
        }

        /// <summary>
        /// 获取产品信息列表
        /// </summary>
        [HttpPost("ProductListInfo")]
        public async Task<IActionResult> GetPMCProductInfo(PMCRequestDto requestDto)
        {
            var basicInfo = await _pmcService.GetPMCProductListInfo(requestDto);
            // 无论是否有数据，都视为查询成功
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "查询成功！",
                Data = basicInfo
            });
        }

        /// <summary>
        /// 根据货号获取产品资料装配清单
        /// </summary>
        [HttpPost("ProductDataAssemblyList")]
        public async Task<IActionResult> GetProductDataAssemblyList(PMCRequestDto requestDto)
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
                var result = await _pmcService.GetProductDataAssemblyList(requestDto.货号);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = result
                });
            }
            catch (InvalidOperationException ex) // 当 FirstAsync 找不到数据时抛出
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"未找到货号 {requestDto.货号} 对应的产品资料装配信息"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "服务器内部错误"
                });
            }
        }

        /// <summary>
        /// 检查线圈货号是否存在于装配清单中
        /// </summary>
        [HttpPost("CheckAssemblyList")]
        public async Task<IActionResult> CheckIsExistInAssemblyList(PMCRequestDto requestDto)
        {
            if (string.IsNullOrWhiteSpace(requestDto.线圈货号))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "线圈货号不能为空！"
                });
            }

            var result = await _pmcService.SearchCoils(requestDto.线圈货号);
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "查询成功！",
                Data = result
            });
        }

        /// <summary>
        /// 获取交期评审列表
        /// </summary>
        [HttpPost("PMCDeliveryReviewList")]
        public async Task<IActionResult> GetPMCDeliveryReviewList()
        {
            try
            {
                var reviewList = await _pmcService.GetPMCDeliveryReviewList();
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = reviewList // 空列表也正常返回
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }



        /// <summary>
        /// 新增交期评审记录
        /// </summary>
        [HttpPost("AddPMCDeliveryReview")]
        public async Task<IActionResult> AddPMCDeliveryReview(PMCDeliveryReview review)
        {
            try
            {
                var result = await _pmcService.AddPMCDeliveryReview(review);
                if (result == null)
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "新增失败，未返回有效数据！"
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "新增成功！",
                    Data = result
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });


            }
        }


        /// <summary>
        /// 
        /// </summary>
        [HttpPost("AddPMCSalesControlList")]
        public async Task<IActionResult> AddPMCSalesControlList()
        {
            try
            {
                var reviewList = await _pmcService.AddPMCSalesControlList();
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = reviewList // 空列表也正常返回
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        [HttpPost("GetPMCSalesControlList")]
        public async Task<IActionResult> GetPMCSalesControlList(PMCRequestDto requestDto)
        {
            try
            {
                var reviewList = await _pmcService.GetPMCSalesControlList(requestDto.货号);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = reviewList // 空列表也正常返回
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }
        [HttpPost("GetPMCProductData")]
        public async Task<IActionResult> GetPMCProductData(PMCRequestDto requestDto)
        {
            try
            {
                var productData = await _pmcService.GetProductData(requestDto.货号);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = productData // 空列表也正常返回
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

        [HttpPost("SchedulingAnalysisList")]
        public async Task<IActionResult> GetSchedulingAnalysisList(PMCRequestDto requestDto)
        {
            try
            {
                var productData = await _pmcService.GetSchedulingAnalysisListDto(requestDto);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "获取成功！",
                    Data = productData // 空列表也正常返回
                });
            }
            catch (Exception e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"错误提示：{e.Message}"
                });
            }
        }

    }
}