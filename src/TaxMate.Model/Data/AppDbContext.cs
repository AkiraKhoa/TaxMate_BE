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
    
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();

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

    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<IncomeCategory> IncomeCategories => Set<IncomeCategory>();

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<ProductIngredient> ProductIngredients => Set<ProductIngredient>();
    public DbSet<IngredientPurchase> IngredientPurchases => Set<IngredientPurchase>();

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatReference> ChatReferences => Set<ChatReference>();
    
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();

    public DbSet<PaymentAccount> PaymentAccounts => Set<PaymentAccount>();
    public DbSet<TransactionItem> TransactionItems => Set<TransactionItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration from this assembly
        // User
        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(x => x.TaxCode)
            .IsUnique()
            .HasFilter("\"TaxCode\" IS NOT NULL");

        modelBuilder.Entity<User>()
            .HasIndex(x => x.Phone)
            .IsUnique()
            .HasFilter("\"Phone\" IS NOT NULL");

        modelBuilder.Entity<User>()
            .HasIndex(x => x.GoogleId)
            .IsUnique()
            .HasFilter("\"GoogleId\" IS NOT NULL");

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
            .Property(x => x.Category)
            .HasConversion<string>()
            .HasMaxLength(100);

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
            .HasOne(x => x.Business)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.BusinessId);

        modelBuilder.Entity<Transaction>()
            .HasIndex(x => x.BusinessId);

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

        // TransactionItem
        modelBuilder.Entity<TransactionItem>()
            .HasOne(x => x.Transaction)
            .WithMany(x => x.TransactionItems)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TransactionItem>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<TransactionItem>()
            .HasIndex(x => x.TransactionId);

        // PaymentAccount
        modelBuilder.Entity<PaymentAccount>()
            .HasOne(x => x.Business)
            .WithMany(x => x.PaymentAccounts)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PaymentAccount>()
            .HasIndex(x => x.BusinessId);

        modelBuilder.Entity<PaymentAccount>()
            .HasIndex(x => new { x.BusinessId, x.IsDefault });

        // Payment
        modelBuilder.Entity<Payment>()
            .HasOne(x => x.Transaction)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Payment>()
            .HasOne(x => x.PaymentAccount)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.PaymentAccountId)
            .OnDelete(DeleteBehavior.SetNull);

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
            .HasOne(x => x.Business)
            .WithMany() // Assuming BusinessProfile doesn't necessarily need a collection of ExpenseCategories
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExpenseCategory>()
            .HasIndex(x => x.BusinessId);

        modelBuilder.Entity<ExpenseCategory>()
            .HasIndex(x => new { x.BusinessId, x.CategoryName })
            .IsUnique();
        
        // Income
        modelBuilder.Entity<Income>()
            .HasOne(x => x.Business)
            .WithMany(x => x.Incomes)
            .HasForeignKey(x => x.BusinessId);

        modelBuilder.Entity<Income>()
            .HasOne(x => x.IncomeCategory)
            .WithMany(x => x.Incomes)
            .HasForeignKey(x => x.IncomeCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        
        modelBuilder.Entity<Income>()
            .HasIndex(x => x.BusinessId);

        modelBuilder.Entity<Income>()
            .HasIndex(x => x.IncomeDate);

        modelBuilder.Entity<Income>()
            .HasIndex(x => new
            {
                x.BusinessId,
                x.IncomeDate
            });
        
        // Income Category
        modelBuilder.Entity<IncomeCategory>()
            .HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IncomeCategory>()
            .HasIndex(x => x.BusinessId);

        modelBuilder.Entity<IncomeCategory>()
            .HasIndex(x => new { x.BusinessId, x.CategoryName })
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

        // Chat Conversation
        modelBuilder.Entity<ChatConversation>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatConversation>()
            .HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ChatConversation>()
            .HasIndex(x => x.UserId);

        modelBuilder.Entity<ChatConversation>()
            .HasIndex(x => new
            {
                x.UserId,
                x.Status
            });

        modelBuilder.Entity<ChatConversation>()
            .HasIndex(x => x.BusinessId);

        // Chat Message
        modelBuilder.Entity<ChatMessage>()
            .HasOne(x => x.Conversation)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(x => x.ConversationId);

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(x => x.CreatedAt);

        // Chat Reference
        modelBuilder.Entity<ChatReference>()
            .HasOne(x => x.Message)
            .WithMany(x => x.References)
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatReference>()
            .HasOne(x => x.LegalDocument)
            .WithMany()
            .HasForeignKey(x => x.LegalDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatReference>()
            .HasIndex(x => x.MessageId);

        modelBuilder.Entity<ChatReference>()
            .HasIndex(x => x.LegalDocumentId);
        
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
        
        //==========================================Seed Data==========================================
        var freePlanId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var smallBusinessPlanId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var premiumPlanId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        // Seed subscription plans
        modelBuilder.Entity<SubscriptionPlan>().HasData(
            new SubscriptionPlan
            {
                Id = freePlanId,
                Name = "Free",
                Description = "Basic features for small household businesses.",
                MonthlyPrice = 0,
                AnnualPrice = 0,
                MaxProducts = 50,
                MaxTransactionsPerMonth = 300,
                IsActive = true,
                SortOrder = 1
            },
            new SubscriptionPlan
            {
                Id = smallBusinessPlanId,
                Name = "Small Business",
                Description = "Expense tracking, profitability dashboard, AI tax guidance and RAG legal retrieval.",
                MonthlyPrice = 99000,
                AnnualPrice = 999000,
                MaxProducts = 500,
                MaxTransactionsPerMonth = 5000,
                IsActive = true,
                SortOrder = 2
            },
            new SubscriptionPlan
            {
                Id = premiumPlanId,
                Name = "Premium Business",
                Description = "Electronic invoice integration, advanced analytics and growth monitoring.",
                MonthlyPrice = 199000,
                AnnualPrice = 1999000,
                MaxProducts = null,
                MaxTransactionsPerMonth = null,
                IsActive = true,
                SortOrder = 3
            }
        );
        
        // Seed plan features
        modelBuilder.Entity<PlanFeature>().HasData(
            new PlanFeature
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
                SubscriptionPlanId = freePlanId,
                FeatureKey = "revenue_recording",
                FeatureName = "Revenue recording",
                IsEnabled = true
            },
            new PlanFeature
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
                SubscriptionPlanId = freePlanId,
                FeatureKey = "product_management",
                FeatureName = "Product management",
                IsEnabled = true
            },
            new PlanFeature
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1"),
                SubscriptionPlanId = smallBusinessPlanId,
                FeatureKey = "expense_tracking",
                FeatureName = "Expense recording and monitoring",
                IsEnabled = true
            },
            new PlanFeature
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"),
                SubscriptionPlanId = smallBusinessPlanId,
                FeatureKey = "rag_legal_retrieval",
                FeatureName = "RAG-based legal information retrieval",
                IsEnabled = true
            },
            new PlanFeature
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1"),
                SubscriptionPlanId = premiumPlanId,
                FeatureKey = "electronic_invoice",
                FeatureName = "Electronic invoice integration",
                IsEnabled = true
            },
            new PlanFeature
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc2"),
                SubscriptionPlanId = premiumPlanId,
                FeatureKey = "advanced_analytics",
                FeatureName = "Advanced business analytics",
                IsEnabled = true
            }
        );
    }
}
