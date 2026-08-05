using LocalDataApi.Infrastructure.WeChatWork;
using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Models;

namespace LocalDataApi.Application.WeChatWork;

/// <summary>
/// 企业微信用户信息服务实现(网页授权 OAuth2 场景)。
/// AccessToken 统一走 <see cref="WechatWorkTokenProvider"/> 缓存,避免每次请求重复获取。
/// </summary>
public class WechatWorkUserService : IWechatWorkUserService
{
    private readonly WechatWorkClient _client;
    private readonly WechatWorkTokenProvider _tokenProvider;
    private readonly string _corpId;
    private readonly string _agentId;
    private readonly ILogger<WechatWorkUserService> _logger;

    public WechatWorkUserService(
        WechatWorkClient client,
        WechatWorkTokenProvider tokenProvider,
        IConfiguration configuration,
        ILogger<WechatWorkUserService> logger)
    {
        _client = client;
        _tokenProvider = tokenProvider;
        _corpId = configuration["WechatWork:CorpId"] ?? string.Empty;
        _agentId = configuration["WechatWork:AgentId"]?.ToString() ?? string.Empty;
        _logger = logger;
    }

    /// <summary>
    /// 生成授权跳转URL
    /// </summary>
    public string GenerateAuthorizeUrl(string redirectUri, string state = "STATE")
    {
        var encodedUri = System.Web.HttpUtility.UrlEncode(redirectUri);

        // snsapi_base: 静默授权,只能获取userid
        return $"https://open.weixin.qq.com/connect/oauth2/authorize?appid={_corpId}&redirect_uri={encodedUri}&response_type=code&scope=snsapi_base&state={state}&agentid={_agentId}#wechat_redirect";
    }

    /// <summary>
    /// 通过授权code获取用户基本信息
    /// </summary>
    public async Task<WechatWorkUserInfo> GetUserInfoByCodeAsync(string code)
    {
        try
        {
            // 1. 获取 AccessToken(带缓存)
            string accessToken = await _tokenProvider.GetAccessTokenAsync();

            // 2. 通过 code 获取用户 ID
            var userInfoRequest = new CgibinUserGetUserInfoRequest
            {
                AccessToken = accessToken,
                Code = code
            };
            var userInfoResponse = await _client.ExecuteCgibinUserGetUserInfoAsync(userInfoRequest);

            if (!userInfoResponse.IsSuccessful())
            {
                _logger.LogError("获取用户信息失败: {ErrorMsg}", userInfoResponse.ErrorMessage);
                throw new Exception($"获取用户信息失败: {userInfoResponse.ErrorMessage}");
            }

            return new WechatWorkUserInfo
            {
                UserId = userInfoResponse.UserId,
                DeviceId = userInfoResponse.DeviceId,
                OpenId = userInfoResponse.OpenId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户基本信息异常");
            throw;
        }
    }

    /// <summary>
    /// 通过userid获取用户详细信息
    /// </summary>
    public async Task<WechatWorkUserDetailInfo> GetUserDetailByUserIdAsync(string userId)
    {
        try
        {
            // 1. 获取 AccessToken(带缓存)
            string accessToken = await _tokenProvider.GetAccessTokenAsync();

            // 2. 获取用户详细信息
            var getUserRequest = new CgibinUserGetRequest
            {
                AccessToken = accessToken,
                UserId = userId
            };
            var getUserResponse = await _client.ExecuteCgibinUserGetAsync(getUserRequest);

            if (!getUserResponse.IsSuccessful())
            {
                _logger.LogError("获取用户详情失败: {ErrorMsg}", getUserResponse.ErrorMessage);
                throw new Exception($"获取用户详情失败: {getUserResponse.ErrorMessage}");
            }

            var user = getUserResponse;
            return new WechatWorkUserDetailInfo
            {
                UserId = user.UserId,
                Name = user.Name,
                Alias = user.Alias,
                Gender = user.Gender,
                Mobile = user.MobileNumber,
                Email = user.Email,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户详细信息异常, UserId: {UserId}", userId);
            throw;
        }
    }
}

/// <summary>
/// 企业微信用户信息服务接口
/// </summary>
public interface IWechatWorkUserService
{
    /// <summary>
    /// 生成授权跳转URL
    /// </summary>
    string GenerateAuthorizeUrl(string redirectUri, string state = "STATE");

    /// <summary>
    /// 通过授权code获取用户基本信息
    /// </summary>
    Task<WechatWorkUserInfo> GetUserInfoByCodeAsync(string code);

    /// <summary>
    /// 通过userid获取用户详细信息
    /// </summary>
    Task<WechatWorkUserDetailInfo> GetUserDetailByUserIdAsync(string userId);
}

/// <summary>
/// 用户基本信息
/// </summary>
public class WechatWorkUserInfo
{
    public string UserId { get; set; }
    public string DeviceId { get; set; }
    public string OpenId { get; set; }
}

/// <summary>
/// 用户详细信息
/// </summary>
public class WechatWorkUserDetailInfo
{
    public string UserId { get; set; }
    public string Name { get; set; }
    public string Alias { get; set; }
    public int? Gender { get; set; }
    public string Mobile { get; set; }
    public string Email { get; set; }
    public string BizMail { get; set; }
    public string Avatar { get; set; }
    public string ThumbAvatar { get; set; }
    public string Position { get; set; }
    public string Department { get; set; }
    public string MainDepartment { get; set; }
    public string[] Departments { get; set; }
    public int? Status { get; set; }
    public long? Enable { get; set; }
    public string IsLeader { get; set; }
    public string Leader { get; set; }
    public string[] DirectLeader { get; set; }
    public string ExternalPosition { get; set; }
    public string Address { get; set; }
    public string OpenUserId { get; set; }
}
