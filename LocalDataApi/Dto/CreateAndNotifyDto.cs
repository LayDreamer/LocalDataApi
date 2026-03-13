namespace LocalDataApi.Dto
{
    // 定义 DTO
    public class CreateAndNotifyDto:CreateSmartSheetDto
    {
        public List<string> NoticeUsers { get; set; }   // 接收通知的用户
    }
}
