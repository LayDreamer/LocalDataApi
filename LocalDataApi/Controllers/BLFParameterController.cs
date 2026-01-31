using LocalDataApi.Dto;
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
        private readonly BLFParameterService _blfService;

        public BLFParameterController(BLFParameterService blfService)
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
        public async Task<ActionResult<BLFParameter>> GetBLFParameter([FromBody] GetBLFParameterRequest getBLFParameter)
        {
            var blfParameter = await _blfService.GetBLFParameter(getBLFParameter);
            if (blfParameter == null)
            {
                return Ok(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"未找到比例阀编号:{getBLFParameter}相关数据！",
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
            catch (Exception e)
            {
                return Ok(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"创建失败:{e.Message}",
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
            catch (Exception e)
            {
                return Ok(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"更新失败:{e.Message}",
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
            catch (Exception e)
            {
                return Ok(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"删除失败:{e.Message}",
                });
               // return BadRequest(new { error = $"删除失败:{e.Message}" });
            }
        }
    }
}