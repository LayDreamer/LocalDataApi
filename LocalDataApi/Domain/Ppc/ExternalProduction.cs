using System.ComponentModel.DataAnnotations.Schema;

using LocalDataApi.Domain.Erp;

namespace LocalDataApi.Domain.Ppc
{
    /// <summary>
    /// 外产_生产
    /// </summary>
    public class ExternalProduction : ERPBase
    {
        [System.ComponentModel.DataAnnotations.Timestamp]
        public byte[]? RowVersion { get; set; }
        public string? 货号 { get; set; }
        public string? 排产编号 { get; set; }
        public string? 需求量 { get; set; }
        public string? 生产数量 { get; set; }
        public string? 分析单号 { get; set; }
        public string? 工单单号 { get; set; }
        public string? 来源 { get; set; }
        public string? 工序车间 { get; set; }
        public string? 工序 { get; set; }
        public string? 工单层级 { get; set; }
        public string? 电压 { get; set; }
        public string? 线圈 { get; set; }
        public string? 订单数 { get; set; }
        public string? 单位 { get; set; }
        public string? 仓库名称 { get; set; }
        public string? 备注 { get; set; }
        public string? 用量 { get; set; }
    }
}
