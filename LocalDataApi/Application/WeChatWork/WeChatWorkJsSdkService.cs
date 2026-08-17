using LocalDataApi.Infrastructure.WeChatWork;
using SKIT.FlurlHttpClient.Wechat.Work;
using System.Security.Cryptography;
using System.Text;

namespace LocalDataApi.Application.WeChatWork;

/// <summary>
/// 企业微信 JS-SDK 用例:生成前端 wx.config 所需的签名配置。
/// </summary>
public class WeChatWorkJsSdkService : WechatWorkServiceBase
{
    private readonly IConfiguration _config;

    public WeChatWorkJsSdkService(
        WechatWorkClient client,
        WechatWorkTokenProvider tokenProvider,
        IConfiguration configuration,
        ILogger<WeChatWorkJsSdkService> logger)
        : base(client, tokenProvider, logger)
    {
        _config = configuration;
    }

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
        using var sha1 = SHA1.Create();
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
