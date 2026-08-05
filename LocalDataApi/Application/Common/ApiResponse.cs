namespace LocalDataApi.Application.Common;

/// <summary>
/// 统一 API 响应包装。前端契约:{ Success, Message, Data, Timestamp }。
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
