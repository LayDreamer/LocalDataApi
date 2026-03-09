
namespace LocalDataApi.Models
{
    public class WeChatWorkSettings
    {
        public required string CorpId { get; set; }      // 企业ID
        public required string AgentSecret { get; set; }      // 应用的Secret
        public int AgentId { get; set; }        // 应用ID（AgentId）
    }

}
