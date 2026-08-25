namespace TaxMate.Model.DTO;

/// <summary>
/// Xác nhận hoặc xóa số dư đầu kỳ của một tài khoản tiền.
/// Hai trường phải cùng có giá trị hoặc cùng null.
/// </summary>
public sealed class UpdatePaymentAccountInitialBalanceRequest
{
    public decimal? InitialBalance { get; set; }

    public DateOnly? InitialBalanceDate { get; set; }
}
