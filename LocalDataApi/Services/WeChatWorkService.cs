using Azure;
using Azure.Core;
using LocalDataApi.WeChatWork;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Crmf;
using Org.BouncyCastle.Asn1.Ocsp;
using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Models;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using static SKIT.FlurlHttpClient.Wechat.Work.Models.CgibinDepartmentListResponse.Types.Department;

namespace LocalDataApi.Services
{
    public class WeChatWorkService(WechatWorkClient client, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<WeChatWorkService> logger)
    {
        private readonly WechatWorkClient _client = client;
        private readonly IConfiguration _config = config;
        private readonly ILogger<WeChatWorkService> _logger = logger;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly int _agentId = config.GetValue<int>("WechatWork:AgentId"); // 从配置中读取应用ID
        private string? _accessToken;
        private DateTime _tokenExpireTime;
        
        #region 公司组织架构相关接口

        // 获取部门列表
        public async Task<CgibinDepartmentListResponse> GetDepartmentsAsync()
        {
            await GetAccessTokenAsync();
            var request = new CgibinDepartmentListRequest() { AccessToken = _accessToken };
            return await _client.ExecuteCgibinDepartmentListAsync(request);
        }

        /// <summary>
        /// 将平面部门列表转换为嵌套树形结构
        /// </summary>
        /// <param name="departments">平面部门列表</param>
        /// <returns>嵌套的部门树形结构</returns>
        public List<DepartmentTreeNode> BuildDepartmentTree(List<CgibinDepartmentListResponse.Types.Department> departments)
        {
            if (departments == null || departments.Count == 0)
            {
                return new List<DepartmentTreeNode>();
            }

            // 创建所有部门的字典，方便快速查找
            var departmentDict = departments.ToDictionary(d => d.DepartmentId, d => new DepartmentTreeNode
            {
                Id = d.DepartmentId,
                Name = d.Name,
                ParentId = d.ParentDepartmentId,
                Order = d.DepartmentOrder,
                DepartmentLeader = d.LeaderUserIdList?.ToList(),
                Children = new List<DepartmentTreeNode>()
            });

            // 根节点列表
            var rootNodes = new List<DepartmentTreeNode>();

            // 遍历所有部门，构建树形结构
            foreach (var kvp in departmentDict)
            {
                var department = kvp.Value;

                if (department.ParentId == 0)
                {
                    // 根节点
                    rootNodes.Add(department);
                }
                else if (departmentDict.ContainsKey(department.ParentId))
                {
                    // 子节点，添加到父节点的Children列表
                    departmentDict[department.ParentId].Children.Add(department);
                }
            }

            // 对每个节点的Children按order排序
            SortChildren(rootNodes);

            return rootNodes;
        }

        /// <summary>
        /// 递归排序子节点
        /// </summary>
        private void SortChildren(List<DepartmentTreeNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return;

            // 按order排序
            nodes.Sort((a, b) => a.Order.CompareTo(b.Order));

            // 递归排序子节点
            foreach (var node in nodes)
            {
                SortChildren(node.Children);
            }
        }

        /// <summary>
        /// 获取扁平化的部门路径（从根节点到当前节点）
        /// </summary>
        /// <param name="departmentId">部门ID</param>
        /// <param name="departments">所有部门列表</param>
        /// <returns>部门路径列表</returns>
        public List<CgibinDepartmentListResponse.Types.Department> GetDepartmentPath(int departmentId, List<CgibinDepartmentListResponse.Types.Department> departments)
        {
            var path = new List<CgibinDepartmentListResponse.Types.Department>();
            var departmentDict = departments.ToDictionary(d => d.DepartmentId);

            if (!departmentDict.ContainsKey(departmentId))
            {
                return path;
            }

            var current = departmentDict[departmentId];
            while (current != null)
            {
                path.Insert(0, current);

                if (current.ParentDepartmentId == 0)
                {
                    break;
                }

                if (departmentDict.ContainsKey(current.ParentDepartmentId))
                {
                    current = departmentDict[current.ParentDepartmentId];
                }
                else
                {
                    break;
                }
            }

            return path;
        }

