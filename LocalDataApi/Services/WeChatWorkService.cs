using Azure;
using Azure.Core;
using LocalDataApi.Data;
using LocalDataApi.Models;
using LocalDataApi.WeChatWork;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Crmf;
using Org.BouncyCastle.Asn1.Ocsp;
using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Models;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace LocalDataApi.Services
{
    public class WeChatWorkService(
        WechatWorkClient client,
        WechatWorkTokenProvider tokenProvider,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<WeChatWorkService> logger,
        AppDbContext context)
    {
        private readonly WechatWorkClient _client = client;
        private readonly WechatWorkTokenProvider _tokenProvider = tokenProvider;
        private readonly IConfiguration _config = config;
        private readonly ILogger<WeChatWorkService> _logger = logger;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly AppDbContext _context = context;
        private readonly int _agentId = config.GetValue<int>("WechatWork:AgentId"); // 从配置中读取应用ID

        #region 企业组织架构相关接口

        // 获取部门列表
        public async Task<CgibinDepartmentListResponse> GetDepartmentsAsync(CancellationToken ct = default)
        {
            var response = await ExecuteWithTokenRefreshAsync(
                async token =>
                {
                    var request = new CgibinDepartmentListRequest() { AccessToken = token };
                    return await _client.ExecuteCgibinDepartmentListAsync(request);
                }, ct);

            if (!response.IsSuccessful())
            {
                _logger.LogError(
                    "获取部门列表失败: [{ErrorCode}] {ErrorMessage} (提示: 错误码 60011 表示服务器出口IP未配置可信IP)",
                    response.ErrorCode, response.ErrorMessage);
            }

            return response;
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


        #endregion

        #region 企业成员相关接口
        /// <summary>
        /// 获取指定部门下的成员列表（包含详情）
        /// </summary>
        /// <param name="departmentId">部门ID，默认根部门(1)</param>
        /// <returns></returns>
        public async Task<CgibinUserListResponse> GetDepartmentUsersAsync(int departmentId, bool fetchChild = true, CancellationToken ct = default)
        {
            var response = await ExecuteWithTokenRefreshAsync(
                async token =>
                {
                    var request = new CgibinUserListRequest
                    {
                        AccessToken = token,
                        DepartmentId = departmentId,
                        RequireFetchChild = fetchChild
                    };
                    return await _client.ExecuteCgibinUserListAsync(request);
                }, ct);

            if (!response.IsSuccessful())
            {
                _logger.LogError(
                    "获取部门成员失败: [{ErrorCode}] {ErrorMessage} (提示: 错误码 60011 表示服务器出口IP未配置可信IP)",
                    response.ErrorCode, response.ErrorMessage);
            }

            return response;
        }

        #endregion

        #region 获取上下游企业相关接口

        //获取上下游根目录
        public async Task<CgibinCorpGroupCorpGetChainListResponse> GetChainsAsync(CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinCorpGroupCorpGetChainListRequest()
            {
                AccessToken = accessToken
            };
            return await _client.ExecuteCgibinCorpGroupCorpGetChainListAsync(request);
        }

        //获取上下游分组列表
        public async Task<CgibinCorpGroupCorpGetChainGroupResponse> GetChainGroupAsync(string chainId, CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinCorpGroupCorpGetChainGroupRequest()
            {
                AccessToken = accessToken,
                ChainId = chainId,
            };
            return await _client.ExecuteCgibinCorpGroupCorpGetChainGroupAsync(request);
        }

        //获取互联企业列表（查询指定互联企业/上下游的企业列表）
        public async Task<CgibinCorpGroupCorpGetChainCorpInfoListResponse> GetChainGroupInfoListAsync(string chainId, int? groupId, CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinCorpGroupCorpGetChainCorpInfoListRequest()
            {
                AccessToken = accessToken,
                ChainId = chainId,
                GroupId = groupId
            };
            return await _client.ExecuteCgibinCorpGroupCorpGetChainCorpInfoListAsync(request);
        }

        public async Task<CgibinLinkedCorpUserListResponse> GetLinkedCorpUserListAsync(string chainId, int? groupId, CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinLinkedCorpUserListRequest()
            {
                AccessToken = accessToken,
                LinkedDepartmentId = $"{chainId}/{groupId}", // 互联企业部门ID格式为 "企业ID/部门ID"
                RequireFetchChild = true
            };
            return await _client.ExecuteCgibinLinkedCorpUserListAsync(request);
        }

        /// <summary>
        /// 获取互联企业部门列表（查询指定互联企业/上下游的部门结构）
        /// </summary>
        public async Task<CgibinLinkedCorpDepartmentListResponse> GetLinkedCorpDepartmentsAsync(string corpId, string? deptId = null, CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);

            // 正确拼接：企业ID/部门ID，如果 deptId 为 null，则默认查询根部门(/1)
            string departmentId = string.IsNullOrEmpty(deptId) ? $"{corpId}/1" : $"{corpId}/{deptId}";

            var request = new CgibinLinkedCorpDepartmentListRequest
            {
                AccessToken = accessToken,
                LinkedDepartmentId = departmentId
            };

            return await _client.ExecuteCgibinLinkedCorpDepartmentListAsync(request);
        }

        // 获取互联企业部门成员（查询指定部门下的成员列表）
        public async Task<CgibinLinkedCorpUserListResponse> GetLinkedCorpUsersAsync(string linkedDepartmentId, bool requireFetchChild = true, CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinLinkedCorpUserListRequest
            {
                AccessToken = accessToken,
                LinkedDepartmentId = linkedDepartmentId,
                RequireFetchChild = requireFetchChild
            };

            return await _client.ExecuteCgibinLinkedCorpUserListAsync(request);
        }

        /// <summary>
        ///  获取互联企业成员详细信息（根据成员ID获取其详细信息，不含敏感字段）
        /// </summary>
        /// <param name="userId">上下游成员ID，格式为"企业ID/成员ID"（必填）</param>
        public async Task<CgibinLinkedCorpUserGetResponse> GetLinkedCorpUserDetailAsync(string userId, CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinLinkedCorpUserGetRequest
            {
                AccessToken = accessToken,
                CorpUserId = userId // 格式为"企业ID/成员ID"
            };
            return await _client.ExecuteCgibinLinkedCorpUserGetAsync(request);
        }


        /// <summary>
        /// 获取下游企业的 AccessToken（用于代表下游企业调用接口）
        /// </summary>
        /// <param name="corpId">下游企业的CorpId（如 "wpNrVGagAATR5j8L4lli59Bd0rzseVOw"）</param>
        /// <returns>包含下游企业 AccessToken 的响应对象</returns>
        public async Task<CgibinCorpGroupCorpGetTokenResponse> GetCorpGroupTokenAsync(string corpId, CancellationToken ct = default)
        {
            // 确保你有上游企业的 AccessToken
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinCorpGroupCorpGetTokenRequest
            {
                AccessToken = accessToken,      // 这是你上游企业的 AccessToken
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
        public async Task<CgibinLinkedCorpMessageSendResponse> SendTextMessageToLinkedCorpUserAsync(string corpId, string userId, string content, CancellationToken ct = default)
        {
            // 1. 先获取下游企业的 Token
            var tokenResponse = await GetCorpGroupTokenAsync(corpId, ct);
            if (!tokenResponse.IsSuccessful())
            {
                throw new InvalidOperationException($"获取下游企业Token失败: [{tokenResponse.ErrorCode}] {tokenResponse.ErrorMessage}");
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
                       List<string> users,
                       string content,
                       WechatWorkMessageType msgType,
                       bool isSafe = false,
                       CancellationToken ct = default)
        {
            if (users == null || users.Count == 0 || users.All(string.IsNullOrWhiteSpace))
                throw new ArgumentException("接收者列表不能为空", nameof(users));
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("消息内容不能为空", nameof(content));

            users = users.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);

            var request = new CgibinMessageSendRequest
            {
                AccessToken = accessToken,
                ToUserIdList = users,
                ToDepartmentIdList = null,
                ToTagIdList = null,
                IsSafe = isSafe,
            };

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
                // case WechatWorkMessageType.Card:
                //     request.MessageType = "textcard";
                //     request.MessageContentAsTextCard = new CgibinMessageSendRequest.Types.TextCardMessage
                //     {
                //         Title = "永创系统后台通知",
                //         Description = content,
                //         Url = url,
                //          ButtonText = "查看详情"
                //     };
                //     break;

                default:
                    throw new NotSupportedException($"不支持的消息类型: {msgType}");
            }

            var response = await ExecuteWithTokenRefreshAsync(
                async token =>
                {
                    request.AccessToken = token;
                    return await _client.ExecuteCgibinMessageSendAsync(request);
                }, ct);

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
                        string buttontText = "查看详情",
                        CancellationToken ct = default)
        {
            if (users == null || users.Count == 0 || users.All(string.IsNullOrWhiteSpace))
                throw new ArgumentException("接收者列表不能为空", nameof(users));

            users = users.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);

            var request = new CgibinMessageSendRequest
            {
                AccessToken = accessToken,
                ToUserIdList = users,
                MessageType = "textcard",
                ToDepartmentIdList = null,
                ToTagIdList = null,
                IsSafe = false,
                MessageContentAsTextCard = new CgibinMessageSendRequest.Types.TextCardMessage
                {
                    Title = title,
                    Description = description,
                    Url = url,
                    ButtonText = buttontText
                },
            };

            var response = await ExecuteWithTokenRefreshAsync(
                async token =>
                {
                    request.AccessToken = token;
                    return await _client.ExecuteCgibinMessageSendAsync(request);
                }, ct);

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
        public async Task<CgibinWedocCreateDocumentResponse> CreateDocumentAsync(string title, List<string> userIds, string? parentId = null, CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinWedocCreateDocumentRequest
            {
                AccessToken = accessToken,
                DocumentName = title,
                DocumentType = 10,
                AdminUserIdList = userIds
            };
            return await _client.ExecuteCgibinWedocCreateDocumentAsync(request);
        }

        //删除智能表格(doc)
        public async Task<CgibinWedocDeleteDocumentResponse> DeleteDocumentAsync(string documentId, CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinWedocDeleteDocumentRequest
            {
                AccessToken = accessToken,
                DocumentId = documentId,
            };
            return await _client.ExecuteCgibinWedocDeleteDocumentAsync(request);
        }

        //添加智能表格子表（sheet）
        public async Task<CgibinWedocSmartSheetAddSheetResponse> SmartSheetAddSheetAsync(string documentId, CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinWedocSmartSheetAddSheetRequest
            {
                AccessToken = accessToken,
                DocumentId = documentId,
                Sheet = new CgibinWedocSmartSheetAddSheetRequest.Types.Sheet
                {
                    Title = "测试表1",
                }
            };
            return await _client.ExecuteCgibinWedocSmartSheetAddSheetAsync(request);
        }

        // 获取智能表格的所有子表信息
        public async Task<List<CgibinWedocSmartSheetGetSheetResponse.Types.Sheet>> GetSheetsAsync(string docId, CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinWedocSmartSheetGetSheetRequest
            {
                AccessToken = accessToken,
                DocumentId = docId
            };

            var response = await _client.ExecuteCgibinWedocSmartSheetGetSheetAsync(request);

            if (response.IsSuccessful() && response.ErrorCode == 0)
            {
                return response.SheetList?.ToList() ?? new List<CgibinWedocSmartSheetGetSheetResponse.Types.Sheet>();
            }
            else
            {
                throw new InvalidOperationException($"查询子表失败: [{response.ErrorCode}] {response.ErrorMessage}");
            }
        }

        // 向智能表格的指定子表中添加记录（行）
        public async Task<CgibinWedocSmartSheetAddRecordsResponse> AddSmartSheetRecordsAsync(
            string docId,
            string? sheetId,
            IList<IDictionary<string, object>> records,
            CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);

            if (string.IsNullOrEmpty(sheetId))
            {
                sheetId = await GetDefaultSheetIdAsync(docId, ct);
            }

            var fields = await GetFieldsAsync(docId, sheetId, ct);
            var fieldNameToIdMap = fields.FieldList.ToDictionary(f => f.Title, f => f.FieldId);
            var fieldIdToTypeMap = fields.FieldList.ToDictionary(f => f.FieldId, f => f.Type);

            var (missingFields, fieldNameToInferredType) = AnalyzeMissingFields(records, fieldNameToIdMap.Keys);

            if (missingFields.Any())
            {
                await CreateMissingFieldsAsync(docId, sheetId, missingFields, fieldNameToInferredType, accessToken);
                fields = await GetFieldsAsync(docId, sheetId, ct);
                fieldNameToIdMap = fields.FieldList.ToDictionary(f => f.Title, f => f.FieldId);
                fieldIdToTypeMap = fields.FieldList.ToDictionary(f => f.FieldId, f => f.Type);
            }

            var recordList = BuildRecordList(records, fieldNameToIdMap, fieldIdToTypeMap);

            var addRequest = new CgibinWedocSmartSheetAddRecordsRequest
            {
                AccessToken = accessToken,
                DocumentId = docId,
                SheetId = sheetId,
                RecordList = recordList,
                KeyType = "CELL_VALUE_KEY_TYPE_FIELD_ID"
            };

            return await _client.ExecuteCgibinWedocSmartSheetAddRecordsAsync(addRequest);
        }

        //获取智能表格相关数据信息
        public async Task<CgibinWedocSmartSheetGetRecordsResponse> GetSmartSheetRecordsAsync(
          string docId,
          string? sheetId,
          CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinWedocSmartSheetGetRecordsRequest
            {
                AccessToken = accessToken,
                DocumentId = docId,
                SheetId = sheetId,
            };
            return await _client.ExecuteCgibinWedocSmartSheetGetRecordsAsync(request);
        }

        //删除智能表格的指定子表中的记录（行）
        public async Task<CgibinWedocSmartSheetDeleteRecordsResponse> DeleteSmartSheetRecordsAsync(
         string docId,
         string? sheetId,
         IList<string> recordIds,
         CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinWedocSmartSheetDeleteRecordsRequest
            {
                AccessToken = accessToken,
                DocumentId = docId,
                SheetId = sheetId,
                RecordIdList = recordIds
            };
            return await _client.ExecuteCgibinWedocSmartSheetDeleteRecordsAsync(request);
        }

        //更新智能表格的指定子表中的记录（行）
        public async Task<CgibinWedocSmartSheetUpdateRecordsResponse> UpdateSmartSheetRecordsAsync(
         string docId,
         string? sheetId,
         IList<CgibinWedocSmartSheetUpdateRecordsRequest.Types.Record> recordList,
         CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinWedocSmartSheetUpdateRecordsRequest
            {
                AccessToken = accessToken,
                DocumentId = docId,
                SheetId = sheetId,
                KeyType = "CELL_VALUE_KEY_TYPE_FIELD_ID",
                RecordList = recordList
            };
            return await _client.ExecuteCgibinWedocSmartSheetUpdateRecordsAsync(request);
        }

        // 获取默认子表的SheetId
        public async Task<string> GetDefaultSheetIdAsync(string docId, CancellationToken ct = default)
        {
            var sheets = await GetSheetsAsync(docId, ct);
            var defaultSheet = sheets.FirstOrDefault(s => s.Type == "smartsheet");

            if (defaultSheet != null)
            {
                return defaultSheet.SheetId;
            }
            throw new InvalidOperationException("未找到智能表格类型的子表");
        }

        // 获取智能表格指定子表的字段列表
        public async Task<CgibinWedocSmartSheetGetFieldsResponse> GetFieldsAsync(
            string docId,
            string sheetId,
            CancellationToken ct = default)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
            var request = new CgibinWedocSmartSheetGetFieldsRequest
            {
                AccessToken = accessToken,
                DocumentId = docId,
                SheetId = sheetId
            };
            return await _client.ExecuteCgibinWedocSmartSheetGetFieldsAsync(request);
        }

        // 分析记录中使用的字段，找出缺失字段并推断类型。
        private (List<string> MissingFields, Dictionary<string, string> FieldNameToInferredType) AnalyzeMissingFields(
            IEnumerable<IDictionary<string, object>> records,
            ICollection<string> existingFieldNames)
        {
            var allFieldNames = new HashSet<string>();
            var fieldNameToInferredType = new Dictionary<string, string>();

            foreach (var record in records)
            {
                foreach (var kv in record)
                {
                    var fieldName = kv.Key;
                    var value = kv.Value;
                    allFieldNames.Add(fieldName);
                    // 仅当该字段尚未推断类型且值非空时进行推断
                    if (value != null && !fieldNameToInferredType.ContainsKey(fieldName))
                    {
                        fieldNameToInferredType[fieldName] = InferFieldType(value);
                    }
                }
            }

            // 找出缺失字段（不在现有字段集合中）
            var missingFields = allFieldNames.Where(name => !existingFieldNames.Contains(name)).ToList();

            // 对于缺失字段但从未推断出类型的（所有记录该字段都为空），默认设为文本类型
            foreach (var fieldName in missingFields.Where(name => !fieldNameToInferredType.ContainsKey(name)))
            {
                fieldNameToInferredType[fieldName] = "FIELD_TYPE_TEXT";
            }

            return (missingFields, fieldNameToInferredType);
        }

        // 批量创建缺失的字段。
        private async Task CreateMissingFieldsAsync(
                            string docId,
                            string sheetId,
                            List<string> missingFieldNames,
                            Dictionary<string, string> fieldNameToInferredType,
                            string accessToken)
        {
            var fieldsToAdd = missingFieldNames
                .Select(fieldName => CreateFieldWithDefaultProperties(fieldName, fieldNameToInferredType[fieldName]))
                .ToList();

            var addFieldsRequest = new CgibinWedocSmartSheetAddFieldsRequest
            {
                AccessToken = accessToken,
                DocumentId = docId,
                SheetId = sheetId,
                FieldList = fieldsToAdd
            };

            var addFieldsResponse = await _client.ExecuteCgibinWedocSmartSheetAddFieldsAsync(addFieldsRequest);
            if (!addFieldsResponse.IsSuccessful())
            {
                var fieldNames = string.Join(", ", missingFieldNames);
                throw new InvalidOperationException(
                    $"创建字段失败 (字段: {fieldNames})。错误码: [{addFieldsResponse.ErrorCode}] {addFieldsResponse.ErrorMessage}");
            }
        }

        //根据字段名和推断类型，创建一个带有默认属性设置的 Field 对象。        
        private CgibinWedocSmartSheetAddFieldsRequest.Types.Field CreateFieldWithDefaultProperties(
            string fieldName, string inferredType)
        {
            var field = new CgibinWedocSmartSheetAddFieldsRequest.Types.Field
            {
                Title = fieldName,
                Type = inferredType
            };

            // 根据推断类型，实例化对应的 PropertyAsXXX 属性（提供合理的默认值）
            switch (inferredType)
            {
                case "FIELD_TYPE_TEXT":
                    // 文本类型：可以设置一个空属性对象，也可以不设置（根据API要求）
                    field.PropertyAsText = new CgibinWedocSmartSheetAddFieldsRequest.Types.Field.Types.TextFieldProperty();
                    break;

                case "FIELD_TYPE_NUMBER":
                    field.PropertyAsNumber = new CgibinWedocSmartSheetAddFieldsRequest.Types.Field.Types.NumberFieldProperty
                    {
                        DecimalPlaces = 0,          // 默认小数位0
                        IsUseSeparate = false       // 默认不使用千位分隔符
                    };
                    break;

                case "FIELD_TYPE_CHECKBOX":
                    field.PropertyAsCheckbox = new CgibinWedocSmartSheetAddFieldsRequest.Types.Field.Types.CheckboxFieldProperty
                    {
                        IsChecked = false            // 默认未勾选
                    };
                    break;

                case "FIELD_TYPE_DATE_TIME":
                    field.PropertyAsDateTime = new CgibinWedocSmartSheetAddFieldsRequest.Types.Field.Types.DateTimeFieldProperty
                    {
                        FormatString = "yyyy-MM-dd", // 默认日期格式，可根据需要调整
                        IsAutoFill = false
                    };
                    break;

                // 如果未来推断类型扩展到其他简单类型（如百分比、货币等），可以在此添加对应默认属性
                // 注意：对于单选、多选、人员等复杂类型，由于无法自动生成选项列表，应当由 InferFieldType 回退为 "text"
                // 因此这里不需要处理 select/single_select/user 等类型

                default:
                    // 对于未明确处理的类型，默认作为文本处理（确保至少有一个属性对象）
                    field.PropertyAsText = new CgibinWedocSmartSheetAddFieldsRequest.Types.Field.Types.TextFieldProperty();
                    break;
            }

            return field;
        }


        // 构建记录列表，替换字段名为字段ID，并根据字段类型构造单元格值对象
        private List<CgibinWedocSmartSheetAddRecordsRequest.Types.Record> BuildRecordList(
           IEnumerable<IDictionary<string, object>> records,
           IReadOnlyDictionary<string, string> fieldNameToIdMap,
           IReadOnlyDictionary<string, string> fieldIdToTypeMap
           )
        {
            var recordList = new List<CgibinWedocSmartSheetAddRecordsRequest.Types.Record>();
            foreach (var record in records)
            {
                var values = new Dictionary<string, object>();
                //object[] cellValues = [];
                foreach (var kv in record)
                {
                    // 1. 通过字段名获取字段ID
                    if (!fieldNameToIdMap.TryGetValue(kv.Key, out var fieldId))
                        throw new KeyNotFoundException($"字段 '{kv.Key}' 不存在");
                    var fieldType = fieldIdToTypeMap[fieldId];
                    // 3. 根据字段类型构造单元格值对象
                    values[fieldId] = BuildCellValue(fieldType, kv.Value);
                }
                recordList.Add(new CgibinWedocSmartSheetAddRecordsRequest.Types.Record { Values = values });
            }
            return recordList;
        }

        // 根据字段类型和原始值构建单元格值对象
        private object BuildCellValue(string fieldType, object rawValue)
        {
            switch (fieldType)
            {
                case "FIELD_TYPE_TEXT":      // 文本
                    return new object[] { new { type = "text", text = rawValue?.ToString() ?? "" } };

                case "FIELD_TYPE_NUMBER":    // 数字
                    return Convert.ToDouble(rawValue);

                case "FIELD_TYPE_CHECKBOX":  // 复选框
                    return Convert.ToBoolean(rawValue);

                case "FIELD_TYPE_DATE_TIME": // 日期
                                             // 日期需要转换为毫秒级的Unix时间戳字符串
                    if (rawValue is DateTime dateTime)
                    {
                        var unixTimeMillis = new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
                        return unixTimeMillis.ToString();
                    }
                    return rawValue?.ToString() ?? "";

                case "FIELD_TYPE_IMAGE":     // 图片
                                             // CellImageValue 结构：id、title、image_url、width、height
                    if (rawValue == null)
                    {
                        return new object[] { };
                    }

                    if (rawValue is string imageUrl)
                    {
                        return new object[] { new { id = (string)null, title = "", image_url = imageUrl, width = 0, height = 0 } };
                    }

                    if (rawValue is Dictionary<string, object> imageDict)
                    {
                        var id = imageDict.ContainsKey("id") ? imageDict["id"]?.ToString() : null;
                        var title = imageDict.ContainsKey("title") ? imageDict["title"]?.ToString() : "";
                        var imageUrlValue = imageDict.ContainsKey("image_url") ? imageDict["image_url"]?.ToString() : "";
                        var width = imageDict.ContainsKey("width") ? Convert.ToInt32(imageDict["width"]) : 0;
                        var height = imageDict.ContainsKey("height") ? Convert.ToInt32(imageDict["height"]) : 0;

                        return new object[] { new { id = id, title = title, image_url = imageUrlValue, width = width, height = height } };
                    }

                    return new object[] { new { id = (string)null, title = "", image_url = rawValue.ToString(), width = 0, height = 0 } };

                case "FIELD_TYPE_ATTACHMENT": // 文件
                                              // CellAttachmentValue 结构：name、size、file_ext、file_id、file_url、file_type、doc_type
                    if (rawValue == null)
                    {
                        return new object[] { };
                    }

                    if (rawValue is string fileUrl)
                    {
                        return new object[] { new { name = "", size = 0, file_ext = "", file_id = "", file_url = fileUrl, file_type = "", doc_type = 2 } };
                    }

                    if (rawValue is Dictionary<string, object> attachmentDict)
                    {
                        var name = attachmentDict.ContainsKey("name") ? attachmentDict["name"]?.ToString() : "";
                        var size = attachmentDict.ContainsKey("size") ? Convert.ToInt32(attachmentDict["size"]) : 0;
                        var fileExt = attachmentDict.ContainsKey("file_ext") ? attachmentDict["file_ext"]?.ToString() : "";
                        var fileId = attachmentDict.ContainsKey("file_id") ? attachmentDict["file_id"]?.ToString() : "";
                        var fileUrlValue = attachmentDict.ContainsKey("file_url") ? attachmentDict["file_url"]?.ToString() : "";
                        var fileType = attachmentDict.ContainsKey("file_type") ? attachmentDict["file_type"]?.ToString() : "";
                        var docType = attachmentDict.ContainsKey("doc_type") ? Convert.ToInt32(attachmentDict["doc_type"]) : 2;

                        return new object[] { new { name = name, size = size, file_ext = fileExt, file_id = fileId, file_url = fileUrlValue, file_type = fileType, doc_type = docType } };
                    }

                    return new object[] { new { name = "", size = 0, file_ext = "", file_id = "", file_url = rawValue.ToString(), file_type = "", doc_type = 2 } };

                case "FIELD_TYPE_USER":      // 成员
                                             // CellUserValue 结构：user_id
                    if (rawValue == null)
                    {
                        return new object[] { };
                    }

                    if (rawValue is string userId)
                    {
                        return new object[] { new { user_id = userId } };
                    }

                    if (rawValue is Dictionary<string, object> userDict)
                    {
                        var userIdValue = userDict.ContainsKey("user_id") ? userDict["user_id"]?.ToString() : "";
                        return new object[] { new { user_id = userIdValue } };
                    }

                    return new object[] { new { user_id = rawValue.ToString() } };

                case "FIELD_TYPE_URL":       // 链接
                                             // CellUrlValue 结构：type、text、link
                    if (rawValue == null)
                    {
                        return new object[] { };
                    }

                    if (rawValue is string link)
                    {
                        return new object[] { new { type = "url", text = link, link = link } };
                    }

                    if (rawValue is Dictionary<string, object> urlDict)
                    {
                        var type = urlDict.ContainsKey("type") ? urlDict["type"]?.ToString() : "url";
                        var text = urlDict.ContainsKey("text") ? urlDict["text"]?.ToString() : "";
                        var linkValue = urlDict.ContainsKey("link") ? urlDict["link"]?.ToString() : "";

                        return new object[] { new { type = type, text = text, link = linkValue } };
                    }

                    return new object[] { new { type = "url", text = rawValue.ToString(), link = rawValue.ToString() } };

                case "FIELD_TYPE_SELECT":    // 多选
                case "FIELD_TYPE_SINGLE_SELECT": // 单选
                                                 // Option 结构：id、style、text
                    if (rawValue == null)
                    {
                        return new object[] { };
                    }

                    if (rawValue is string selectText)
                    {
                        return new object[] { new { id = (string)null, style = 0, text = selectText } };
                    }

                    if (rawValue is Dictionary<string, object> selectDict)
                    {
                        var id = selectDict.ContainsKey("id") ? selectDict["id"]?.ToString() : null;
                        var style = selectDict.ContainsKey("style") ? Convert.ToInt32(selectDict["style"]) : 0;
                        var text = selectDict.ContainsKey("text") ? selectDict["text"]?.ToString() : "";
                        return new object[] { new { id = id, style = style, text = text } };
                    }

                    return new object[] { new { id = (string)null, style = 0, text = rawValue.ToString() } };

                case "FIELD_TYPE_PROGRESS":  // 进度
                    return Convert.ToDouble(rawValue);

                case "FIELD_TYPE_PHONE_NUMBER": // 电话
                    return rawValue?.ToString() ?? "";

                case "FIELD_TYPE_EMAIL":    // 邮箱
                    return rawValue?.ToString() ?? "";

                case "FIELD_TYPE_LOCATION":  // 地理位置
                                             // CellLocationValue 结构：source_type、id、latitude、longitude、title
                    if (rawValue == null)
                    {
                        return new object[] { };
                    }

                    if (rawValue is string locationTitle)
                    {
                        // 简单方式：只提供地点名称
                        return new object[] {
                            new {
                                source_type = 1,
                                id = "",
                                latitude = "",
                                longitude = "",
                                title = locationTitle
                            }
                        };
                    }

                    if (rawValue is Dictionary<string, object> locationDict)
                    {
                        var sourceType = locationDict.ContainsKey("source_type") ? Convert.ToUInt32(locationDict["source_type"]) : 1;
                        var id = locationDict.ContainsKey("id") ? locationDict["id"]?.ToString() : "";
                        var latitude = locationDict.ContainsKey("latitude") ? locationDict["latitude"]?.ToString() : "";
                        var longitude = locationDict.ContainsKey("longitude") ? locationDict["longitude"]?.ToString() : "";
                        var title = locationDict.ContainsKey("title") ? locationDict["title"]?.ToString() : "";

                        return new object[] {
                            new {
                                source_type = sourceType,
                                id = id,
                                latitude = latitude,
                                longitude = longitude,
                                title = title
                            }
                        };
                    }

                    // 如果是元组 (latitude, longitude, title)
                    if (rawValue is ValueTuple<string, string, string> locationTuple)
                    {
                        return new object[] {
                            new {
                                source_type = 1,
                                id = "",
                                latitude = locationTuple.Item1,
                                longitude = locationTuple.Item2,
                                title = locationTuple.Item3
                            }
                        };
                    }

                    return new object[] {
                        new {
                            source_type = 1,
                            id = "",
                            latitude = "",
                            longitude = "",
                            title = rawValue.ToString()
                        }
                    };

                case "FIELD_TYPE_CURRENCY": // 货币
                    return Convert.ToDouble(rawValue);

                case "FIELD_TYPE_PERCENTAGE": // 百分数
                    return Convert.ToDouble(rawValue);

                case "FIELD_TYPE_BARCODE":  // 条码
                    return rawValue?.ToString() ?? "";

                default:
                    throw new NotSupportedException($"不支持的字段类型: {fieldType}");
            }
        }


        // 根据值的类型推断字段类型（支持常见类型）     
        private string InferFieldType(object value)
        {
            if (value == null)
                return "text"; // 空值无法推断，默认文本

            // 根据实际类型和内容推断
            switch (value)
            {
                case int _:
                case long _:
                case float _:
                case double _:
                case decimal _:
                    // 数字类型：对应 API 的 "number"
                    return "FIELD_TYPE_NUMBER";

                case bool _:
                    // 布尔类型：对应 API 的 "checkbox"
                    return "FIELD_TYPE_CHECKBOX";

                case DateTime _:
                    // 日期时间：对应 API 的 "date_time"
                    return "FIELD_TYPE_DATE_TIME";

                case string str:
                    // 字符串类型：尝试进一步识别格式
                    if (DateTime.TryParse(str, out _))
                        return "FIELD_TYPE_DATE_TIME"; // 能解析为日期时间的字符串
                    if (bool.TryParse(str, out _))
                        return "FIELD_TYPE_CHECKBOX";   // 能解析为布尔值的字符串（如 "true"/"false"）
                    if (decimal.TryParse(str, out _))
                        return "FIELD_TYPE_NUMBER";      // 纯数字字符串
                                                         // 默认作为普通文本
                    return "FIELD_TYPE_TEXT";

                default:
                    // 其他未知类型（如数组、对象等）保守地作为文本处理
                    return "FIELD_TYPE_TEXT";
            }
        }

        #endregion

        #region Token 失效自动重试

        private async Task<TResponse> ExecuteWithTokenRefreshAsync<TResponse>(
            Func<string, Task<TResponse>> action,
            CancellationToken ct)
            where TResponse : WechatWorkResponse
        {
            var token = await _tokenProvider.GetAccessTokenAsync(ct);
            var response = await action(token);

            if (response.ErrorCode == 42001 || response.ErrorCode == 40014)
            {
                _logger.LogWarning("AccessToken 提前失效（错误码 {ErrorCode}），正在强制刷新...", response.ErrorCode);
                await _tokenProvider.ForceRefreshAccessTokenAsync(ct);
                token = await _tokenProvider.GetAccessTokenAsync(ct);
                response = await action(token);
            }

            return response;
        }

        #endregion

        #region 群聊相关接口

        /// <summary>
        /// 创建企业微信群聊并发送消息（一步完成）
        /// </summary>
        /// <param name="userIds">群成员 userid 列表，2-2000人（必须包含群主 userid）</param>
        /// <param name="chatName">群聊名称，最多50个UTF-8字符</param>
        /// <param name="ownerUserId">群主 userid（必须在 userList 中）</param>
        /// <param name="content">消息内容</param>
        /// <param name="msgType">消息类型，默认文本</param>
        /// <param name="chatId">可选，自定义群聊ID（不传则系统自动生成），最长为32字符</param>
        /// <param name="title">卡片标题，仅在 msgType 为 Card 时必填</param>
        /// <param name="description">卡片描述，仅在 msgType 为 Card 时必填</param>
        /// <param name="url">卡片跳转链接，仅在 msgType 为 Card 时必填</param>
        /// <param name="buttonText">卡片按钮文字，仅在 msgType 为 Card 时有效，默认"查看详情"</param>
        /// <returns>包含创建结果和发送结果的元组</returns>
        public async Task<(CgibinAppChatCreateResponse CreateResult, CgibinAppChatSendResponse? SendResult)>
            CreateChatAndSendMessageAsync(
                List<string> userIds,
                string chatName,
                string ownerUserId,
                string content,
                WechatWorkMessageType msgType = WechatWorkMessageType.Text,
                string? chatId = null,
                string? title = null,
                string? description = null,
                string? url = null,
                string? buttonText = null,
                CancellationToken ct = default)
        {
            // 1. 创建群聊
            var createResponse = await CreateGroupChatAsync(userIds, chatName, ownerUserId, chatId, ct);
            if (!createResponse.IsSuccessful())
            {
                _logger.LogError("创建群聊失败: [{ErrorCode}] {ErrorMessage}",
                    createResponse.ErrorCode, createResponse.ErrorMessage);
                throw new InvalidOperationException(
                    $"创建群聊失败: [{createResponse.ErrorCode}] {createResponse.ErrorMessage}");
            }

            _logger.LogInformation("群聊创建成功，ChatId: {ChatId}", createResponse.ChatId);

            // 2. 向新建的群聊发送消息
            var sendResponse = await SendMessageToGroupChatAsync(
                createResponse.ChatId, content, msgType, title, description, url, buttonText, ct: ct);

            if (!sendResponse.IsSuccessful())
            {
                _logger.LogError("向群聊 [{ChatId}] 发送消息失败: [{ErrorCode}] {ErrorMessage}",
                    createResponse.ChatId, sendResponse.ErrorCode, sendResponse.ErrorMessage);
            }
            else
            {
                _logger.LogInformation("向群聊 [{ChatId}] 发送消息成功", createResponse.ChatId);
            }

            return (createResponse, sendResponse);
        }

        /// <summary>
        /// 创建企业微信应用群聊
        /// </summary>
        /// <param name="userIds">群成员 userid 列表，2-2000人（必须包含群主）</param>
        /// <param name="chatName">群聊名称，最多50个UTF-8字符</param>
        /// <param name="ownerUserId">群主 userid（必须在 userList 中）</param>
        /// <param name="chatId">可选，自定义群聊ID（不传则系统自动生成），最长为32字符</param>
        public async Task<CgibinAppChatCreateResponse> CreateGroupChatAsync(
            List<string> userIds,
            string chatName,
            string ownerUserId,
            string? chatId = null,
            CancellationToken ct = default)
        {
            if (userIds == null || userIds.Count < 2)
                throw new ArgumentException("群聊至少需要2个成员", nameof(userIds));
            if (string.IsNullOrWhiteSpace(chatName))
                throw new ArgumentException("群聊名称不能为空", nameof(chatName));
            if (string.IsNullOrWhiteSpace(ownerUserId))
                throw new ArgumentException("群主不能为空", nameof(ownerUserId));

            var distinctUsers = userIds.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();

            if (!distinctUsers.Contains(ownerUserId))
            {
                _logger.LogWarning("群主 {Owner} 不在成员列表中，已自动添加", ownerUserId);
                distinctUsers.Add(ownerUserId);
                distinctUsers = distinctUsers.Distinct().ToList();
            }

            var response = await ExecuteWithTokenRefreshAsync(
                async token =>
                {
                    var request = new CgibinAppChatCreateRequest
                    {
                        AccessToken = token,
                        Name = chatName,
                        OwnerUserId = ownerUserId,
                        MemberUserIdList = distinctUsers,
                        ChatId = chatId
                    };
                    return await _client.ExecuteCgibinAppChatCreateAsync(request);
                }, ct);

            if (!response.IsSuccessful())
            {
                _logger.LogError("创建群聊失败: [{ErrorCode}] {ErrorMessage}",
                    response.ErrorCode, response.ErrorMessage);
                return response;
            }

            // 创建成功后，将群聊信息持久化到数据库
            await SaveGroupChatToDbAsync(response.ChatId, chatName, ownerUserId, distinctUsers);

            return response;
        }

        /// <summary>
        /// 从数据库获取所有群聊记录
        /// </summary>
        public async Task<List<WechatWorkGroupChat>> GetAllGroupChatsAsync(CancellationToken ct = default)
        {
            return await _context.企业微信群聊
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(ct);
        }

        /// <summary>
        /// 将群聊记录保存到数据库
        /// </summary>
        private async Task SaveGroupChatToDbAsync(
            string chatId,
            string chatName,
            string ownerUserId,
            List<string> memberUserIds)
        {
            try
            {
                // 检查是否已存在（防重复）
                var exists = await _context.企业微信群聊.AnyAsync(c => c.ChatId == chatId);
                if (exists)
                {
                    _logger.LogInformation("群聊 [{ChatId}] 已在数据库中存在，跳过保存", chatId);
                    return;
                }

                _context.企业微信群聊.Add(new WechatWorkGroupChat
                {
                    编号=Guid.NewGuid().ToString("N"),
                    ChatId = chatId,
                    Name = chatName,
                    OwnerUserId = ownerUserId,
                    MemberUserIds = string.Join(",", memberUserIds),
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });

                await _context.SaveChangesAsync();
                _logger.LogInformation("群聊 [{ChatId}] 已保存到数据库", chatId);
            }
            catch (Exception ex)
            {
                // 数据库保存失败不影响群聊创建结果
                _logger.LogWarning(ex, "保存群聊 [{ChatId}] 到数据库失败，但群聊已创建成功", chatId);
            }
        }

        /// <summary>
        /// 向已有企业微信群聊发送消息
        /// </summary>
        /// <param name="chatId">群聊ID</param>
        /// <param name="content">消息内容（文本/Markdown 内容）</param>
        /// <param name="msgType">消息类型（支持 Text / Markdown / Card）</param>
        /// <param name="title">卡片标题，仅在 msgType 为 Card 时必填</param>
        /// <param name="description">卡片描述，仅在 msgType 为 Card 时必填</param>
        /// <param name="url">卡片跳转链接，仅在 msgType 为 Card 时必填</param>
        /// <param name="buttonText">卡片按钮文字，仅在 msgType 为 Card 时有效，默认"查看详情"</param>
        public async Task<CgibinAppChatSendResponse> SendMessageToGroupChatAsync(
            string chatId,
            string content,
            WechatWorkMessageType msgType,
            string? title = null,
            string? description = null,
            string? url = null,
            string? buttonText = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(chatId))
                throw new ArgumentException("群聊ID不能为空", nameof(chatId));
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("消息内容不能为空", nameof(content));

            var response = await ExecuteWithTokenRefreshAsync(
                async token =>
                {
                    var request = new CgibinAppChatSendRequest
                    {
                        AccessToken = token,
                        ChatId = chatId
                    };

                    switch (msgType)
                    {
                        case WechatWorkMessageType.Text:
                            request.MessageType = "text";
                            request.MessageContentAsText = new CgibinAppChatSendRequest.Types.TextMessage
                            {
                                Content = content
                            };
                            break;

                        case WechatWorkMessageType.Markdown:
                            request.MessageType = "markdown";
                            request.MessageContentAsMarkdown = new CgibinAppChatSendRequest.Types.MarkdownMessage
                            {
                                Content = content
                            };
                            break;

                        case WechatWorkMessageType.Card:
                            if (string.IsNullOrWhiteSpace(title))
                                throw new ArgumentException("发送卡片消息时 title 不能为空", nameof(title));
                            if (string.IsNullOrWhiteSpace(description))
                                throw new ArgumentException("发送卡片消息时 description 不能为空", nameof(description));
                            if (string.IsNullOrWhiteSpace(url))
                                throw new ArgumentException("发送卡片消息时 url 不能为空", nameof(url));
                            request.MessageType = "textcard";
                            request.MessageContentAsTextCard = new CgibinAppChatSendRequest.Types.TextCardMessage
                            {
                                Title = title,
                                Description = description,
                                Url = url,
                                ButtonText = buttonText ?? "查看详情"
                            };
                            break;

                        default:
                            throw new NotSupportedException($"群聊消息不支持的类型: {msgType}");
                    }

                    return await _client.ExecuteCgibinAppChatSendAsync(request);
                }, ct);

            if (!response.IsSuccessful())
            {
                _logger.LogError("向群聊 [{ChatId}] 发送消息失败: [{ErrorCode}] {ErrorMessage}",
                    chatId, response.ErrorCode, response.ErrorMessage);
            }

            return response;
        }

        /// <summary>
        /// 向已有企业微信群聊发送文本卡片消息
        /// </summary>
        /// <param name="chatId">群聊ID</param>
        /// <param name="title">卡片标题</param>
        /// <param name="description">卡片描述</param>
        /// <param name="url">点击后跳转的链接</param>
        /// <param name="buttonText">按钮文字，默认"查看详情"</param>
        public async Task<CgibinAppChatSendResponse> SendMessageToGroupChatAsCardAsync(
            string chatId,
            string title,
            string description,
            string url,
            string buttonText = "查看详情",
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(chatId))
                throw new ArgumentException("群聊ID不能为空", nameof(chatId));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("卡片标题不能为空", nameof(title));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("卡片描述不能为空", nameof(description));
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("跳转链接不能为空", nameof(url));

            var response = await ExecuteWithTokenRefreshAsync(
                async token =>
                {
                    var request = new CgibinAppChatSendRequest
                    {
                        AccessToken = token,
                        ChatId = chatId,
                        MessageType = "textcard",
                        MessageContentAsTextCard = new CgibinAppChatSendRequest.Types.TextCardMessage
                        {
                            Title = title,
                            Description = description,
                            Url = url,
                            ButtonText = buttonText
                        }
                    };

                    return await _client.ExecuteCgibinAppChatSendAsync(request);
                }, ct);

            if (!response.IsSuccessful())
            {
                _logger.LogError("向群聊 [{ChatId}] 发送卡片消息失败: [{ErrorCode}] {ErrorMessage}",
                    chatId, response.ErrorCode, response.ErrorMessage);
            }

            return response;
        }

        #endregion

        /// <summary>获取企业微信 JS-SDK 的 jsapi_ticket</summary>
        public async Task<string> GetJsApiTicketAsync(CancellationToken ct = default)
        {
            return await _tokenProvider.GetJsApiTicketAsync(ct);
        }

        /// <summary>生成前端 wx.config 所需的签名配置</summary>
        public async Task<JsSdkConfig> GetJsSdkConfigAsync(string url, CancellationToken ct = default)
        {
            var ticket = await _tokenProvider.GetJsApiTicketAsync(ct);
            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            var nonceStr = Guid.NewGuid().ToString("N")[..16];

            var raw = $"jsapi_ticket={ticket}&noncestr={nonceStr}&timestamp={timestamp}&url={url}";
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(raw));
            var signature = BitConverter.ToString(hash).Replace("-", "").ToLower();

            return new JsSdkConfig
            {
                AppId = _config["WechatWork:CorpId"]!,
                Timestamp = timestamp,
                NonceStr = nonceStr,
                Signature = signature
            };
        }

        public class JsSdkConfig
        {
            public string AppId { get; set; } = null!;
            public long Timestamp { get; set; }
            public string NonceStr { get; set; } = null!;
            public string Signature { get; set; } = null!;
        }

    }
}