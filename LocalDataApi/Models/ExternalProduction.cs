using System.ComponentModel.DataAnnotations.Schema;

namespace LocalDataApi.Models
{
    /// <summary>
    /// 外产_生产
    /// </summary>
    public class ExternalProduction : ERPBase
    {
        public string? 货号 { get; set; }
        public string? 排产编号 { get; set; }
        public string? 需求量 { get; set; }
        public string? 生产数量 { get; set; }
        public string? 分析单号 { get; set; }
        public string? 工单单号 { get; set; }
    }
}
