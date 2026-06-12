using System.ComponentModel.DataAnnotations.Schema;

namespace LocalDataApi.Models
{
    /// <summary>
    /// 外产_BOM
    /// </summary>
    public class ExternalProductionBOM : ERPBase
    {
        public string? 货号 { get; set; }
        public string? 层 { get; set; }
        public string? 品名 { get; set; }
        public string? 规格 { get; set; }
        public string? 关联编号 { get; set; }
        public string? 父级编号 { get; set; }
    }
}