using System.ComponentModel.DataAnnotations;
using TaxMate.Model.Common;

namespace TaxMate.Model.Entities;

public class BusinessProfile : BaseEntity
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    [Required]
    [MaxLength(300)]
    public string BusinessName { get; set; } = null!;

    [MaxLength(20)]
    public string? ProvinceCode { get; set; }

    [MaxLength(20)]
    public string? WardCode { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    public Guid? MainCategoryId { get; set; }

    public bool PreferElectronicInvoice { get; set; }

    public bool IsActive { get; set; } = true;

    public User Owner { get; set; } = null!;

    public BusinessCategory? MainCategory { get; set; }

    public ICollection<Product> Products { get; set; }
        = new List<Product>();

    public ICollection<Ingredient> Ingredients { get; set; }
        = new List<Ingredient>();

    public ICollection<Invoice> Invoices { get; set; }
        = new List<Invoice>();

    public ICollection<TaxPeriod> TaxPeriods { get; set; }
        = new List<TaxPeriod>();

    public ICollection<Expense> Expenses { get; set; }
        = new List<Expense>();

    public ICollection<PaymentAccount> PaymentAccounts { get; set; }
        = new List<PaymentAccount>();

    public ICollection<Transaction> Transactions { get; set; }
        = new List<Transaction>();
}