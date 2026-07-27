namespace TaxMate.Model.DTO.TaxDeclaration;

public class SubmitTaxDeclarationRequest
{
    public string SubmissionMethod { get; set; } = "Manual";

    public string? SubmissionReference { get; set; }
}