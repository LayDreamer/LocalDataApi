using Azure;
using LocalDataApi.Dto;
using LocalDataApi.Models;
using LocalDataApi.Services;
using Microsoft.AspNetCore.Mvc;
using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        public async Task<ActionResult<ApiResponse<object>>> GetUsers([FromBody] DepartmentRequestDto departmentRequest)
        {
            try
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
                    Message = $"[{response.ErrorCode}] {response.ErrorMessage}",
                    Data = new { response.ErrorCode, response.ErrorMessage }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("departments")]
        public async Task<ActionResult<ApiResponse<object>>> GetDepartments()
        {
            try
            {
                var response = await _wechatWorkService.GetDepartmentsAsync();
                if (response.IsSuccessful())
                {
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
                    Message = $"[{response.ErrorCode}] {response.ErrorMessage}",
                    Data = new { response.ErrorCode, response.ErrorMessage }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("send")]
        public async Task<ActionResult<ApiResponse<object>>> SendMessage(SendMessageDto dto)
        {
            try
            {
                // 可以在这里添加业务逻辑，比如验证、日志等
                var response = await _wechatWorkService.SendMessageAsync(
                    users: dto.Users,
                    content: dto.Content,
                    msgType: dto.MsgType
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
        public async Task<ActionResult<ApiResponse<object>>> SendCardMessage(SendMessageDto dto)
        {
            try
            {
                // 可以在这里添加业务逻辑，比如验证、日志等
                var response = await _wechatWorkService.SendMessageAsCardAsync(
                    users: dto.Users,
                    title: dto.Title,
                    description: dto.Description, // 卡片描述
                    url: dto.Url
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
        public async Task<ActionResult<ApiResponse<object>>> CreateSmartSheet(CreateSmartSheetDto createDto)
        {
            var response = await _wechatWorkService.CreateDocumentAsync(createDto.Title, createDto.AdminUserIds);
            if (response.IsSuccessful())
            {
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "智能表创建成功！",
                    Data = response,
                });
            }
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = response.ErrorMessage
            });
        }


        [HttpPost("addSmartSheetRecord")]
        public async Task<ActionResult<ApiResponse<object>>> AddSmartSheetRecord(string docId, string? sheetId)
        {
            var records = new List<IDictionary<string, object>>
                            {
                                new Dictionary<string, object>
                                {
                                    { "姓名", "孙磊" },
                                },
                                //, new Dictionary<string, object>
                                //{
                                //    { "文本", "首付" },
                                //    {"数字", 12 },
                                //    {"日期",DateTime.Now}
                                //}
                            };
            var response = await _wechatWorkService.AddSmartSheetRecordsAsync(docId, sheetId, records);
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

        [HttpPost("getSmartSheetRecord")]
        public async Task<ActionResult<ApiResponse<object>>> getSmartSheetRecord(string docId, string? sheetId)
        {
          
            var response = await _wechatWorkService.GetSmartSheetRecordsAsync(docId, sheetId);
            if (response.IsSuccessful())
            {
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "查询成功！",
                    Data = response,
                });
            }
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = response.ErrorMessage
            });
        }

        /// <summary>
        /// 创建企业微信群聊并发送消息
        /// </summary>
        [HttpPost("createChatAndSend")]
        public async Task<ActionResult<ApiResponse<object>>> CreateChatAndSendMessage(GroupChatMessageDto dto)
        {
            try
            {
                var (createResult, sendResult) = await _wechatWorkService.CreateChatAndSendMessageAsync(
                    userIds: dto.UserIds,
                    chatName: dto.ChatName,
                    ownerUserId: dto.OwnerUserId,
                    content: dto.Content,
                    msgType: dto.MsgType,
                    chatId: dto.ChatId,
                    title: dto.Title,
                    description: dto.Description,
                    url: dto.Url,
                    buttonText: dto.ButtonText
                );

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "群聊创建成功，消息已发送",
                    Data = new
                    {
                        ChatId = createResult.ChatId,
                        CreateResult = createResult,
                        SendResult = sendResult
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        /// <summary>
        /// 向已有的企业微信群聊发送消息
        /// </summary>
        [HttpPost("sendToGroupChat")]
        public async Task<ActionResult<ApiResponse<object>>> SendMessageToGroupChat(GroupChatMessageDto dto)
        {
            try
            {
                var response = await _wechatWorkService.SendMessageToGroupChatAsync(
                    chatId: dto.ChatId,
                    content: dto.Content,
                    msgType: dto.MsgType,
                    title: dto.Title,
                    description: dto.Description,
                    url: dto.Url,
                    buttonText: dto.ButtonText
                );

                if (response.IsSuccessful())
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "消息发送成功",
                        Data = response
                    });
                }

                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"发送失败: [{response.ErrorCode}] {response.ErrorMessage}",
                    Data = response
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取所有已创建的群聊列表（从数据库查询）
        /// </summary>
        [HttpGet("groupChats")]
        public async Task<ActionResult<ApiResponse<object>>> GetGroupChats()
        {
            try
            {
                var chats = await _wechatWorkService.GetAllGroupChatsAsync();
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"查询成功，共 {chats.Count} 个群聊",
                    Data = chats
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("createSmartSheetAndNotify")]
        public async Task<ActionResult<ApiResponse<object>>> CreateSmartSheetAndNotify(CreateAndNotifyDto dto)
        {
            try
            {
                // 1. 创建智能表格
                var createResponse = await _wechatWorkService.CreateDocumentAsync(dto.Title, dto.AdminUserIds);
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

        /// <summary>
        /// 获取 JS-SDK 配置（用于前端 wx.config 鉴权，调用扫码等能力）
        /// </summary>
        [HttpGet("jssdk-config")]
        public async Task<IActionResult> GetJsSdkConfig([FromQuery] string url)
        {
            try
            {
                var config = await _wechatWorkService.GetJsSdkConfigAsync(url);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "获取成功",
                    Data = config
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }


        //[HttpPost("chains")]
        //public async Task<IActionResult> GetChains()
        //{
        //    var response = await _wechatWorkService.GetChainsAsync();
        //    if (response.IsSuccessful())
        //    {
        //        return Ok(new ApiResponse<object>
        //        {
        //            Success = true,
        //            Message = "关联企业列表查询成功！",
        //            Data = response.ChainList,
        //        });
        //    }
        //    return BadRequest(new ApiResponse<object>
        //    {
        //        Success = false,
        //        Message = response.ErrorMessage
        //    });

        //}
        //[HttpPost("chainGroup")]
        //public async Task<IActionResult> GetChainGroup(string chainId)
        //{
        //    var response = await _wechatWorkService.GetChainGroupAsync(chainId);
        //    if (response.IsSuccessful())
        //    {
        //        return Ok(new ApiResponse<object>
        //        {
        //            Success = true,
        //            Message = "关联企业根目录查询成功！",
        //            Data = response.GroupList,
        //        });
        //    }
        //    return BadRequest(new ApiResponse<object>
        //    {
        //        Success = false,
        //        Message = response.ErrorMessage
        //    });

        //}
        //[HttpPost("chainGroupInfo")]
        //public async Task<IActionResult> GetChainGroup(string chainId, int? groupId)
        //{
        //    var response = await _wechatWorkService.GetChainGroupInfoListAsync(chainId, groupId);
        //    if (response.IsSuccessful())
        //    {
        //        return Ok(new ApiResponse<object>
        //        {
        //            Success = true,
        //            Message = "关联企业分组查询成功！",
        //            Data = response.GroupCorpList,
        //        });
        //    }
        //    return BadRequest(new ApiResponse<object>
        //    {
        //        Success = false,
        //        Message = response.ErrorMessage
        //    });

        //}

        //[HttpPost("linkedUserInfo")]
        //public async Task<IActionResult> GetLinkedUserInfo(string chainId, int? groupId)
        //{
        //    var response = await _wechatWorkService.GetLinkedCorpUserListAsync(chainId, groupId);
        //    if (response.IsSuccessful())
        //    {
        //        return Ok(new ApiResponse<object>
        //        {
        //            Success = true,
        //            Message = "关联企业员工列表查询成功！",
        //            Data = response.CorpUserList,
        //        });
        //    }
        //    return BadRequest(new ApiResponse<object>
        //    {
        //        Success = false,
        //        Message = response.ErrorMessage
        //    });

        //}


        //[HttpPost("sendSupplier")]
        //public async Task<IActionResult> SendMessageToSupplier(string corpId, string userId)
        //{

        //    var response = await _wechatWorkService.SendTextMessageToLinkedCorpUserAsync(corpId, userId, "您好，这是来自上游企业的一条测试消息。");
        //    if (response.IsSuccessful())
        //    {
        //        return Ok(new ApiResponse<object>
        //        {
        //            Success = true,
        //            Message = "发送成功！",
        //            Data = response.InvalidCorpUserIdList,
        //        });
        //    }
        //    return BadRequest(new ApiResponse<object>
        //    {
        //        Success = false,
        //        Message = response.ErrorMessage
        //    });

        //}
    }
}