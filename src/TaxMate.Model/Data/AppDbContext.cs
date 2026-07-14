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
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();

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

    public DbSet<PaymentAccount> PaymentAccounts => Set<PaymentAccount>();
    public DbSet<TransactionItem> TransactionItems => Set<TransactionItem>();
    public DbSet<EInvoiceConfig> EInvoiceConfigs => Set<EInvoiceConfig>();

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

        // EInvoiceConfig
        modelBuilder.Entity<EInvoiceConfig>()
            .HasOne(x => x.Business)
            .WithOne(x => x.EInvoiceConfig)
            .HasForeignKey<EInvoiceConfig>(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

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

        // ProductCategory relationship
        modelBuilder.Entity<ProductCategory>()
            .HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Product>()
            .HasOne(x => x.ProductCategory)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.ProductCategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        // Supplier relationships
        modelBuilder.Entity<Supplier>()
            .HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IngredientPurchase>()
            .HasOne(x => x.Supplier)
            .WithMany(x => x.IngredientPurchases)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Expense>()
            .HasOne(x => x.Supplier)
            .WithMany(x => x.Expenses)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

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
            .HasOne(x => x.Business)
            .WithMany(x => x.IngredientPurchases)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IngredientPurchase>()
            .HasIndex(x => x.PurchaseDate);

        modelBuilder.Entity<IngredientPurchase>()
            .HasIndex(x => x.IngredientId);

        modelBuilder.Entity<IngredientPurchase>()
            .HasIndex(x => x.BusinessId);

        modelBuilder.Entity<IngredientPurchase>()
            .HasIndex(x => x.InvoiceNumber);

        modelBuilder.Entity<IngredientPurchase>()
            .HasIndex(x => new { x.BusinessId, x.PurchaseDate });
        
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

        modelBuilder.Entity<UserSubscription>()
            .HasIndex(x => x.PaymentOrderCode)
            .IsUnique()
            .HasFilter("\"PaymentOrderCode\" IS NOT NULL");
        
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

        // Seed Subscription Plans
        var freePlanId = Guid.Parse("a1d1c694-d271-460b-8835-2b2e6a1b8c1d");
        var smallPlanId = Guid.Parse("b2d2c694-d271-460b-8835-2b2e6a1b8c2d");
        var premiumPlanId = Guid.Parse("c3d3c694-d271-460b-8835-2b2e6a1b8c3d");

        modelBuilder.Entity<SubscriptionPlan>().HasData(
            new SubscriptionPlan
            {
                Id = freePlanId,
                Name = "Gói Miễn Phí",
                Description = "Trải nghiệm các tính năng quản lý cơ bản",
                MonthlyPrice = 0m,
                AnnualPrice = 0m,
                MaxProducts = 50,
                MaxTransactionsPerMonth = 100,
                IsActive = true,
                SortOrder = 0
            },
            new SubscriptionPlan
            {
                Id = smallPlanId,
                Name = "Gói Hộ Kinh Doanh",
                Description = "Phù hợp cho hộ kinh doanh cá thể nhỏ",
                MonthlyPrice = 99000m,
                AnnualPrice = 990000m,
                MaxProducts = 500,
                MaxTransactionsPerMonth = 1000,
                IsActive = true,
                SortOrder = 1
            },
            new SubscriptionPlan
            {
                Id = premiumPlanId,
                Name = "Gói Doanh Nghiệp Cao Cấp",
                Description = "Giải pháp toàn diện cho doanh nghiệp tăng trưởng",
                MonthlyPrice = 199000m,
                AnnualPrice = 1990000m,
                MaxProducts = null,
                MaxTransactionsPerMonth = null,
                IsActive = true,
                SortOrder = 2
            }
        );

        // Seed Plan Features
        modelBuilder.Entity<PlanFeature>().HasData(
            // Free Tier features
            new PlanFeature { Id = Guid.Parse("f1111111-1111-1111-1111-111111111111"), SubscriptionPlanId = freePlanId, FeatureKey = "revenue_recording", FeatureName = "Ghi nhận doanh thu", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("f2222222-2222-2222-2222-222222222222"), SubscriptionPlanId = freePlanId, FeatureKey = "revenue_aggregation_viz", FeatureName = "Tổng hợp doanh thu theo tháng/năm", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("f3333333-3333-3333-3333-333333333333"), SubscriptionPlanId = freePlanId, FeatureKey = "daily_revenue_reporting", FeatureName = "Báo cáo doanh thu hàng ngày", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("f4444444-4444-4444-4444-444444444444"), SubscriptionPlanId = freePlanId, FeatureKey = "order_history_tracking", FeatureName = "Theo dõi lịch sử đơn hàng", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("f5555555-5555-5555-5555-555555555555"), SubscriptionPlanId = freePlanId, FeatureKey = "best_selling_categories", FeatureName = "Danh mục sản phẩm bán chạy nhất", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("f6666666-6666-6666-6666-666666666666"), SubscriptionPlanId = freePlanId, FeatureKey = "product_management", FeatureName = "Quản lý sản phẩm", IsEnabled = true },

            // Small Business features
            new PlanFeature { Id = Guid.Parse("b1111111-1111-1111-1111-111111111111"), SubscriptionPlanId = smallPlanId, FeatureKey = "revenue_recording", FeatureName = "Ghi nhận doanh thu", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("b2222222-2222-2222-2222-222222222222"), SubscriptionPlanId = smallPlanId, FeatureKey = "revenue_aggregation_viz", FeatureName = "Tổng hợp doanh thu theo tháng/năm", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("b3333333-3333-3333-3333-333333333333"), SubscriptionPlanId = smallPlanId, FeatureKey = "daily_revenue_reporting", FeatureName = "Báo cáo doanh thu hàng ngày", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("b4444444-4444-4444-4444-444444444444"), SubscriptionPlanId = smallPlanId, FeatureKey = "order_history_tracking", FeatureName = "Theo dõi lịch sử đơn hàng", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("b5555555-5555-5555-5555-555555555555"), SubscriptionPlanId = smallPlanId, FeatureKey = "best_selling_categories", FeatureName = "Danh mục sản phẩm bán chạy nhất", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("b6666666-6666-6666-6666-666666666666"), SubscriptionPlanId = smallPlanId, FeatureKey = "product_management", FeatureName = "Quản lý sản phẩm", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("b7777777-7777-7777-7777-777777777777"), SubscriptionPlanId = smallPlanId, FeatureKey = "expense_recording_monitoring", FeatureName = "Ghi nhận & giám sát chi phí", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("b8888888-8888-8888-8888-888888888888"), SubscriptionPlanId = smallPlanId, FeatureKey = "estimated_profitability_dashboard", FeatureName = "Bảng điều khiển lợi nhuận ước tính", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("b9999999-9999-9999-9999-999999999999"), SubscriptionPlanId = smallPlanId, FeatureKey = "ai_tax_guidance", FeatureName = "Tư vấn thuế AI", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("baaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), SubscriptionPlanId = smallPlanId, FeatureKey = "rag_legal_retrieval", FeatureName = "Tra cứu thông tin luật RAG", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), SubscriptionPlanId = smallPlanId, FeatureKey = "business_insight_reports", FeatureName = "Báo cáo insight kinh doanh", IsEnabled = true },

            // Premium Business features
            new PlanFeature { Id = Guid.Parse("e1111111-1111-1111-1111-111111111111"), SubscriptionPlanId = premiumPlanId, FeatureKey = "revenue_recording", FeatureName = "Ghi nhận doanh thu", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("e2222222-2222-2222-2222-222222222222"), SubscriptionPlanId = premiumPlanId, FeatureKey = "revenue_aggregation_viz", FeatureName = "Tổng hợp doanh thu theo tháng/năm", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("e3333333-3333-3333-3333-333333333333"), SubscriptionPlanId = premiumPlanId, FeatureKey = "daily_revenue_reporting", FeatureName = "Báo cáo doanh thu hàng ngày", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("e4444444-4444-4444-4444-444444444444"), SubscriptionPlanId = premiumPlanId, FeatureKey = "order_history_tracking", FeatureName = "Theo dõi lịch sử đơn hàng", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("e5555555-5555-5555-5555-555555555555"), SubscriptionPlanId = premiumPlanId, FeatureKey = "best_selling_categories", FeatureName = "Danh mục sản phẩm bán chạy nhất", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("e6666666-6666-6666-6666-666666666666"), SubscriptionPlanId = premiumPlanId, FeatureKey = "product_management", FeatureName = "Quản lý sản phẩm", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("e7777777-7777-7777-7777-777777777777"), SubscriptionPlanId = premiumPlanId, FeatureKey = "expense_recording_monitoring", FeatureName = "Ghi nhận & giám sát chi phí", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("e8888888-8888-8888-8888-888888888888"), SubscriptionPlanId = premiumPlanId, FeatureKey = "estimated_profitability_dashboard", FeatureName = "Bảng điều khiển lợi nhuận ước tính", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("e9999999-9999-9999-9999-999999999999"), SubscriptionPlanId = premiumPlanId, FeatureKey = "ai_tax_guidance", FeatureName = "Tư vấn thuế AI", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("eaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), SubscriptionPlanId = premiumPlanId, FeatureKey = "rag_legal_retrieval", FeatureName = "Tra cứu thông tin luật RAG", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("ebbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), SubscriptionPlanId = premiumPlanId, FeatureKey = "business_insight_reports", FeatureName = "Báo cáo insight kinh doanh", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("eaaaaaaa-cccc-cccc-cccc-cccccccccccc"), SubscriptionPlanId = premiumPlanId, FeatureKey = "einvoice_integration", FeatureName = "Tích hợp hóa đơn điện tử", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("ebbbbbbb-dddd-dddd-dddd-dddddddddddd"), SubscriptionPlanId = premiumPlanId, FeatureKey = "advanced_analytics", FeatureName = "Phân tích kinh doanh nâng cao", IsEnabled = true },
            new PlanFeature { Id = Guid.Parse("ececcccc-eeee-eeee-eeee-eeeeeeeeeeee"), SubscriptionPlanId = premiumPlanId, FeatureKey = "growth_readiness_monitoring", FeatureName = "Giám sát mức độ sẵn sàng tăng trưởng", IsEnabled = true }
        );

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
