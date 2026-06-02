using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Entities;

namespace TaxMate.Model.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Register DbSets here when entities are created, e.g.:
    // public DbSet<User> Users => Set<User>();
    public DbSet<User> Users => Set<User>();
    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();
    public DbSet<BusinessCategory> BusinessCategories => Set<BusinessCategory>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();

    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceDetail> InvoiceDetails => Set<InvoiceDetail>();

    public DbSet<TaxPeriod> TaxPeriods => Set<TaxPeriod>();
    public DbSet<TaxPayment> TaxPayments => Set<TaxPayment>();

    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<ProductIngredient> ProductIngredients => Set<ProductIngredient>();
    public DbSet<IngredientPurchase> IngredientPurchases => Set<IngredientPurchase>();

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();

    public DbSet<Notification> Notifications => Set<Notification>();
    
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration from this assembly
        // User
        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(x => x.TaxCode);

        // BusinessProfile
        modelBuilder.Entity<BusinessProfile>()
            .HasOne(x => x.Owner)
            .WithMany(x => x.BusinessProfiles)
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BusinessProfile>()
            .HasOne(x => x.MainCategory)
            .WithMany(x => x.BusinessProfiles)
            .HasForeignKey(x => x.MainCategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        // BusinessCategory
        modelBuilder.Entity<BusinessCategory>()
            .HasIndex(x => x.Code)
            .IsUnique();
        
        modelBuilder.Entity<BusinessCategory>()
            .HasIndex(x => x.Name);

        // Product
        modelBuilder.Entity<Product>()
            .HasOne(x => x.Business)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.BusinessId);

        modelBuilder.Entity<Product>()
            .HasIndex(x => x.BusinessId);

        modelBuilder.Entity<Product>()
            .HasIndex(x => x.Name);
        
        modelBuilder.Entity<Product>()
            .HasIndex(x => new
            {
                x.BusinessId,
                x.Status
            });

        // ProductPrice
        modelBuilder.Entity<ProductPrice>()
            .HasOne(x => x.Product)
            .WithMany(x => x.ProductPrices)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductPrice>()
            .HasIndex(x => new
            {
                x.ProductId,
                x.ApplyDate
            });
        // Transaction
        modelBuilder.Entity<Transaction>()
            .HasIndex(x => x.TransactionCode)
            .IsUnique();

        modelBuilder.Entity<Transaction>()
            .HasIndex(x => x.TransactionDate);

        modelBuilder.Entity<Transaction>()
            .HasOne(x => x.Invoice)
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .HasPrincipalKey(x => x.InvoiceNumber)
            .OnDelete(DeleteBehavior.SetNull);
        
        // Payment
        modelBuilder.Entity<Payment>()
            .HasOne(x => x.Transaction)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Payment>()
            .HasIndex(x => x.TransactionId);

        modelBuilder.Entity<Payment>()
            .HasIndex(x => x.PaidAt);
        
        // Invoice
        modelBuilder.Entity<Invoice>()
            .HasKey(x => x.InvoiceNumber);

        modelBuilder.Entity<Invoice>()
            .HasOne(x => x.Business)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.BusinessId);

        modelBuilder.Entity<Invoice>()
            .HasIndex(x => x.BusinessId);

        modelBuilder.Entity<Invoice>()
            .HasIndex(x => x.IssueDate);

        modelBuilder.Entity<Invoice>()
            .HasIndex(x => x.Status);
        
        // Invoice Detail
        modelBuilder.Entity<InvoiceDetail>()
            .HasKey(x => new
            {
                x.ProductId,
                x.InvoiceId
            });

        modelBuilder.Entity<InvoiceDetail>()
            .HasOne(x => x.Invoice)
            .WithMany(x => x.InvoiceDetails)
            .HasForeignKey(x => x.InvoiceId)
            .HasPrincipalKey(x => x.InvoiceNumber)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InvoiceDetail>()
            .HasOne(x => x.Product)
            .WithMany(x => x.InvoiceDetails)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        
        modelBuilder.Entity<Transaction>()
            .HasIndex(x => new
            {
                x.TransactionDate,
                x.Status
            });

        modelBuilder.Entity<Invoice>()
            .HasIndex(x => new
            {
                x.BusinessId,
                x.IssueDate
            });
        
        // Tax Period
        modelBuilder.Entity<TaxPeriod>()
            .HasOne(x => x.Business)
            .WithMany(x => x.TaxPeriods)
            .HasForeignKey(x => x.BusinessId);

        modelBuilder.Entity<TaxPeriod>()
            .HasIndex(x => new
            {
                x.BusinessId,
                x.Year,
                x.Month,
                x.Quarter
            });

        modelBuilder.Entity<TaxPeriod>()
            .HasIndex(x => x.Status);
        
        // Tax Payment
        modelBuilder.Entity<TaxPayment>()
            .HasOne(x => x.TaxPeriod)
            .WithMany(x => x.TaxPayments)
            .HasForeignKey(x => x.TaxPeriodId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaxPayment>()
            .HasIndex(x => x.PaidDate);
        
        // Expense
        modelBuilder.Entity<Expense>()
            .HasOne(x => x.Business)
            .WithMany(x => x.Expenses)
            .HasForeignKey(x => x.BusinessId);

        modelBuilder.Entity<Expense>()
            .HasOne(x => x.ExpenseCategory)
            .WithMany(x => x.Expenses)
            .HasForeignKey(x => x.ExpenseCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        
        modelBuilder.Entity<Expense>()
            .HasIndex(x => x.BusinessId);

        modelBuilder.Entity<Expense>()
            .HasIndex(x => x.ExpenseDate);

        modelBuilder.Entity<Expense>()
            .HasIndex(x => new
            {
                x.BusinessId,
                x.ExpenseDate
            });
        
        // Expense Category
        modelBuilder.Entity<ExpenseCategory>()
            .HasIndex(x => x.CategoryName)
            .IsUnique();
        
        // Product Ingredient
        modelBuilder.Entity<ProductIngredient>()
            .HasKey(x => new
            {
                x.ProductId,
                x.IngredientId
            });

        modelBuilder.Entity<ProductIngredient>()
            .HasOne(x => x.Product)
            .WithMany(x => x.ProductIngredients)
            .HasForeignKey(x => x.ProductId);

        modelBuilder.Entity<ProductIngredient>()
            .HasOne(x => x.Ingredient)
            .WithMany(x => x.ProductIngredients)
            .HasForeignKey(x => x.IngredientId);
        
        // Ingredient Purchase
        modelBuilder.Entity<IngredientPurchase>()
            .HasOne(x => x.Ingredient)
            .WithMany(x => x.IngredientPurchases)
            .HasForeignKey(x => x.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IngredientPurchase>()
            .HasIndex(x => x.PurchaseDate);

        modelBuilder.Entity<IngredientPurchase>()
            .HasIndex(x => x.IngredientId);
        
        // Subscription
        modelBuilder.Entity<PlanFeature>()
            .HasOne(x => x.SubscriptionPlan)
            .WithMany(x => x.PlanFeatures)
            .HasForeignKey(x => x.SubscriptionPlanId);

        modelBuilder.Entity<UserSubscription>()
            .HasOne(x => x.User)
            .WithMany(x => x.UserSubscriptions)
            .HasForeignKey(x => x.UserId);

        modelBuilder.Entity<UserSubscription>()
            .HasOne(x => x.SubscriptionPlan)
            .WithMany(x => x.UserSubscriptions)
            .HasForeignKey(x => x.SubscriptionPlanId);
        
        modelBuilder.Entity<UserSubscription>()
            .HasIndex(x => new
            {
                x.UserId,
                x.SubscriptionPlanId,
                x.Status
            });
        
        // Notification
        modelBuilder.Entity<Notification>()
            .HasOne(x => x.User)
            .WithMany(x => x.Notifications)
            .HasForeignKey(x => x.UserId);

        modelBuilder.Entity<Notification>()
            .HasIndex(x => new
            {
                x.UserId,
                x.IsRead
            });
        
        modelBuilder.Entity<Notification>()
            .HasIndex(x => new
            {
                x.UserId,
                x.CreatedAt
            });
        
        // Base Indexes for Dashboard
        modelBuilder.Entity<ProductPrice>()
            .HasIndex(x => x.ApplyDate);

        modelBuilder.Entity<TaxPeriod>()
            .HasIndex(x => x.DueDate);

        modelBuilder.Entity<TaxPayment>()
            .HasIndex(x => x.TaxPeriodId);

        modelBuilder.Entity<UserSubscription>()
            .HasIndex(x => x.Status);

        modelBuilder.Entity<UserSubscription>()
            .HasIndex(x => x.EndDate);
        
        // Legal Document
        modelBuilder.Entity<LegalDocument>()
            .HasIndex(x => x.DocumentCode)
            .IsUnique();

        modelBuilder.Entity<LegalDocument>()
            .HasIndex(x => x.DocumentType);

        modelBuilder.Entity<LegalDocument>()
            .HasIndex(x => x.Status);
        
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
