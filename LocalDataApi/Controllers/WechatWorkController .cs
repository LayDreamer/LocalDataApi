using Azure;
using LocalDataApi.Dto;
using LocalDataApi.Services;
using Microsoft.AspNetCore.Mvc;
using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using static SKIT.FlurlHttpClient.Wechat.Work.Models.CgibinServiceGetLoginInfoResponse.Types.Authorization.Types;

namespace LocalDataApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WechatWorkController : ControllerBase
    {
        private readonly WeChatWorkService _wechatWorkService;

        public WechatWorkController(WeChatWorkService wechatWorkService)
        {
            _wechatWorkService = wechatWorkService;
        }

        /// <summary>
        /// 获取指定部门下的成员列表（包含详情）
        /// </summary>
        /// <param name="departmentId">部门ID，默认根部门(1)</param>
        /// <returns></returns>
        [HttpPost("users")]
        public async Task<IActionResult> GetUsers(DepartmentRequestDto departmentRequest)
        {
            var response = await _wechatWorkService.GetDepartmentUsersAsync(departmentRequest.DepartmentId);
            if (response.IsSuccessful())
            {
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "用户列表查询成功！",
                    Data = response.UserList,
                });
            }
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = response.ErrorMessage
            });
        }

        [HttpGet("departments")]
        public async Task<IActionResult> GetDepartments()
        {
            var response = await _wechatWorkService.GetDepartmentsAsync();
            if (response.IsSuccessful())
            {
                // 2. 转换为树形结构
                var tree = _wechatWorkService.BuildDepartmentTree(response.DepartmentList.ToList());
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "部门列表查询成功！",
                    Data = tree,
                });
            }
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = response.ErrorMessage
            });
        }

        [HttpPost("chains")]
        public async Task<IActionResult> GetChains()
        {
            var response = await _wechatWorkService.GetChainsAsync();
            if (response.IsSuccessful())
            {
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "关联企业列表查询成功！",
                    Data = response.ChainList,
                });
            }
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = response.ErrorMessage
            });
            
        }
        [HttpPost("chainGroup")]
        public async Task<IActionResult> GetChainGroup(string chainId)
        {
            var response = await _wechatWorkService.GetChainGroupAsync(chainId);
            if (response.IsSuccessful())
            {
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "关联企业根目录查询成功！",
                    Data = response.GroupList,
                });
            }
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = response.ErrorMessage
            });

        }
        [HttpPost("chainGroupInfo")]
        public async Task<IActionResult> GetChainGroup(string chainId,int?groupId)
        {
            var response = await _wechatWorkService.GetChainGroupInfoListAsync(chainId,groupId);
            if (response.IsSuccessful())
            {
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "关联企业分组查询成功！",
                    Data = response.GroupCorpList,
                });
            }
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = response.ErrorMessage
            });

        }

        [HttpPost("linkedUserInfo")]
        public async Task<IActionResult> GetLinkedUserInfo(string chainId, int? groupId)
        {
            var response = await _wechatWorkService.GetLinkedCorpUserListAsync(chainId, groupId);
            if (response.IsSuccessful())
            {
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "关联企业员工列表查询成功！",
                    Data = response.CorpUserList,
                });
            }
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = response.ErrorMessage
            });

        }



        [HttpPost("sendSupplier")]
        public async Task<IActionResult> SendMessageToSupplier(string corpId, string userId)
        {
            
            var response = await _wechatWorkService.SendTextMessageToLinkedCorpUserAsync(corpId,userId,"您好，这是来自上游企业的一条测试消息。");
            if (response.IsSuccessful())
            {
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "发送成功！",
                    Data = response.InvalidCorpUserIdList,
                });
            }
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = response.ErrorMessage
            });

        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage(SendMessageDto dto)
        {
            try
            {
                // 可以在这里添加业务逻辑，比如验证、日志等
                var response = await _wechatWorkService.SendMessageAsync(
                    users: dto.Users,
                    content: dto.Content,
                    msgType:dto.MsgType
                );

                if (response.IsSuccessful())
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "发送文本成功！"
                    });
                }
                else
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "发送失败！",
                        Data = response
                    });
                }

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, ex.Message });
            }
        }

        [HttpPost("sendCardMessage")]
        public async Task<IActionResult> SendCardMessage(SendMessageDto dto)
        {
            try
            {
                // 可以在这里添加业务逻辑，比如验证、日志等
                var response = await _wechatWorkService.SendMessageAsCardAsync(
                    users: dto.Users,
                    title : dto.Title,
                    description : dto.Description, // 卡片描述
                    url:dto.Url
                //url : "https://doc.weixin.qq.com/smartsheet/s3_AZcA3AYLALECN3AVCeR8AT1qu2tpw?scode=ADYAtQdGAGoovltNZDAZcA3AYLALE&version=5.0.6.6028&platform=win&tab=db_qj1WYl"
                );

                if (response.IsSuccessful())
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "发送卡片信息成功！"
                    });
                }
                else
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "发送失败！",
                        Data = response
                    });
                }

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, ex.Message });
            }
        }

        [HttpPost("createSmartSheet")]
        public async Task<IActionResult> CreateSmartSheet(string title,List<string>userIds)
        {
            var response = await _wechatWorkService.CreateSmartSheetAsync(title, userIds);
            if (response.IsSuccessful())
            {
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "创建成功！",
                    Data = response,
                });
            }
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = response.ErrorMessage
            });
        }

        [HttpPost("createSmartSheetAndNotify")]
        public async Task<IActionResult> CreateSmartSheetAndNotify(CreateAndNotifyDto dto)
        {
            try
            {
                // 1. 创建智能表格
                var createResponse = await _wechatWorkService.CreateSmartSheetAsync(dto.Title, dto.AdminUserIds);
                if (!createResponse.IsSuccessful())
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "创建智能表格失败：" + createResponse.ErrorMessage,
                        Data = createResponse
                    });
                }

                // 2. 发送通知消息
                var sendResponse = await _wechatWorkService.SendMessageAsCardAsync(
                    users: dto.NoticeUsers,
                    title: "智能表格已创建",
                    description: $"您创建的智能表格“{dto.Title}”已准备就绪，请点击查看。",
                    url: createResponse.Url
                );

                if (!sendResponse.IsSuccessful())
                {
                    // 创建成功但发送失败，可以返回部分成功
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "智能表格创建成功，但发送通知失败：" + sendResponse.ErrorMessage,
                        Data = new { CreateResult = createResponse, SendResult = sendResponse }
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "智能表格创建成功并已发送通知",
                    Data = new { CreateResult = createResponse, SendResult = sendResponse }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }
    }
}
