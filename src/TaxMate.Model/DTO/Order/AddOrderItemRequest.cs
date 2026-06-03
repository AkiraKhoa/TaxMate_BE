using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO;

public class AddOrderItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    public decimal Quantity { get; set; }

    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public string? Note { get; set; }
}
