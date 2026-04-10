namespace LocalDataApi.Exceptions
{
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
}