        /// <summary>
        /// 获取所有子部门ID（包括递归子部门）
        /// </summary>
        /// <param name="parentId">父部门ID</param>
        /// <param name="departments">所有部门列表</param>
        /// <returns>所有子部门ID列表</returns>
        public List<long> GetAllChildDepartmentIds(int parentId, List<CgibinDepartmentListResponse.Types.Department> departments)
        {
            var result = new List<long>();
            var departmentDict = departments.ToDictionary(d => d.DepartmentId);

            CollectChildDepartmentIds(parentId, departmentDict, result);
            return result;
        }

        /// <summary>
        /// 递归收集子部门ID
        /// </summary>
        private void CollectChildDepartmentIds(long parentId, Dictionary<long, CgibinDepartmentListResponse.Types.Department> departmentDict, List<long> result)
        {
            var children = departmentDict.Values.Where(d => d.ParentDepartmentId == parentId);

            foreach (var child in children)
            {
                result.Add(child.DepartmentId);
                CollectChildDepartmentIds(child.DepartmentId, departmentDict, result);
            }
        }


        /// <summary>
        /// 部门树节点
        /// </summary>
        public class DepartmentTreeNode
        {
            public long Id { get; set; }
            public string Name { get; set; }
            public long ParentId { get; set; }
            public long Order { get; set; }
            public List<string> DepartmentLeader { get; set; }
            public List<DepartmentTreeNode> Children { get; set; }
        }

        #endregion


        /// <summary>
        /// 获取指定部门下的成员列表（包含详情）
        /// </summary>
        /// <param name="departmentId">部门ID，默认根部门(1)</param>
        /// <returns></returns>
        public async Task<CgibinUserListResponse> GetDepartmentUsersAsync(int departmentId, bool fetchChild = true)
        {
            await GetAccessTokenAsync();
            var request = new CgibinUserListRequest
            {
                AccessToken = _accessToken,
                DepartmentId = departmentId,
                RequireFetchChild = fetchChild
            };
            return await _client.ExecuteCgibinUserListAsync(request);
        }


        #region 获取上下游企业相关接口

        //获取上下游根目录
        public async Task<CgibinCorpGroupCorpGetChainListResponse> GetChainsAsync()
        {
            await GetAccessTokenAsync();
            var request = new CgibinCorpGroupCorpGetChainListRequest()
            {
                AccessToken= _accessToken   
            };
            return await _client.ExecuteCgibinCorpGroupCorpGetChainListAsync(request);
        }

        //获取上下游分组列表
        public async Task<CgibinCorpGroupCorpGetChainGroupResponse> GetChainGroupAsync(string chainId)
        {
            await GetAccessTokenAsync();
            var request = new CgibinCorpGroupCorpGetChainGroupRequest()
            {
                AccessToken = _accessToken,
                ChainId = chainId,
            };
            return await _client.ExecuteCgibinCorpGroupCorpGetChainGroupAsync(request);
        }

        //获取互联企业列表（查询指定互联企业/上下游的企业列表）
        public async Task<CgibinCorpGroupCorpGetChainCorpInfoListResponse> GetChainGroupInfoListAsync(string chainId,int? groupId)
        {
            await GetAccessTokenAsync();
            var request = new CgibinCorpGroupCorpGetChainCorpInfoListRequest()
            {
                AccessToken = _accessToken,
                ChainId = chainId, 
                GroupId= groupId
            };
            return await _client.ExecuteCgibinCorpGroupCorpGetChainCorpInfoListAsync(request);
        }

        public async Task<CgibinLinkedCorpUserListResponse> GetLinkedCorpUserListAsync(string chainId, int? groupId)
        {
            await GetAccessTokenAsync();
            var request = new CgibinLinkedCorpUserListRequest()
            {
                AccessToken = _accessToken,
                LinkedDepartmentId = $"{chainId}/{groupId}", // 互联企业部门ID格式为 "企业ID/部门ID"
                RequireFetchChild =true
            };
            return await _client.ExecuteCgibinLinkedCorpUserListAsync(request);
        }

