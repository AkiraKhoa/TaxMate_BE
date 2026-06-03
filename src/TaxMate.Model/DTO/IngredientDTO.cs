namespace TaxMate.Model.DTO;

public class CreateIngredientRequest
{
    public string Name { get; set; } = null!;
    public string? Unit { get; set; }
    public decimal? EstimatedPrice { get; set; }
}

public class UpdateIngredientRequest
{
    public string Name { get; set; } = null!;
    public string? Unit { get; set; }
    public decimal? EstimatedPrice { get; set; }
}

public class IngredientResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Unit { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
