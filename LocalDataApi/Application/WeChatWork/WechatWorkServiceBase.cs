using LocalDataApi.Infrastructure.WeChatWork;
using SKIT.FlurlHttpClient.Wechat.Work;

namespace LocalDataApi.Application.WeChatWork;

/// <summary>
/// 企业微信应用服务基类:提供统一的 AccessToken 注入、失效自动重试与错误提示。
/// </summary>
public abstract class WechatWorkServiceBase
{
    protected readonly WechatWorkClient _client;
    protected readonly WechatWorkTokenProvider _tokenProvider;
    protected readonly ILogger _logger;

    protected WechatWorkServiceBase(
        WechatWorkClient client,
        WechatWorkTokenProvider tokenProvider,
        ILogger logger)
    {
        _client = client;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    /// <summary>
    /// 执行企业微信请求,AccessToken 失效(42001/40014)时自动强制刷新并重试一次。
    /// </summary>
    protected async Task<TResponse> ExecuteWithTokenRefreshAsync<TResponse>(
        Func<string, Task<TResponse>> action,
        CancellationToken ct)
        where TResponse : WechatWorkResponse
    {
        var token = await _tokenProvider.GetAccessTokenAsync(ct);
        var response = await action(token);

        if (response.ErrorCode == 42001 || response.ErrorCode == 40014)
        {
            _logger.LogWarning("AccessToken 提前失效(错误码 {ErrorCode}),正在强制刷新...", response.ErrorCode);
            await _tokenProvider.ForceRefreshAccessTokenAsync(ct);
            token = await _tokenProvider.GetAccessTokenAsync(ct);
            response = await action(token);
        }

        return response;
    }

    /// <summary>
    /// 企业微信常见错误码的排查提示。
    /// </summary>
    protected static string GetWechatWorkErrorHint(int errorCode)
    {
        return errorCode switch
        {
            60020 => "提示: 当前服务器出口公网 IP 未配置到企业微信应用的可信 IP 白名单,请将错误信息中的 from ip 加入可信 IP,或固定后端出口 IP。",
            60011 => "提示: 当前应用没有访问该成员、部门或标签的权限,请检查企业微信应用的可见范围、通讯录权限和所选部门 ID。",
            40014 => "提示: AccessToken 不合法,系统已自动刷新并重试一次;若仍失败,请检查 CorpId、AgentId、AgentSecret 是否匹配当前企业微信应用。",
            42001 => "提示: AccessToken 已过期,系统已自动刷新并重试一次;若仍失败,请检查服务器时间和 token 缓存。",
            40013 => "提示: CorpId 可能无效,请检查 WechatWork:CorpId 配置。",
            40001 => "提示: AgentSecret 可能错误,或 Secret 与当前 AgentId/CorpId 不匹配。",
            60003 => "提示: 部门 ID 不存在,请检查前端传入的部门 ID 是否来自当前企业的部门列表。",
            _ => "提示: 请根据企业微信返回的 errcode 排查应用权限、IP 白名单、参数和 token 状态。"
        };
    }
}
