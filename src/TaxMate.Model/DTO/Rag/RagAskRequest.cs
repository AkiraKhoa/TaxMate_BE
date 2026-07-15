using System.ComponentModel.DataAnnotations;

namespace TaxMate.Model.DTO.Rag;

public class RagAskRequest
{
    [Required]
    [MaxLength(2000)]
    public string Question { get; set; } = null!;
}