using LocalDataApi.Application.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Api.Middlewares;

/// <summary>
/// 全局异常中间件:将业务异常统一转换为对应的 HTTP 状态码与 ApiResponse 结构。
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var (statusCode, message) = exception switch
            {
                ValidationException or ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
                NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                ConflictException or DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, exception.Message),
                DbUpdateException { InnerException: SqlException sqlException }
                    when sqlException.Number is 2601 or 2627 =>
                    (StatusCodes.Status409Conflict, "数据已被其他请求创建或修改,请刷新后重试。"),
                OperationCanceledException when context.RequestAborted.IsCancellationRequested =>
                    (499, "客户端已取消请求。"),
                _ => (StatusCodes.Status500InternalServerError, "服务器内部错误。")
            };

            if (statusCode >= 500)
                logger.LogError(exception, "Unhandled request error. TraceId: {TraceId}", context.TraceIdentifier);
            else
                logger.LogWarning(exception, "Request rejected with {StatusCode}. TraceId: {TraceId}", statusCode, context.TraceIdentifier);

            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(new ApiResponse<object>
            {
                Success = false,
                Message = message,
                Data = new { TraceId = context.TraceIdentifier }
            });
        }
    }
}
