namespace LocalDataApi.WeChatWork
{
    /// <summary>
    /// 企业微信消息类型枚举
    /// </summary>
    public enum WechatWorkMessageType
    {
        Text,
        Markdown,
        Image,
        News,       // 图文消息
        File,
        Card,
        // ... 可按需扩展
    }
}
