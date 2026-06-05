using System.ComponentModel.DataAnnotations.Schema;

namespace LocalDataApi.Models
{
    /// <summary>
    /// 外产_发运
    /// </summary>
    public class ExternalProductionShipment : ERPBase
    {
        public string? 合同号 { get; set; }
        public string? 货号 { get; set; }
        public string? 排产编号 { get; set; }
        public string? 需求量 { get; set; }
        public string? 发运数量 { get; set; }
         public string? 关联编号 { get; set; }
    }
}
