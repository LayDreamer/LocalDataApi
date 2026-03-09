using LocalDataApi.WeChatWork;

namespace LocalDataApi.Dto
{
    public class SendMessageDto
    {
        public List<string> Users { get; set; } = new List<string>();

        public string? Content { get; set; }

        public string? Title { get; set; }  

        public string? Description { get; set; }

        public string? Url { get; set; }

        public WechatWorkMessageType MsgType { get; set; }

    }
}
