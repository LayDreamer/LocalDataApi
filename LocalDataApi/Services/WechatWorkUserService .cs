using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Models;

namespace LocalDataApi.Services
{

    /// <summary>
    /// 企业微信用户信息服务实现
    /// </summary>
    public class WechatWorkUserService : IWechatWorkUserService
    {
        private readonly WechatWorkClient _client;
        private readonly string _corpId;
        private readonly string _agentId;
        private readonly string _agentSecret;
        private readonly ILogger<WechatWorkUserService> _logger;

        public WechatWorkUserService(
            WechatWorkClient client,
            string corpId,
            string agentId,
            string agentSecret,
            ILogger<WechatWorkUserService> logger)
        {
            _client = client;
            _corpId = corpId;
            _agentId = agentId;
            _agentSecret = agentSecret;
            _logger = logger;
        }

        /// <summary>
        /// 生成授权跳转URL
        /// </summary>
        public string GenerateAuthorizeUrl(string redirectUri, string state = "STATE")
        {
            var encodedUri = System.Web.HttpUtility.UrlEncode(redirectUri);

            // snsapi_base: 静默授权，只能获取userid
            // snsapi_userinfo: 需要用户确认，可获取更详细信息
            return $"https://open.weixin.qq.com/connect/oauth2/authorize?appid={_corpId}&redirect_uri={encodedUri}&response_type=code&scope=snsapi_base&state={state}&agentid={_agentId}#wechat_redirect";
        }

        /// <summary>
        /// 通过授权code获取用户基本信息
        /// </summary>
        public async Task<WechatWorkUserInfo> GetUserInfoByCodeAsync(string code)
        {
            try
            {
                // 1. 获取 AccessToken
                var tokenRequest = new CgibinGetTokenRequest();
                
                var tokenResponse = await _client.ExecuteCgibinGetTokenAsync(tokenRequest);

                if (!tokenResponse.IsSuccessful())
                {
                    _logger.LogError("获取AccessToken失败: {ErrorMsg}", tokenResponse.ErrorMessage);
                    throw new Exception($"获取AccessToken失败: {tokenResponse.ErrorMessage}");
                }

                string accessToken = tokenResponse.AccessToken;

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
                // 1. 获取 AccessToken
                var tokenRequest = new CgibinGetTokenRequest();
               
                var tokenResponse = await _client.ExecuteCgibinGetTokenAsync(tokenRequest);

                if (!tokenResponse.IsSuccessful())
                {
                    _logger.LogError("获取AccessToken失败: {ErrorMsg}", tokenResponse.ErrorMessage);
                    throw new Exception($"获取AccessToken失败: {tokenResponse.ErrorMessage}");
                }

                string accessToken = tokenResponse.AccessToken;

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
        /// <param name="redirectUri">授权回调地址</param>
        /// <param name="state">状态参数</param>
        /// <returns>授权URL</returns>
        string GenerateAuthorizeUrl(string redirectUri, string state = "STATE");

        /// <summary>
        /// 通过授权code获取用户基本信息
        /// </summary>
        /// <param name="code">授权code</param>
        /// <returns>用户基本信息</returns>
        Task<WechatWorkUserInfo> GetUserInfoByCodeAsync(string code);

        /// <summary>
        /// 通过userid获取用户详细信息
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>用户详细信息</returns>
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
}
