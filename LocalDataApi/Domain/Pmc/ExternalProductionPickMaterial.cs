using System.ComponentModel.DataAnnotations.Schema;

using LocalDataApi.Domain.Erp;

namespace LocalDataApi.Domain.Pmc
{
    /// <summary>
    /// 外产_领料
    /// </summary>
    public class ExternalProductionPickMaterial : ERPBase
    {
        [System.ComponentModel.DataAnnotations.Timestamp]
        public byte[]? RowVersion { get; set; }
        public string? 货号 { get; set; }
        public string? 需求量 { get; set; }
        public string? 出库数量 { get; set; }
        public string? 分析单号 { get; set; }
        public string? 父级编号 { get; set; }
        public string ? 来源编号 { get; set; }
    }
}
