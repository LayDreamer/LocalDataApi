using LocalDataApi.Domain.WeChatWork;
using LocalDataApi.Infrastructure.WeChatWork;
using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Models;

namespace LocalDataApi.Application.WeChatWork;

/// <summary>
/// 企业微信组织架构用例:部门、成员、上下游企业。
/// </summary>
public class WeChatWorkOrganizationService : WechatWorkServiceBase
{
    private readonly int _agentId;

    public WeChatWorkOrganizationService(
        WechatWorkClient client,
        WechatWorkTokenProvider tokenProvider,
        IConfiguration configuration,
        ILogger<WeChatWorkOrganizationService> logger)
        : base(client, tokenProvider, logger)
    {
        _agentId = configuration.GetValue<int>("WechatWork:AgentId");
    }

    /// <summary>获取部门列表</summary>
    public async Task<CgibinDepartmentListResponse> GetDepartmentsAsync(CancellationToken ct = default)
    {
        var response = await ExecuteWithTokenRefreshAsync(
            async token =>
            {
                var request = new CgibinDepartmentListRequest() { AccessToken = token };
                return await _client.ExecuteCgibinDepartmentListAsync(request, ct);
            }, ct);

        if (!response.IsSuccessful())
        {
            _logger.LogError(
                "获取部门列表失败: [{ErrorCode}] {ErrorMessage}。{ErrorHint}",
                response.ErrorCode, response.ErrorMessage, GetWechatWorkErrorHint(response.ErrorCode));
        }

        return response;
    }

    /// <summary>将平面部门列表转换为嵌套树形结构</summary>
    public List<DepartmentTreeNode> BuildDepartmentTree(List<CgibinDepartmentListResponse.Types.Department> departments)
    {
        if (departments == null || departments.Count == 0)
        {
            return new List<DepartmentTreeNode>();
        }

        var departmentDict = departments.ToDictionary(d => d.DepartmentId, d => new DepartmentTreeNode
        {
            Id = d.DepartmentId,
            Name = d.Name,
            ParentId = d.ParentDepartmentId,
            Order = d.DepartmentOrder,
            DepartmentLeader = d.LeaderUserIdList?.ToList(),
            Children = new List<DepartmentTreeNode>()
        });

        var rootNodes = new List<DepartmentTreeNode>();

        foreach (var department in departmentDict.Values)
        {
            if (department.ParentId == 0)
            {
                rootNodes.Add(department);
            }
            else if (departmentDict.ContainsKey(department.ParentId))
            {
                departmentDict[department.ParentId].Children.Add(department);
            }
        }

        SortChildren(rootNodes);

        return rootNodes;
    }

    private void SortChildren(List<DepartmentTreeNode> nodes)
    {
        if (nodes == null || nodes.Count == 0)
            return;

        nodes.Sort((a, b) => a.Order.CompareTo(b.Order));

        foreach (var node in nodes)
        {
            SortChildren(node.Children);
        }
    }

    /// <summary>获取扁平化的部门路径(从根节点到当前节点)</summary>
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

    /// <summary>获取所有子部门ID(包括递归子部门)</summary>
    public List<long> GetAllChildDepartmentIds(int parentId, List<CgibinDepartmentListResponse.Types.Department> departments)
    {
        var result = new List<long>();
        var departmentDict = departments.ToDictionary(d => d.DepartmentId);

        CollectChildDepartmentIds(parentId, departmentDict, result);
        return result;
    }

    private void CollectChildDepartmentIds(long parentId, Dictionary<long, CgibinDepartmentListResponse.Types.Department> departmentDict, List<long> result)
    {
        var children = departmentDict.Values.Where(d => d.ParentDepartmentId == parentId);

        foreach (var child in children)
        {
            result.Add(child.DepartmentId);
            CollectChildDepartmentIds(child.DepartmentId, departmentDict, result);
        }
    }

