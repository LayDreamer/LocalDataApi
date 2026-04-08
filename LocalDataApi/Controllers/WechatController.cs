using LocalDataApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalDataApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WechatController : ControllerBase
    {
        private readonly IWechatWorkUserService _userService;

        public WechatController(IWechatWorkUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// 生成授权跳转URL
        /// </summary>
        /// <param name="redirectUri">授权后跳转的回调地址</param>
        /// <param name="state">状态参数</param>
        [HttpGet("authorize-url")]
        public IActionResult GetAuthorizeUrl([FromQuery] string redirectUri, [FromQuery] string state = "STATE")
        {
            var url = _userService.GenerateAuthorizeUrl(redirectUri, state);
            return Ok(new { url });
        }

        /// <summary>
        /// 授权回调处理
        /// </summary>
        /// <param name="code">授权code</param>
        /// <param name="state">状态参数</param>
        [HttpGet("callback")]
        public async Task<IActionResult> Callback(string code, string state)
        {
            if (string.IsNullOrEmpty(code))
            {
                return BadRequest(new { error = "code不能为空" });
            }

            try
            {
                // 1. 获取用户基本信息（userid）
                var userInfo = await _userService.GetUserInfoByCodeAsync(code);

                // 2. 获取用户详细信息
                var userDetail = await _userService.GetUserDetailByUserIdAsync(userInfo.UserId);

                return Ok(new
                {
                    userInfo.UserId,
                    userDetail.Name,
                    userDetail.Position,
                    userDetail.Mobile,
                    userDetail.Email,
                    userDetail.Avatar,
                    userDetail.Department
                });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// 直接通过userid获取用户信息
        /// </summary>
        /// <param name="userId">用户ID</param>
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserByUserId(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { error = "userId不能为空" });
            }

            try
            {
                var userDetail = await _userService.GetUserDetailByUserIdAsync(userId);
                return Ok(userDetail);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
