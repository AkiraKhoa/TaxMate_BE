using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class RevenueThresholdAlert : BaseEntity
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    /// <summary>
    /// Calendar year of the current tax period when the alert was sent.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Quarter (1-4) of the current tax period when the alert was sent.
    /// </summary>
    public int Quarter { get; set; }

    public DateTime WindowStart { get; set; }

    public DateTime WindowEnd { get; set; }

    [Precision(18, 2)]
    public decimal TotalRevenue { get; set; }

    public DateTime SentAt { get; set; }

    [MaxLength(30)]
    public string ThresholdCode { get; set; } = RevenueThresholdCodes.Crossed1B;

    [Precision(18, 2)]
    public decimal ThresholdAmount { get; set; }

    [MaxLength(30)]
    public string Status { get; set; }
        = RevenueThresholdAlertStatuses.PendingReview;

    public DateTime? ResolvedAt { get; set; }

    public User Owner { get; set; } = null!;
}
