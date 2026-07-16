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

    [MaxLength(100)]
    public string? SePayCompanyXid { get; set; }

    /// <summary>
    /// Link token XID m\u1edbi nh\u1ea5t \u0111\u01b0\u1ee3c t\u1ea1o khi shop owner b\u1eaft \u0111\u1ea7u li\u00ean k\u1ebft ng\u00e2n h\u00e0ng.
    /// D\u00f9ng \u0111\u1ec3 trace b\u1eaft s\u1ef1 ki\u1ec7n BANK_ACCOUNT_LINKED v\u1ec1 \u0111\u00fang business.
    /// </summary>
    [MaxLength(100)]
    public string? LastSePayLinkTokenXid { get; set; }


    public bool IsActive { get; set; } = true;

    public User Owner { get; set; } = null!;

    public BusinessCategory? MainCategory { get; set; }

    public ICollection<Product> Products { get; set; }
        = new List<Product>();

    public ICollection<Invoice> Invoices { get; set; }
        = new List<Invoice>();

    public ICollection<TaxPeriod> TaxPeriods { get; set; }
        = new List<TaxPeriod>();

    public ICollection<Expense> Expenses { get; set; }
        = new List<Expense>();

    public ICollection<Income> Incomes { get; set; }
        = new List<Income>();

    public ICollection<PaymentAccount> PaymentAccounts { get; set; }
        = new List<PaymentAccount>();

    public ICollection<Transaction> Transactions { get; set; }
        = new List<Transaction>();

    public ICollection<IngredientPurchase> IngredientPurchases { get; set; }
        = new List<IngredientPurchase>();

    public EInvoiceConfig? EInvoiceConfig { get; set; }
}