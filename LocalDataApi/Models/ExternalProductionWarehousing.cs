using System.ComponentModel.DataAnnotations.Schema;

namespace LocalDataApi.Models
{
    /// <summary>
    /// 外产_入库
    /// </summary>
    public class ExternalProductionWarehousing : ERPBase
    {
        public string? 货号 { get; set; }    
        public string? 需求量 { get; set; }
        public string? 入库数量 { get; set; }
       public string? 分析单号 { get; set; }
       public string? 工单单号 { get; set; }
    }
}
