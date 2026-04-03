
namespace LocalDataApi.Dto
{
    //线圈查询结果
    public class CoilSearchResultDto
    {
        public string 线圈 { get; set; }
        public string 品名 { get; set; }
        public string 规格 { get; set; }
        public string 预留 { get; set; } // 对应 SQL 中的空字符串列
    }
}
