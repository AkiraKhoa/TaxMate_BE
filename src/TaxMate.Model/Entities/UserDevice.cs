using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class UserDevice : BaseEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string DeviceToken { get; set; } = null!;

    public string Platform { get; set; } = null!;

    public DateTime LastActiveAt { get; set; }

    public virtual User User { get; set; } = null!;
}