        /// <summary>
        /// 获取互联企业部门列表（查询指定互联企业/上下游的部门结构）
        /// </summary>
        public async Task<CgibinLinkedCorpDepartmentListResponse> GetLinkedCorpDepartmentsAsync(string corpId, string? deptId = null)
        {
            await GetAccessTokenAsync();

            // 正确拼接：企业ID/部门ID，如果 deptId 为 null，则默认查询根部门(/1)
            string departmentId = string.IsNullOrEmpty(deptId) ? $"{corpId}/1" : $"{corpId}/{deptId}";

            var request = new CgibinLinkedCorpDepartmentListRequest
            {
                AccessToken = _accessToken,
                LinkedDepartmentId = departmentId  
            };

            return await _client.ExecuteCgibinLinkedCorpDepartmentListAsync(request);
        }

        // 获取互联企业部门成员（查询指定部门下的成员列表）
        public async Task<CgibinLinkedCorpUserListResponse> GetLinkedCorpUsersAsync(string linkedDepartmentId,bool requireFetchChild=true)
        {
            await GetAccessTokenAsync();
            var request = new CgibinLinkedCorpUserListRequest
            {
                AccessToken = _accessToken,
                LinkedDepartmentId = linkedDepartmentId,
                RequireFetchChild = requireFetchChild
            };

            return await _client.ExecuteCgibinLinkedCorpUserListAsync(request); 
        }

        /// <summary>
        ///  获取互联企业成员详细信息（根据成员ID获取其详细信息，不含敏感字段）
        /// </summary>
        /// <param name="userId">上下游成员ID，格式为"企业ID/成员ID"（必填）</param>
        public async Task<CgibinLinkedCorpUserGetResponse> GetLinkedCorpUserDetailAsync(string userId)
        {
            await GetAccessTokenAsync();
            var request = new CgibinLinkedCorpUserGetRequest
            {
                AccessToken=_accessToken,
                CorpUserId= userId // 格式为"企业ID/成员ID"
            };
            return await _client.ExecuteCgibinLinkedCorpUserGetAsync(request);
        }


        /// <summary>
        /// 获取下游企业的 AccessToken（用于代表下游企业调用接口）
        /// </summary>
        /// <param name="corpId">下游企业的CorpId（如 "wpNrVGagAATR5j8L4lli59Bd0rzseVOw"）</param>
        /// <returns>包含下游企业 AccessToken 的响应对象</returns>
        public async Task<CgibinCorpGroupCorpGetTokenResponse> GetCorpGroupTokenAsync(string corpId)
        {
            // 确保你有上游企业的 AccessToken
            await GetAccessTokenAsync();
            var request = new CgibinCorpGroupCorpGetTokenRequest
            {
                AccessToken = _accessToken,      // 这是你上游企业的 AccessToken
                CorpId = corpId,                   // 要授权的下游企业 CorpId
                AgentId = _agentId                  // 你的应用ID
            };
            return await _client.ExecuteCgibinCorpGroupCorpGetTokenAsync(request);
        }

        /// <summary>
        /// 给下游企业的某个成员发送文本消息
        /// </summary>
        /// <param name="corpId">下游企业的CorpId</param>
        /// <param name="userId">接收消息的成员ID，格式为 "企业ID/成员ID"（例如 "wpNrVGagAATR5j8L4lli59Bd0rzseVOw/zhangsan"）</param>
        /// <param name="content">消息内容</param>
        /// <returns>发送消息的响应对象</returns>
        public async Task<CgibinLinkedCorpMessageSendResponse> SendTextMessageToLinkedCorpUserAsync(string corpId, string userId, string content)
        {
            // 1. 先获取下游企业的 Token
            var tokenResponse = await GetCorpGroupTokenAsync(corpId);
            if (!tokenResponse.IsSuccessful())
            {
                // 处理获取token失败的情况
                throw new Exception($"获取下游企业Token失败: {tokenResponse.ErrorMessage}");
            }
            string downstreamAccessToken = tokenResponse.AccessToken;

            // 2. 构造发送消息的请求（注意：这是互联企业消息推送的接口）
            var request = new CgibinLinkedCorpMessageSendRequest
            {
                AccessToken = downstreamAccessToken, // 使用下游企业的 Token
                ToCorpUserIdList = [userId],      // 接收者，这里是完整的 "企业ID/成员ID"
                AgentId = _agentId,               // 你的应用ID
                MessageType = "text",             // 消息类型
                MessageContentAsText = new CgibinLinkedCorpMessageSendRequest.Types.TextMessage()
                {
                    Content = content
                },
                IsSafe = false
            };

            return await _client.ExecuteCgibinLinkedCorpMessageSendAsync(request);
        }


