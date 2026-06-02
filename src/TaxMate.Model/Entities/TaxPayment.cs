using Microsoft.EntityFrameworkCore;

namespace TaxMate.Model.Entities;

public class TaxPayment
{
    public Guid Id { get; set; }

    public Guid TaxPeriodId { get; set; }

    [Precision(18,2)]
    public decimal Amount { get; set; }

    public DateTime PaidDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public TaxPeriod TaxPeriod { get; set; } = null!;
}