using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class TaxPayment : BaseEntity
{
    public Guid Id { get; set; }

    public Guid TaxPeriodId { get; set; }

    [Precision(18,2)]
    public decimal Amount { get; set; }

    public DateTime PaidDate { get; set; }

    public TaxPeriod TaxPeriod { get; set; } = null!;
}