        #endregion

        #region 消息发送
        /// <summary>
        /// 发送消息接口（支持文本、markdown等多种消息类型，接收者可以是成员/部门/标签）
        /// </summary>
        public async Task<CgibinMessageSendResponse> SendMessageAsync(
                       List<string> users,          // 接收消息的成员ID列表
                       string content,                // 消息内容（文本/markdown等）
                       WechatWorkMessageType msgType,
                       bool isSafe = false)           // 是否保密消息
        {
            await GetAccessTokenAsync();

            // 2. 构建基础请求对象
            var request = new CgibinMessageSendRequest
            {
                AccessToken = _accessToken,
                ToUserIdList = users,
                ToDepartmentIdList = null, // 部门ID列表
                ToTagIdList = null, // 标签ID列表
                IsSafe = isSafe,
            };            
            // 3. 根据消息类型设置对应的消息内容和 MsgType
            switch (msgType)
            {
                case WechatWorkMessageType.Text:
                    request.MessageType = "text";
                    request.MessageContentAsText = new CgibinMessageSendRequest.Types.TextMessage
                    {
                        Content = content
                    };
                    break;

                case WechatWorkMessageType.Markdown:
                    request.MessageType = "markdown";
                    request.MessageContentAsMarkdown = new CgibinMessageSendRequest.Types.MarkdownMessage
                    {
                        Content = content
                    };
                    break;
                    //request.MessageContentAsTextCard = new CgibinMessageSendRequest.Types.TextCardMessage
                    //{
                    //    Title = "2026研发项目管理智能表",
                    //    Description = "这是本月项目进度的汇总表，请及时更新您的任务状态。", // 卡片描述
                    //    Url = "https://doc.weixin.qq.com/smartsheet/s3_AZcA3AYLALECN3AVCeR8AT1qu2tpw?scode=ADYAtQdGAGoovltNZDAZcA3AYLALE&version=5.0.6.6028&platform=win&tab=db_qj1WYl", // 智能表格链接
                    //    ButtonText = "查看详情" // 可选，按钮文字，默认“详情”
                    //};
                    //break;

                // 示例：图片消息（需要传入图片 media_id）
                // case WechatWorkMessageType.Image:
                //     request.MessageType = "image";
                //     request.MessageContentAsImage = new CgibinMessageSendRequest.Types.ImageMessage
                //     {
                //         MediaId = content   // content 此时应为 media_id
                //     };
                //     break;

                // 其他消息类型可继续添加...

                default:
                    throw new NotSupportedException($"不支持的消息类型: {msgType}");
            }

            // 4. 发送请求
            var response = await _client.ExecuteCgibinMessageSendAsync(request);

            // 5. 日志记录
            if (response.IsSuccessful())
            {
                _logger.LogInformation("消息发送成功，MsgId: {MsgId}", response.MessageId);
            }
            else
            {
                _logger.LogError("发送消息失败: {ErrorCode} - {ErrorMessage}", response.ErrorCode, response.ErrorMessage);
            }

            return response;
        }



        //发送文本卡片消息（适合发送智能表格链接等场景）
        public async Task<CgibinMessageSendResponse> SendMessageAsCardAsync(
                      List<string> users,         
                        string title,
                        string description,
                        string url,
                        string buttontText="查看详情")          
        {
            await GetAccessTokenAsync();

            // 2. 构建基础请求对象
            var request = new CgibinMessageSendRequest
            {
                AccessToken = _accessToken,
                ToUserIdList = users,
                MessageType = "textcard",
                ToDepartmentIdList = null, // 部门ID列表
                ToTagIdList = null, // 标签ID列表
                IsSafe = false,
                MessageContentAsTextCard = new CgibinMessageSendRequest.Types.TextCardMessage
                {
                    Title = title,
                    Description = description, // 卡片描述
                    Url = url, // 智能表格链接
                    ButtonText = "查看详情" // 可选，按钮文字，默认“详情”
                },
            };
            

            // 4. 发送请求
            var response = await _client.ExecuteCgibinMessageSendAsync(request);

            // 5. 日志记录
            if (response.IsSuccessful())
            {
                _logger.LogInformation("消息发送成功，MsgId: {MsgId}", response.MessageId);
            }
            else
            {
                _logger.LogError("发送消息失败: {ErrorCode} - {ErrorMessage}", response.ErrorCode, response.ErrorMessage);
            }

            return response;
        }


