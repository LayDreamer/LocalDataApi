using System.ComponentModel.DataAnnotations.Schema;

using LocalDataApi.Domain.Erp;

namespace LocalDataApi.Domain.Pmc
{
    /// <summary>
    /// 外产_发运
    /// </summary>
    public class ExternalProductionShipment : ERPBase
    {
        [System.ComponentModel.DataAnnotations.Timestamp]
        public byte[]? RowVersion { get; set; }

        public string? 货号 { get; set; }
        public string? 排产编号 { get; set; }
        public string? 需求量 { get; set; }
        public string? 发运数量 { get; set; }
        public string? 分析单号 { get; set; }
    }
}