    /// <summary>获取指定部门下的成员列表(包含详情)</summary>
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
                return await _client.ExecuteCgibinUserListAsync(request, ct);
            }, ct);

        if (!response.IsSuccessful())
        {
            _logger.LogError(
                "获取部门成员失败: [{ErrorCode}] {ErrorMessage}。部门ID: {DepartmentId}, 是否递归子部门: {FetchChild}。{ErrorHint}",
                response.ErrorCode, response.ErrorMessage, departmentId, fetchChild, GetWechatWorkErrorHint(response.ErrorCode));
        }

        return response;
    }

    #region 上下游企业

    /// <summary>获取上下游根目录</summary>
    public async Task<CgibinCorpGroupCorpGetChainListResponse> GetChainsAsync(CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinCorpGroupCorpGetChainListRequest { AccessToken = accessToken };
        return await _client.ExecuteCgibinCorpGroupCorpGetChainListAsync(request, ct);
    }

    /// <summary>获取上下游分组列表</summary>
    public async Task<CgibinCorpGroupCorpGetChainGroupResponse> GetChainGroupAsync(string chainId, CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinCorpGroupCorpGetChainGroupRequest
        {
            AccessToken = accessToken,
            ChainId = chainId,
        };
        return await _client.ExecuteCgibinCorpGroupCorpGetChainGroupAsync(request, ct);
    }

    /// <summary>获取互联企业列表(查询指定互联企业/上下游的企业列表)</summary>
    public async Task<CgibinCorpGroupCorpGetChainCorpInfoListResponse> GetChainGroupInfoListAsync(string chainId, int? groupId, CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinCorpGroupCorpGetChainCorpInfoListRequest
        {
            AccessToken = accessToken,
            ChainId = chainId,
            GroupId = groupId
        };
        return await _client.ExecuteCgibinCorpGroupCorpGetChainCorpInfoListAsync(request, ct);
    }

    /// <summary>获取互联企业成员列表</summary>
    public async Task<CgibinLinkedCorpUserListResponse> GetLinkedCorpUserListAsync(string chainId, int? groupId, CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinLinkedCorpUserListRequest
        {
            AccessToken = accessToken,
            LinkedDepartmentId = $"{chainId}/{groupId}", // 互联企业部门ID格式为 "企业ID/部门ID"
            RequireFetchChild = true
        };
        return await _client.ExecuteCgibinLinkedCorpUserListAsync(request, ct);
    }

    /// <summary>获取互联企业部门列表</summary>
    public async Task<CgibinLinkedCorpDepartmentListResponse> GetLinkedCorpDepartmentsAsync(string corpId, string? deptId = null, CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);

        string departmentId = string.IsNullOrEmpty(deptId) ? $"{corpId}/1" : $"{corpId}/{deptId}";

        var request = new CgibinLinkedCorpDepartmentListRequest
        {
            AccessToken = accessToken,
            LinkedDepartmentId = departmentId
        };

        return await _client.ExecuteCgibinLinkedCorpDepartmentListAsync(request, ct);
    }

    /// <summary>获取互联企业部门成员</summary>
    public async Task<CgibinLinkedCorpUserListResponse> GetLinkedCorpUsersAsync(string linkedDepartmentId, bool requireFetchChild = true, CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinLinkedCorpUserListRequest
        {
            AccessToken = accessToken,
            LinkedDepartmentId = linkedDepartmentId,
            RequireFetchChild = requireFetchChild
        };

        return await _client.ExecuteCgibinLinkedCorpUserListAsync(request, ct);
    }

    /// <summary>获取互联企业成员详细信息</summary>
    public async Task<CgibinLinkedCorpUserGetResponse> GetLinkedCorpUserDetailAsync(string userId, CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinLinkedCorpUserGetRequest
        {
            AccessToken = accessToken,
            CorpUserId = userId // 格式为"企业ID/成员ID"
        };
        return await _client.ExecuteCgibinLinkedCorpUserGetAsync(request, ct);
    }

    /// <summary>获取下游企业的 AccessToken(用于代表下游企业调用接口)</summary>
    public async Task<CgibinCorpGroupCorpGetTokenResponse> GetCorpGroupTokenAsync(string corpId, CancellationToken ct = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var request = new CgibinCorpGroupCorpGetTokenRequest
        {
            AccessToken = accessToken,
            CorpId = corpId,
            AgentId = _agentId
        };
        return await _client.ExecuteCgibinCorpGroupCorpGetTokenAsync(request, ct);
    }

    /// <summary>给下游企业的某个成员发送文本消息</summary>
    public async Task<CgibinLinkedCorpMessageSendResponse> SendTextMessageToLinkedCorpUserAsync(string corpId, string userId, string content, CancellationToken ct = default)
    {
        var tokenResponse = await GetCorpGroupTokenAsync(corpId, ct);
        if (!tokenResponse.IsSuccessful())
        {
            throw new InvalidOperationException($"获取下游企业Token失败: [{tokenResponse.ErrorCode}] {tokenResponse.ErrorMessage}");
        }
        string downstreamAccessToken = tokenResponse.AccessToken;

        var request = new CgibinLinkedCorpMessageSendRequest
        {
            AccessToken = downstreamAccessToken,
            ToCorpUserIdList = [userId],
            AgentId = _agentId,
            MessageType = "text",
            MessageContentAsText = new CgibinLinkedCorpMessageSendRequest.Types.TextMessage()
            {
                Content = content
            },
            IsSafe = false
        };

        return await _client.ExecuteCgibinLinkedCorpMessageSendAsync(request, ct);
    }

    #endregion
}
