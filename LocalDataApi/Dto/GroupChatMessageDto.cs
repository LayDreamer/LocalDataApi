using LocalDataApi.WeChatWork;

namespace LocalDataApi.Dto
{
    /// <summary>
    /// 企业微信群聊消息统一 DTO（支持创建群聊+发送 / 仅发送）
    /// </summary>
    public class GroupChatMessageDto
    {
        /// <summary>群成员 userid 列表，至少2人，最多2000人（必须包含群主）。仅在创建群聊时必填。</summary>
        public List<string> UserIds { get; set; } = new List<string>();

        /// <summary>群聊名称，最多50个UTF-8字符。仅在创建群聊时必填。</summary>
        public string ChatName { get; set; } = string.Empty;

        /// <summary>群主 userid（必须在 UserIds 中）。仅在创建群聊时必填。</summary>
        public string OwnerUserId { get; set; } = string.Empty;

        /// <summary>群聊ID。发送消息时必填；创建群聊时可选（不传则由系统生成）。</summary>
        public string? ChatId { get; set; }

        /// <summary>消息内容（文本/Markdown 内容）</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>消息类型（支持 Text / Markdown / Card），默认文本</summary>
        public WechatWorkMessageType MsgType { get; set; } = WechatWorkMessageType.Text;

        /// <summary>卡片标题，仅在 MsgType 为 Card 时必填</summary>
        public string? Title { get; set; }

        /// <summary>卡片描述，仅在 MsgType 为 Card 时必填</summary>
        public string? Description { get; set; }

        /// <summary>卡片跳转链接，仅在 MsgType 为 Card 时必填</summary>
        public string? Url { get; set; }

        /// <summary>卡片按钮文字，仅在 MsgType 为 Card 时有效，默认"查看详情"</summary>
        public string? ButtonText { get; set; }
    }
}
