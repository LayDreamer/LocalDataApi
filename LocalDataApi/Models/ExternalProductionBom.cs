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
        //分析单号
        public string? 关联编号 { get; set; }
        public string? 父级编号 { get; set; }

        public string? 用量 { get; set; }
        public string? 仓库名称 { get; set; }

        public string? 仓库数 { get; set; }

        public string? 生产数 { get; set; }

        public string? 分析单号 { get; set; }

        public string ?交货日期{get;set;}
        public string? 产品属性 { get; set; }
        public string? 来源 { get; set; }
        public string? 单位 { get; set; }
        public string? 备注 { get; set; }
    }
}