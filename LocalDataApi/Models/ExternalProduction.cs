using System.ComponentModel.DataAnnotations.Schema;

namespace LocalDataApi.Models
{
    /// <summary>
    /// 外产_生产
    /// </summary>
    public class ExternalProduction : ERPBase
    {
        public string? 合同号 { get; set; }
        public string? 货号 { get; set; }
        public string? 排产编号 { get; set; }
        public string? 需求量 { get; set; }
        public string? 生产数量 { get; set; }
        public string? 关联编号 { get; set; }
    }
}
