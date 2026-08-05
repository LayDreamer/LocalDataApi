using System.ComponentModel.DataAnnotations.Schema;

using LocalDataApi.Domain.Erp;

namespace LocalDataApi.Domain.Ppc
{
    /// <summary>
    /// 外产_入库
    /// </summary>
    public class ExternalProductionWarehousing : ERPBase
    {
        [System.ComponentModel.DataAnnotations.Timestamp]
        public byte[]? RowVersion { get; set; }
        public string? 货号 { get; set; }    
        public string? 需求量 { get; set; }
        public string? 入库数量 { get; set; }
       public string? 分析单号 { get; set; }
       public string? 工单单号 { get; set; }
    }
}
