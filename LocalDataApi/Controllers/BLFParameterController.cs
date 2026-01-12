using LocalDataApi.Models;
using LocalDataApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalDataApi.Controllers
{
    [ApiController]
    [Route("api/blfParameter")]
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
            var bLFParameters = await _blfService.GetAllParameters();
            if (bLFParameters == null || !bLFParameters.Any())
            {
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "数据列表为空！",
                });
            }
            return Ok(bLFParameters);
        }


        [HttpPost("detail")]
        public async Task<ActionResult<BLFParameter>> GetBLFParameter(string number)
        {
            var blfParameter = await _blfService.GetBLFParameter(number);

            if (blfParameter == null)
            {
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"未找到比例阀编号:{number}相关数据！",
                });
            }

            return blfParameter;
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
                return BadRequest(new { error = $"创建失败:{e.Message}" });
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
                return BadRequest(new { error = $"更新失败:{e.Message}" });
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
                return BadRequest(new { error = $"删除失败:{e.Message}" });
            }
        }
    }
}