        #endregion

        #region 智能表格相关接口

        /// <summary>
        /// 创建一个智能表格
        /// </summary>
        public async Task<CgibinWedocCreateDocumentResponse> CreateSmartSheetAsync(string title, List<string>userIds, string? parentId = null)
        {
            // 1. 获取访问令牌
            await GetAccessTokenAsync();

            // 2. 构建创建请求
            var request = new CgibinWedocCreateDocumentRequest
            {
                AccessToken = _accessToken,
                DocumentName = title,
                DocumentType = 10, // 固定为10，代表智能表格
                AdminUserIdList= userIds
            };

            // 3. 执行请求
            return  await _client.ExecuteCgibinWedocCreateDocumentAsync(request);
        }


        public async Task<CgibinWedocDeleteDocumentResponse> DeleteSmartSheetAsync(string documentId)
        {
            // 1. 获取访问令牌
            await GetAccessTokenAsync();

            // 2. 构建创建请求
            var request = new CgibinWedocDeleteDocumentRequest
            {
                AccessToken = _accessToken,
                DocumentId= documentId,
            };
            // 3. 执行请求
            return await _client.ExecuteCgibinWedocDeleteDocumentAsync(request);
        }
        #endregion



        #region  获取 AccessToken

        // 获取 AccessToken 的方法（使用 SDK）
        public async Task<string> GetAccessTokenAsync()
        {
            var request = new CgibinGetTokenRequest();
            var response = await _client.ExecuteCgibinGetTokenAsync(request);
            if (response.IsSuccessful())
            {
                _accessToken = response.AccessToken;
                //_tokenExpireTime = DateTime.Now.AddSeconds(response.ExpiresIn).AddMinutes(-5);
                _logger.LogInformation("Token 刷新成功，值为{AccessToken}", _accessToken);
                //_logger.LogInformation("Token 刷新成功，值为{AccessToken}, 有效期至 {ExpireTime}",_accessToken, _tokenExpireTime);
                return _accessToken;
            }
            throw new Exception($"获取Token失败: {response.ErrorMessage}");
        }


        //手动实现获取 AccessToken 的方法（不使用 SDK，直接调用接口）
        private async Task<string> GetAccessTokenAsync1()
        {
            // 如果已有有效 Token，直接返回
            if (!string.IsNullOrEmpty(_accessToken) && _tokenExpireTime > DateTime.Now)
            {
                return _accessToken;
            }

            // 手动调用企业微信获取 Token 的接口
            var corpId = _config["WechatWork:CorpId"];
            var secret = _config["WechatWork:AgentSecret"];
            var url = $"https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid={corpId}&corpsecret={secret}";

            using var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            // 解析响应（手动或使用 JsonDocument）
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.TryGetProperty("access_token", out var tokenElement))
            {
                _accessToken = tokenElement.GetString();
                var expiresIn = root.GetProperty("expires_in").GetInt32();
                // 提前5分钟过期，避免边界问题
                _tokenExpireTime = DateTime.Now.AddSeconds(expiresIn).AddMinutes(-5);
                _logger.LogInformation("Token 刷新成功，有效期至 {ExpireTime}", _tokenExpireTime);
                return _accessToken;
            }
            else
            {
                var errCode = root.GetProperty("errcode").GetInt32();
                var errMsg = root.GetProperty("errmsg").GetString();
                throw new Exception($"获取 Token 失败: {errCode} - {errMsg}");
            }
        }

        #endregion
    }
}
