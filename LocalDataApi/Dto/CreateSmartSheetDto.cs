namespace LocalDataApi.Dto
{
    // 定义 DTO
    public class CreateSmartSheetDto
    {
        public string Title { get; set; }
        public List<string>? AdminUserIds { get; set; } // 文档管理员，可选
        
    }
}
