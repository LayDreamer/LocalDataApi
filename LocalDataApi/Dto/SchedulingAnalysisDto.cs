namespace LocalDataApi.Dto
{
    public class SchedulingAnalysisDto
    {
        public string? 合同号 { get; set; }
        public string? 成品货号 { get; set; }       

        public string? 货号 { get; set; }
        public string? 层 { get; set; }

        public string? 产品属性 { get; set; }

        public string? 品名 { get; set; }
        public string? 规格 { get; set; }

        public string? 来源 { get; set; }
        public string? 用量 { get; set; }
        public string? 需求量 { get; set; }
        public string? 单位 { get; set; }
        public string? 备注 { get; set; }
        public string? 工序名称 { get; set; }
        public string? 工序车间 { get; set; }
        public string? 仓库名称 { get; set; }
        public string? 仓库数 { get; set; }
        public string? 在途数 { get; set; }
        public string? 在产需求 { get; set; }
        public string? 库存上限 { get; set; }
        public string? 库存下限 { get; set; }
        public string? 仓库可用 { get; set; }
        public string? 生产数 { get; set; }
        public string? 采购数 { get; set; }
        public string? 生产损耗 { get; set; }

        public List<SchedulingAnalysisDto>? 子集 { get; set; }
    }
}
