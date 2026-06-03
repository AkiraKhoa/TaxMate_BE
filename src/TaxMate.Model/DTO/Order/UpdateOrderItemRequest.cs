namespace TaxMate.Model.DTO;

public class UpdateOrderItemRequest
{
    public decimal? Quantity { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public string? Note { get; set; }
}
