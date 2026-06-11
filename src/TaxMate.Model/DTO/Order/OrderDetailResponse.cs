namespace TaxMate.Model.DTO;

public class OrderDetailResponse
{
    public Guid TransactionId { get; set; }
    public string TransactionCode { get; set; } = null!;
    public DateTime TransactionDate { get; set; }
    public string Status { get; set; } = null!;
    public string? Note { get; set; }
    public string? InvoiceNumber { get; set; }

    public decimal SubTotal { get; set; }

    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }

    public string? SurchargeName { get; set; }
    public string? SurchargeType { get; set; }
    public decimal? SurchargeValue { get; set; }
    public decimal SurchargeAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public List<OrderItemResponse> Items { get; set; } = new();
    public List<OrderPaymentResponse> Payments { get; set; } = new();
}
