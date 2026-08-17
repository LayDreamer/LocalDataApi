using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Models;

namespace LocalDataApi.Infrastructure.WeChatWork;

/// <summary>
/// 企业微信 AccessToken / JsApiTicket 缓存提供者(线程安全,单例注册)。
/// 走 IHttpClientFactory 管理 HTTP 连接,避免裸 HttpClient 造成 socket 耗尽。
/// </summary>
public class WechatWorkTokenProvider
{
    private readonly WechatWorkClient _client;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WechatWorkTokenProvider> _logger;

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly SemaphoreSlim _ticketLock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset? _tokenExpireTime;

    private string? _jsApiTicket;
    private DateTimeOffset? _ticketExpireTime;

    public WechatWorkTokenProvider(
        WechatWorkClient client,
        IHttpClientFactory httpClientFactory,
        ILogger<WechatWorkTokenProvider> logger)
    {
        _client = client;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (_accessToken != null && _tokenExpireTime.HasValue
            && DateTimeOffset.UtcNow < _tokenExpireTime.Value)
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_accessToken != null && _tokenExpireTime.HasValue
                && DateTimeOffset.UtcNow < _tokenExpireTime.Value)
            {
                return _accessToken;
            }

            var request = new CgibinGetTokenRequest();
            var response = await _client.ExecuteCgibinGetTokenAsync(request, ct);
            if (response.IsSuccessful())
            {
                _accessToken = response.AccessToken;
                _tokenExpireTime = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn).AddMinutes(-5);
                _logger.LogInformation(
                    "AccessToken 刷新成功,有效期至 {ExpireTime:yyyy-MM-dd HH:mm:ss}",
                    _tokenExpireTime);
                return _accessToken;
            }
            throw new InvalidOperationException(
                $"获取 AccessToken 失败: [{response.ErrorCode}] {response.ErrorMessage}");
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public async Task ForceRefreshAccessTokenAsync(CancellationToken ct = default)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            _accessToken = null;
            _tokenExpireTime = null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public async Task<string> GetJsApiTicketAsync(CancellationToken ct = default)
    {
        if (_jsApiTicket != null && _ticketExpireTime.HasValue
            && DateTimeOffset.UtcNow < _ticketExpireTime.Value)
        {
            return _jsApiTicket;
        }

        await _ticketLock.WaitAsync(ct);
        try
        {
            if (_jsApiTicket != null && _ticketExpireTime.HasValue
                && DateTimeOffset.UtcNow < _ticketExpireTime.Value)
            {
                return _jsApiTicket;
            }

            var token = await GetAccessTokenAsync(ct);
            using var httpClient = _httpClientFactory.CreateClient("WechatWork");
            var response = await httpClient.GetAsync(
                $"https://qyapi.weixin.qq.com/cgi-bin/get_jsapi_ticket?access_token={token}", ct);
            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("ticket", out var ticketElement))
            {
                _jsApiTicket = ticketElement.GetString();
                var expiresIn = root.GetProperty("expires_in").GetInt32();
                _ticketExpireTime = DateTimeOffset.UtcNow.AddSeconds(expiresIn).AddMinutes(-5);
                return _jsApiTicket!;
            }

            var errMsg = root.TryGetProperty("errmsg", out var em) ? em.GetString() : "未知错误";
            throw new InvalidOperationException($"获取 JsApiTicket 失败: {errMsg}");
        }
        finally
        {
            _ticketLock.Release();
        }
    }
}
