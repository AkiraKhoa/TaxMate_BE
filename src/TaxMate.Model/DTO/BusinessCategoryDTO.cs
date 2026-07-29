namespace TaxMate.Model.DTO;

public class BusinessCategoryResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal VatRate { get; set; }
    public decimal PitRate { get; set; }
}
