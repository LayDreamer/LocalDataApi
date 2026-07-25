using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Models;

namespace LocalDataApi.Services
{
    /// <summary>
    /// 企业微信登录（网页授权 OAuth2）服务。
    /// 流程：
    ///   1) 前端跳转 BuildAuthorizeUrl 生成的授权页（企业微信内静默授权 scope=snsapi_base）；
    ///   2) 用户在企业微信中打开后，重定向到配置的 RedirectUri 并携带 code；
    ///   3) 后端用该 code 调用 LoginAsync：code → userid（auth/getuserinfo）→ 成员详情（user/get）。
    /// </summary>
    public class WeChatWorkLoginService(
        WechatWorkClient client,
        WechatWorkTokenProvider tokenProvider,
        IConfiguration config,
        ILogger<WeChatWorkLoginService> logger)
    {
        private readonly string? _corpId = config["WeChatWork:CorpId"];
        private readonly string? _redirectUri = config["WeChatWork:RedirectUri"];

        /// <summary>
        /// 生成企业微信网页授权跳转地址，前端据此跳转，授权后携带 code 回调 RedirectUri。
        /// </summary>
        /// <param name="state">原样回传的状态值，可用于防 CSRF</param>
        /// <param name="scope">snsapi_base（静默，仅拿到 userid）/ snsapi_privateinfo（需用户确认，可拿敏感信息）</param>
        public string BuildAuthorizeUrl(string? state = null, string scope = "snsapi_base")
        {
            if (string.IsNullOrWhiteSpace(_corpId))
                throw new InvalidOperationException("企业微信 CorpId 未配置（WeChatWork:CorpId）");
            if (string.IsNullOrWhiteSpace(_redirectUri))
                throw new InvalidOperationException("企业微信 RedirectUri 未配置（WeChatWork:RedirectUri）");

            var redirect = Uri.EscapeDataString(_redirectUri);
            var stateValue = string.IsNullOrWhiteSpace(state) ? "state" : state;

            return $"https://open.weixin.qq.com/connect/oauth2/authorize" +
                   $"?appid={_corpId}" +
                   $"&redirect_uri={redirect}" +
                   $"&response_type=code" +
                   $"&scope={Uri.EscapeDataString(scope)}" +
                   $"&state={Uri.EscapeDataString(stateValue)}" +
                   "#wechat_redirect";
        }

        /// <summary>
        /// 企业微信登录：用回调 code 换取成员身份并返回成员详情。
        /// </summary>
        /// <param name="code">授权回调地址中的 code 参数</param>
        /// <param name="ct">取消令牌</param>
        public async Task<WeChatWorkLoginResult> LoginAsync(string code, CancellationToken ct = default)
        {
            var result = new WeChatWorkLoginResult();

            if (string.IsNullOrWhiteSpace(code))
            {
                result.ErrorMessage = "code 不能为空";
                return result;
            }

            try
            {
                var accessToken = await tokenProvider.GetAccessTokenAsync(ct);

                // 1) 用 code 换取 userid
                var userInfoResp = await client.ExecuteCgibinAuthGetUserInfoAsync(
                    new CgibinAuthGetUserInfoRequest
                    {
                        AccessToken = accessToken,
                        Code = code
                    }, ct);

                if (!userInfoResp.IsSuccessful())
                {
                    result.ErrorMessage = $"获取成员身份失败: [{userInfoResp.ErrorCode}] {userInfoResp.ErrorMessage}";
                    return result;
                }

                if (string.IsNullOrWhiteSpace(userInfoResp.UserId))
                {
                    // 非企业成员（如外部联系人）只有 OpenId，无法获取成员详情
                    result.OpenId = userInfoResp.OpenId;
                    result.ErrorMessage = "该账号不是企业成员，仅获取到 OpenId";
                    return result;
                }

                // 2) 用 userid 拉取成员详情
                var userResp = await client.ExecuteCgibinUserGetAsync(
                    new CgibinUserGetRequest
                    {
                        AccessToken = accessToken,
                        UserId = userInfoResp.UserId
                    }, ct);

                if (!userResp.IsSuccessful())
                {
                    result.ErrorMessage = $"获取成员详情失败: [{userResp.ErrorCode}] {userResp.ErrorMessage}";
                    return result;
                }

                result.IsSuccess = true;
                result.UserId = userResp.UserId;
                result.Name = userResp.Name;
                result.Email = userResp.Email;
                result.Mobile = userResp.MobileNumber;
                result.Avatar = userResp.AvatarUrl;
                result.Position = userResp.Position;
                result.Department = userResp.DepartmentIdList?.ToList();
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "企业微信登录异常（code={Code}）", code);
                result.ErrorMessage = ex.Message;
                return result;
            }
        }
    }

    /// <summary>
    /// 企业微信登录结果
    /// </summary>
    public class WeChatWorkLoginResult
    {
        /// <summary>是否登录成功</summary>
        public bool IsSuccess { get; set; }

        /// <summary>企业成员 UserId（企业内唯一标识）</summary>
        public string? UserId { get; set; }

        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Mobile { get; set; }
        public string? Avatar { get; set; }
        public string? Position { get; set; }

        /// <summary>所属部门 ID 列表</summary>
        public List<long>? Department { get; set; }

        /// <summary>非企业成员时返回的 OpenId</summary>
        public string? OpenId { get; set; }

        /// <summary>失败时的错误信息</summary>
        public string? ErrorMessage { get; set; }
    }
}
