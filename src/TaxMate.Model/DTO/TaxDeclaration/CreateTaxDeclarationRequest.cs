namespace TaxMate.Model.DTO.TaxDeclaration;

public class CreateTaxDeclarationRequest
{
    public string DeclarationType { get; set; } = "Initial";

    public int? SupplementNumber { get; set; }
}