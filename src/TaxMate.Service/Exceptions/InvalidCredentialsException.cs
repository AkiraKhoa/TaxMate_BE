namespace TaxMate.Service.Exceptions;

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Email/số điện thoại hoặc mật khẩu không đúng.")
    {
    }
}
