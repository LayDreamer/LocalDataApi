using Microsoft.AspNetCore.Mvc;
using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Models;
using System.Web;

[Route("wechat")]
public class WeChatController : Controller
{
    private readonly WechatWorkClient _client;
    private readonly WechatWorkOptions _options;

    public WeChatController(WechatWorkClient client, WechatWorkOptions options)
    {
        _client = client;
        _options = options;
    }

    /// <summary>
    /// 步骤1：生成授权链接，引导用户跳转
    /// </summary>
    [HttpGet("login")]
    public IActionResult Login()
    {
        // 构造回调地址（需UrlEncode）
        string redirectUri = HttpUtility.UrlEncode(_options.RedirectUri);

        // 生成OAuth2链接（场景A）
        string oauthUrl = $"https://open.weixin.qq.com/connect/oauth2/authorize" +
            $"?appid={_options.CorpId}" +
            $"&redirect_uri={redirectUri}" +
            $"&response_type=code" +
            $"&scope=snsapi_base" +   // 静默授权，直接获取UserId；如需详细信息可改为snsapi_privateinfo
            $"&agentid={_options.AgentId}" +
            $"&state=random_state" +   // 可自行生成随机字符串用于防跨站
            $"#wechat_redirect";

        // 重定向到企业微信授权页
        return Redirect(oauthUrl);
    }

    /// <summary>
    /// 步骤2：授权回调，接收code换取用户信息
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(string code, string state)
    {
        if (string.IsNullOrEmpty(code))
        {
            return Content("授权失败，未获取到code。");
        }

        try
        {
            var request = new CgibinGetTokenRequest();
            var response = await _client.ExecuteCgibinGetTokenAsync(request);


            // 1. 通过code换取访问凭证及用户UserId
            var userInfoReq = new CgibinAuthGetUserInfoRequest
            {
                AccessToken = response.AccessToken, // 库内部可自动获取，但需显式传递（也可通过client自动处理）
                Code = code
            };
            var userInfoResp = await _client.ExecuteCgibinAuthGetUserInfoAsync(userInfoReq);

            if (userInfoResp.IsSuccessful())
            {
                string userId = userInfoResp.UserId;

                // 2. 获取用户详细信息（如姓名、头像、手机号等）
                var userDetailReq = new CgibinUserGetRequest
                {
                    AccessToken = response.AccessToken,
                    UserId = userId
                };
                var userDetailResp = await _client.ExecuteCgibinUserGetAsync(userDetailReq);

                if (userDetailResp.IsSuccessful())
                {
                    // 将用户信息展示或存入Session等
                    return View(userDetailResp); // 假设有一个对应的视图
                }
                else
                {
                    return Content($"获取用户详情失败：{userDetailResp.ErrorMessage}");
                }
            }
            else
            {
                return Content($"换取UserId失败：{userInfoResp.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            return Content($"异常：{ex.Message}");
        }
    }
}

// 配置模型
public class WechatWorkOptions
{
    public string CorpId { get; set; }
    public int AgentId { get; set; }
    public string AgentSecret { get; set; }
    public string RedirectUri { get; set; }
}