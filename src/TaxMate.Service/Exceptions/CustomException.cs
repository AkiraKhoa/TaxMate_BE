namespace TaxMate.Service.Exceptions;

public class BadRequestException : Exception
{
    public BadRequestException(string message)
        : base(message)
    {
    }
}

public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}

public class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}

public class UnprocessableEntityException : Exception
{
    public string ErrorCode { get; }

    public UnprocessableEntityException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}