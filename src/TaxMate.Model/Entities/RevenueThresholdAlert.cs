using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class RevenueThresholdAlert : BaseEntity
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    /// <summary>
    /// Calendar year (năm dương lịch) whose doanh thu triggered the alert.
    /// Unique with OwnerId so one email is sent per owner per year.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Quarter (1-4) in which the owner first crossed the threshold that year.
    /// </summary>
    public int Quarter { get; set; }

    public DateTime WindowStart { get; set; }

    public DateTime WindowEnd { get; set; }

    [Precision(18, 2)]
    public decimal TotalRevenue { get; set; }

    public DateTime SentAt { get; set; }

    public User Owner { get; set; } = null!;
}
