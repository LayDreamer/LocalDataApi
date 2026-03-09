namespace LocalDataApi.Dto
{
    // 定义 DTO
    public class CreateAndNotifyDto
    {
        public string Title { get; set; }
        public List<string>? AdminUserIds { get; set; } // 文档管理员，可选
        public List<string> NoticeUsers { get; set; }   // 接收通知的用户
    }
}
