using System.Text.RegularExpressions;

namespace TaxMate.Service.Common;

public static partial class UserInputValidator
{
    public static void ValidateTaxCode(string taxCode)
    {
        if (string.IsNullOrWhiteSpace(taxCode) || !TaxCodeRegex().IsMatch(taxCode))
        {
            throw new ArgumentException("Số căn cước công dân phải gồm đúng 12 chữ số.");
        }
    }

    public static void ValidatePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || !PhoneRegex().IsMatch(phone))
        {
            throw new ArgumentException("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");
        }
    }

    public static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !EmailRegex().IsMatch(email))
        {
            throw new ArgumentException("Email không hợp lệ.");
        }
    }

    public static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new ArgumentException("Mật khẩu phải có ít nhất 8 ký tự.");
        }
    }

    [GeneratedRegex(@"^\d{12}$")]
    private static partial Regex TaxCodeRegex();

    [GeneratedRegex(@"^0\d{9}$")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
