namespace LocalDataApi.Models
{
    /// <summary>
    /// 企业微信应用群聊记录
    /// </summary>
    public class WechatWorkGroupChat:ERPBase
    {
        /// <summary>群聊ID（企业微信返回的 chatId）</summary>
        public string ChatId { get; set; } = string.Empty;

        /// <summary>群聊名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>群主 UserId</summary>
        public string OwnerUserId { get; set; } = string.Empty;

        /// <summary>群成员 UserId 列表（逗号分隔）</summary>
        public string MemberUserIds { get; set; } = string.Empty;

        /// <summary>创建时间</summary>
        public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
