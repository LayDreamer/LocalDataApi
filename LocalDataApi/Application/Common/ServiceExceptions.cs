namespace LocalDataApi.Application.Common;

/// <summary>
/// 业务服务异常基类。由全局异常中间件统一转换为对应 HTTP 状态码。
/// </summary>
public class ServiceException : Exception
{
    public ServiceException(string message)
        : base(message)
    {
    }

    public ServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public class ValidationException : ServiceException
{
    public ValidationException(string message)
        : base(message)
    {
    }
}

public class NotFoundException : ServiceException
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}

public class ConflictException : ServiceException
{
    public ConflictException(string message)
        : base(message)
    {
    }
}
