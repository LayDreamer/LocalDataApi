namespace LocalDataApi.Models
{
    public class ERPBase
    {
        public string? 编号 { get; set; }       
        public string? 用户编号 { get; set; }
        public string? 用户铭 { get; set; }

        public string? 修改状态 { get; set; }

        public string? 创建时间{ get; set; }

        public string?  锁定用户{ get; set; }

        public string? 审核过程 { get; set; }

        public string? 打印 { get; set; }
    }


    public class ERPUser
    {
        public string? ID { get; set; }
        public string? username { get; set; }
        public string? usercode { get; set; }
      
    }
}
