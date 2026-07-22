namespace TaxMate.Service.Exceptions;

public class AccountInactiveException : Exception
{
    public AccountInactiveException()
        : base("Tài khoản đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.")
    {
    }
}
