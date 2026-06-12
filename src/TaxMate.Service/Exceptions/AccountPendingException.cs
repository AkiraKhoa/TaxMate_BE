namespace TaxMate.Service.Exceptions;

public class AccountPendingException : Exception
{
    public AccountPendingException()
        : base("Vui lòng xác minh email để kích hoạt tài khoản.")
    {
    }
}
