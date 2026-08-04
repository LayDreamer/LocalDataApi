namespace LocalDataApi.Dto
{
    public class PMCRequestDto : PagedRequestDtoBase
    {
        public string? 编号 { get; set; }
        public string? 合同号 { get; set; }
        public string? 排产编号 { get; set; }        
        public  string? 分析单号 { get; set; }
        public string? 货号 { get; set; }   
        public string? 线圈货号 { get; set; }
        public string? 补充数据{get;set;}
        public string? 生产类型 { get; set; }
    }
}
