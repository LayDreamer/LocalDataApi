using LocalDataApi.Dto;
using LocalDataApi.Exceptions;
using LocalDataApi.Models;
using LocalDataApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalDataApi.Controllers
{
    [ApiController]
    [Route("api/blfParameter")]
    //[Route("[controller]")]
    public class BLFParameterController : ControllerBase
    {
        private readonly IBLFParameterService _blfService;

        public BLFParameterController(IBLFParameterService blfService)
        {
            _blfService = blfService;
        }


        [HttpPost("list")]
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


        [HttpPost("detail")]
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


        [HttpPost("create")]
        public async Task<IActionResult> CreateBLFParameter(BLFParameter blfParameter)
        {
            try
            {
                await _blfService.CreateBLFParameter(blfParameter);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "创建成功！",
                    Data = new { create = blfParameter }
                });
            }
            catch (ValidationException e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"创建失败: {e.Message}",
                });
            }
            catch (ServiceException e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"创建失败: {e.Message}",
                });
            }
            catch (Exception e)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "服务器内部错误。"
                });
            }
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateBLFParameter(BLFParameter blfParameter)
        {
            try
            {
                await _blfService.UpdateBLFParameter(blfParameter);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "更新成功！",
                    Data = new { update = blfParameter }
                });
            }
            catch (ValidationException e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"更新失败: {e.Message}",
                });
            }
            catch (NotFoundException e)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"更新失败: {e.Message}",
                });
            }
            catch (ServiceException e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"更新失败: {e.Message}",
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "服务器内部错误。"
                });
            }
        }

        [HttpPost("delete")]
        public async Task<IActionResult> DeleteUser(List<string> numbers)
        {
            try
            {
                await _blfService.DeleteBLFParameter(numbers);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除成功！",
                    Data = new { deleted = numbers }
                });
            }
            catch (ValidationException e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"删除失败: {e.Message}",
                });
            }
            catch (NotFoundException e)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"删除失败: {e.Message}",
                });
            }
            catch (ServiceException e)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"删除失败: {e.Message}",
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "服务器内部错误。"
                });
            }
        }
    }
}