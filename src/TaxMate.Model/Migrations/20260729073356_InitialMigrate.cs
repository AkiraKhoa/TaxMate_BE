using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxMate.Model.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigrate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessCategories",
                columns: table => new
                {
                    BusinessCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VatRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    PitRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FormIndicatorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FormSectionCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessCategories", x => x.BusinessCategoryId);
                });

            migrationBuilder.CreateTable(
                name: "LegalDocuments",
                columns: table => new
                {
                    LegalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentCode = table.Column<string>(type: "text", nullable: false),
                    DocumentName = table.Column<string>(type: "text", nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: true),
                    AuthorityLevel = table.Column<string>(type: "text", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExpiredDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SourceFileName = table.Column<string>(type: "text", nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileHash = table.Column<string>(type: "text", nullable: false),
                    IsIndexed = table.Column<bool>(type: "boolean", nullable: false),
                    TotalPages = table.Column<int>(type: "integer", nullable: true),
                    TotalChunks = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalDocuments", x => x.LegalDocumentId);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxProducts = table.Column<int>(type: "integer", nullable: true),
                    MaxTransactionsPerMonth = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GoogleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AccountStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EmailVerificationToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    EmailVerificationTokenExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FeatureName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanFeatures_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ProvinceCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    WardCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MainCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreferElectronicInvoice = table.Column<bool>(type: "boolean", nullable: false),
                    SePayCompanyXid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastSePayLinkTokenXid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TaxAdministrationAreaCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ManagingTaxAuthority = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TaxAuthorityLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CollectingAuthority = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BusinessLocationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessProfiles_BusinessCategories_MainCategoryId",
                        column: x => x.MainCategoryId,
                        principalTable: "BusinessCategories",
                        principalColumn: "BusinessCategoryId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BusinessProfiles_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceToken = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    LastActiveAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDevices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BillingCycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AutoRenew = table.Column<bool>(type: "boolean", nullable: false),
                    PaymentOrderCode = table.Column<long>(type: "bigint", nullable: true),
                    PaymentLinkId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PaymentStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatConversations_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChatConversations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EInvoiceConfigs",
                columns: table => new
                {
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClientSecret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProviderAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InvoiceTemplateCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Symbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    QuotaWarningThreshold = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EInvoiceConfigs", x => x.BusinessId);
                    table.ForeignKey(
                        name: "FK_EInvoiceConfigs_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    ExpenseCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.ExpenseCategoryId);
                    table.ForeignKey(
                        name: "FK_ExpenseCategories_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncomeCategories",
                columns: table => new
                {
                    IncomeCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeCategories", x => x.IncomeCategoryId);
                    table.ForeignKey(
                        name: "FK_IncomeCategories_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EstimatedPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ingredients_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InvoiceTemplateCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Symbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PdfUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TaxAuthorityCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OfficialPdfUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OfficialXmlUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SePayTrackingCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SePayReferenceCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SePayMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BuyerTaxCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BuyerCompanyName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    BuyerAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BuyerEmail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.InvoiceNumber);
                    table.ForeignKey(
                        name: "FK_Invoices_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAccounts",
                columns: table => new
                {
                    PaymentAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankShortName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CassoAccessToken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CassoRefreshToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CassoConnectedAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SePayBankAccountXid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAccounts", x => x.PaymentAccountId);
                    table.ForeignKey(
                        name: "FK_PaymentAccounts_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductCategories_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Suppliers_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: true),
                    Quarter = table.Column<int>(type: "integer", nullable: true),
                    PeriodStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SalesRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxableRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonalIncomeTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmountDebt = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PaidDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxPeriods_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxPeriods_BusinessProfiles_BusinessProfileId",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    PromptTokens = table.Column<int>(type: "integer", nullable: false),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    ModelName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Incomes",
                columns: table => new
                {
                    IncomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IncomeDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReceiptImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incomes", x => x.IncomeId);
                    table.ForeignKey(
                        name: "FK_Incomes_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Incomes_IncomeCategories_IncomeCategoryId",
                        column: x => x.IncomeCategoryId,
                        principalTable: "IncomeCategories",
                        principalColumn: "IncomeCategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DiscountValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SurchargeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SurchargeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SurchargeValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SurchargeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InvoiceId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TransactionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_Transactions_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transactions_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceNumber",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_BusinessCategories_BusinessCategoryId",
                        column: x => x.BusinessCategoryId,
                        principalTable: "BusinessCategories",
                        principalColumn: "BusinessCategoryId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Products_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Products_ProductCategories_ProductCategoryId",
                        column: x => x.ProductCategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    ExpenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpenseDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReceiptImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PaidDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.ExpenseId);
                    table.ForeignKey(
                        name: "FK_Expenses_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Expenses_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "ExpenseCategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IngredientPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReceiptImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientPurchases_BusinessProfiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientPurchases_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientPurchases_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TaxCalculations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CalculationRuleVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TotalRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTaxableRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalVatTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPersonalIncomeTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTaxBeforeExemption = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalExemptionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTaxPayableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualRevenueAtCalculation = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ApplicableRevenueThreshold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecommendedFormCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RemainingPitDeduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CalculatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxCalculations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxCalculations_TaxPeriods_TaxPeriodId",
                        column: x => x.TaxPeriodId,
                        principalTable: "TaxPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkId = table.Column<string>(type: "text", nullable: false),
                    SimilarityScore = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatReferences_ChatMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatReferences_LegalDocuments_LegalDocumentId",
                        column: x => x.LegalDocumentId,
                        principalTable: "LegalDocuments",
                        principalColumn: "LegalDocumentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.PaymentId);
                    table.ForeignKey(
                        name: "FK_Payments_PaymentAccounts_PaymentAccountId",
                        column: x => x.PaymentAccountId,
                        principalTable: "PaymentAccounts",
                        principalColumn: "PaymentAccountId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Payments_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "TransactionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceDetails",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceDetails", x => new { x.ProductId, x.InvoiceId });
                    table.ForeignKey(
                        name: "FK_InvoiceDetails_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceNumber",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceDetails_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductIngredients",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductIngredients", x => new { x.ProductId, x.IngredientId });
                    table.ForeignKey(
                        name: "FK_ProductIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductIngredients_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ApplyDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPrices_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransactionItems",
                columns: table => new
                {
                    TransactionItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    DiscountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DiscountValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CostAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionItems", x => x.TransactionItemId);
                    table.ForeignKey(
                        name: "FK_TransactionItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TransactionItems_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "TransactionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxCalculationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxCalculationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IndicatorCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BusinessActivityCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BusinessActivityName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    BusinessLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessLocationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TotalRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatTaxableRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ZeroRatedVatRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatTaxRate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    VatTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonalIncomeTaxableRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonalIncomeTaxDeductibleRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonalIncomeTaxRate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    PersonalIncomeTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatNonTaxableRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonalIncomeTaxRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    BusinessCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxCalculationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxCalculationLines_BusinessCategories_BusinessCategoryId",
                        column: x => x.BusinessCategoryId,
                        principalTable: "BusinessCategories",
                        principalColumn: "BusinessCategoryId");
                    table.ForeignKey(
                        name: "FK_TaxCalculationLines_TaxCalculations_TaxCalculationId",
                        column: x => x.TaxCalculationId,
                        principalTable: "TaxCalculations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxDeclarations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxCalculationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DeclarationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DeclarationType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SupplementNumber = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TaxpayerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TaxpayerAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AuthorizedDeclarerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AuthorizedDeclarerTaxCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TaxAgentName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TaxAgentTaxCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TaxAgentContractNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TaxAgentContractDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TotalRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalVatTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPersonalIncomeTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatExemptionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonalIncomeTaxExemptionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatPayableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonalIncomeTaxPayableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTaxPayableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SubmissionMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SubmissionReference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PdfFileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    XmlFileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RemainingPitDeduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxDeclarations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxDeclarations_TaxCalculations_TaxCalculationId",
                        column: x => x.TaxCalculationId,
                        principalTable: "TaxCalculations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxDeclarations_TaxPeriods_TaxPeriodId",
                        column: x => x.TaxPeriodId,
                        principalTable: "TaxPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxDeclarationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxDeclarationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IndicatorCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BusinessActivityCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BusinessActivityName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    BusinessLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessLocationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TotalRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatTaxableRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ZeroRatedVatRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatTaxRate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    VatTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonalIncomeTaxableRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonalIncomeTaxDeductibleRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonalIncomeTaxRate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    PersonalIncomeTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatNonTaxableRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonalIncomeTaxRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxDeclarationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxDeclarationLines_TaxDeclarations_TaxDeclarationId",
                        column: x => x.TaxDeclarationId,
                        principalTable: "TaxDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxDeclarationObligations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxDeclarationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BusinessLocationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StateBudgetContent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IndicatorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AssessedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExemptionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StateBudgetChapterCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StateBudgetSubsectionCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AdministrativeAreaCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CollectingAuthority = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TaxAuthority = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxDeclarationObligations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxDeclarationObligations_TaxDeclarations_TaxDeclarationId",
                        column: x => x.TaxDeclarationId,
                        principalTable: "TaxDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxDeclarationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TransactionReference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    StateBudgetChapterCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StateBudgetSubsectionCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AdministrativeAreaCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReceiptFileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxPayments_TaxDeclarations_TaxDeclarationId",
                        column: x => x.TaxDeclarationId,
                        principalTable: "TaxDeclarations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TaxPayments_TaxPeriods_TaxPeriodId",
                        column: x => x.TaxPeriodId,
                        principalTable: "TaxPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "BusinessCategories",
                columns: new[] { "BusinessCategoryId", "Code", "CreatedAt", "Description", "EffectiveFrom", "EffectiveTo", "FormIndicatorCode", "FormSectionCode", "IsActive", "Name", "PitRate", "UpdatedAt", "VatRate" },
                values: new object[,]
                {
                    { new Guid("a0000001-0000-4000-8000-000000000001"), "DIST_GOODS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GTGT 1%, TNCN 0.5%", null, null, null, null, true, "Phân phối, cung cấp hàng hóa", 0.5m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1m },
                    { new Guid("a0000001-0000-4000-8000-000000000002"), "PROD_TRANSPORT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GTGT 3%, TNCN 1.5%", null, null, null, null, true, "Sản xuất, vận tải, dịch vụ gắn HH, XD có NVL", 1.5m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3m },
                    { new Guid("a0000001-0000-4000-8000-000000000003"), "SERVICE_CONSTRUCT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GTGT 5%, TNCN 2%", null, null, null, null, true, "Dịch vụ, XD không bao thầu NVL", 2m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5m },
                    { new Guid("a0000001-0000-4000-8000-000000000004"), "ASSET_INSURANCE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GTGT 5%, TNCN 5%", null, null, null, null, true, "Cho thuê tài sản / đại lý BH, xổ số, BHĐC…", 5m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5m },
                    { new Guid("a0000001-0000-4000-8000-000000000005"), "OTHER", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GTGT 2%, TNCN 1%", null, null, null, null, true, "Hoạt động khác", 1m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2m },
                    { new Guid("d1111111-1111-1111-1111-111111111111"), "FNB", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hoạt động dịch vụ ăn uống có gắn với hàng hóa.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "d", "I", true, "Ăn uống, nhà hàng, F&B", 1.50m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3.00m },
                    { new Guid("d2222222-2222-2222-2222-222222222222"), "SERVICE", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Dịch vụ, xây dựng không bao thầu nguyên vật liệu.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "b", "I", true, "Dịch vụ", 2.00m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 5.00m }
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "Id", "AnnualPrice", "Description", "IsActive", "MaxProducts", "MaxTransactionsPerMonth", "MonthlyPrice", "Name", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d"), 0m, "Trải nghiệm các tính năng quản lý cơ bản", true, 50, 100, 0m, "Gói Miễn Phí", 0 },
                    { new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d"), 990000m, "Phù hợp cho hộ kinh doanh cá thể nhỏ", true, 500, 1000, 99000m, "Gói Hộ Kinh Doanh", 1 },
                    { new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d"), 1990000m, "Giải pháp toàn diện cho doanh nghiệp tăng trưởng", true, null, null, 199000m, "Gói Doanh Nghiệp Cao Cấp", 2 }
                });

            migrationBuilder.InsertData(
                table: "PlanFeatures",
                columns: new[] { "Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId" },
                values: new object[,]
                {
                    { new Guid("b1111111-1111-1111-1111-111111111111"), "revenue_recording", "Ghi nhận doanh thu", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b2222222-2222-2222-2222-222222222222"), "revenue_aggregation_viz", "Tổng hợp doanh thu theo tháng/năm", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b3333333-3333-3333-3333-333333333333"), "daily_revenue_reporting", "Báo cáo doanh thu hàng ngày", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b4444444-4444-4444-4444-444444444444"), "order_history_tracking", "Theo dõi lịch sử đơn hàng", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b5555555-5555-5555-5555-555555555555"), "best_selling_categories", "Danh mục sản phẩm bán chạy nhất", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b6666666-6666-6666-6666-666666666666"), "product_management", "Quản lý sản phẩm", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b7777777-7777-7777-7777-777777777777"), "expense_recording_monitoring", "Ghi nhận & giám sát chi phí", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b8888888-8888-8888-8888-888888888888"), "estimated_profitability_dashboard", "Bảng điều khiển lợi nhuận ước tính", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("b9999999-9999-9999-9999-999999999999"), "ai_tax_guidance", "Tư vấn thuế AI", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("baaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "rag_legal_retrieval", "Tra cứu thông tin luật RAG", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "business_insight_reports", "Báo cáo insight kinh doanh", true, new Guid("b2d2c694-d271-460b-8835-2b2e6a1b8c2d") },
                    { new Guid("e1111111-1111-1111-1111-111111111111"), "revenue_recording", "Ghi nhận doanh thu", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e2222222-2222-2222-2222-222222222222"), "revenue_aggregation_viz", "Tổng hợp doanh thu theo tháng/năm", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e3333333-3333-3333-3333-333333333333"), "daily_revenue_reporting", "Báo cáo doanh thu hàng ngày", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e4444444-4444-4444-4444-444444444444"), "order_history_tracking", "Theo dõi lịch sử đơn hàng", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e5555555-5555-5555-5555-555555555555"), "best_selling_categories", "Danh mục sản phẩm bán chạy nhất", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e6666666-6666-6666-6666-666666666666"), "product_management", "Quản lý sản phẩm", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e7777777-7777-7777-7777-777777777777"), "expense_recording_monitoring", "Ghi nhận & giám sát chi phí", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e8888888-8888-8888-8888-888888888888"), "estimated_profitability_dashboard", "Bảng điều khiển lợi nhuận ước tính", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("e9999999-9999-9999-9999-999999999999"), "ai_tax_guidance", "Tư vấn thuế AI", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("eaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "rag_legal_retrieval", "Tra cứu thông tin luật RAG", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("eaaaaaaa-cccc-cccc-cccc-cccccccccccc"), "einvoice_integration", "Tích hợp hóa đơn điện tử", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("ebbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "business_insight_reports", "Báo cáo insight kinh doanh", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("ebbbbbbb-dddd-dddd-dddd-dddddddddddd"), "advanced_analytics", "Phân tích kinh doanh nâng cao", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("ececcccc-eeee-eeee-eeee-eeeeeeeeeeee"), "growth_readiness_monitoring", "Giám sát mức độ sẵn sàng tăng trưởng", true, new Guid("c3d3c694-d271-460b-8835-2b2e6a1b8c3d") },
                    { new Guid("f1111111-1111-1111-1111-111111111111"), "revenue_recording", "Ghi nhận doanh thu", true, new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d") },
                    { new Guid("f2222222-2222-2222-2222-222222222222"), "revenue_aggregation_viz", "Tổng hợp doanh thu theo tháng/năm", true, new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d") },
                    { new Guid("f3333333-3333-3333-3333-333333333333"), "daily_revenue_reporting", "Báo cáo doanh thu hàng ngày", true, new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d") },
                    { new Guid("f4444444-4444-4444-4444-444444444444"), "order_history_tracking", "Theo dõi lịch sử đơn hàng", true, new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d") },
                    { new Guid("f5555555-5555-5555-5555-555555555555"), "best_selling_categories", "Danh mục sản phẩm bán chạy nhất", true, new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d") },
                    { new Guid("f6666666-6666-6666-6666-666666666666"), "product_management", "Quản lý sản phẩm", true, new Guid("a1d1c694-d271-460b-8835-2b2e6a1b8c1d") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCategories_Code",
                table: "BusinessCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCategories_Name",
                table: "BusinessCategories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProfiles_MainCategoryId",
                table: "BusinessProfiles",
                column: "MainCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProfiles_OwnerId",
                table: "BusinessProfiles",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_BusinessId",
                table: "ChatConversations",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserId",
                table: "ChatConversations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserId_Status",
                table: "ChatConversations",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId",
                table: "ChatMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_CreatedAt",
                table: "ChatMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatReferences_LegalDocumentId",
                table: "ChatReferences",
                column: "LegalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatReferences_MessageId",
                table: "ChatReferences",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_BusinessId",
                table: "ExpenseCategories",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_BusinessId_CategoryName",
                table: "ExpenseCategories",
                columns: new[] { "BusinessId", "CategoryName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BusinessId",
                table: "Expenses",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BusinessId_ExpenseDate",
                table: "Expenses",
                columns: new[] { "BusinessId", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseCategoryId",
                table: "Expenses",
                column: "ExpenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseDate",
                table: "Expenses",
                column: "ExpenseDate");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_SupplierId",
                table: "Expenses",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeCategories_BusinessId",
                table: "IncomeCategories",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeCategories_BusinessId_CategoryName",
                table: "IncomeCategories",
                columns: new[] { "BusinessId", "CategoryName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_BusinessId",
                table: "Incomes",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_BusinessId_IncomeDate",
                table: "Incomes",
                columns: new[] { "BusinessId", "IncomeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_IncomeCategoryId",
                table: "Incomes",
                column: "IncomeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_IncomeDate",
                table: "Incomes",
                column: "IncomeDate");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientPurchases_BusinessId",
                table: "IngredientPurchases",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientPurchases_BusinessId_PurchaseDate",
                table: "IngredientPurchases",
                columns: new[] { "BusinessId", "PurchaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientPurchases_IngredientId",
                table: "IngredientPurchases",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientPurchases_InvoiceNumber",
                table: "IngredientPurchases",
                column: "InvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientPurchases_PurchaseDate",
                table: "IngredientPurchases",
                column: "PurchaseDate");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientPurchases_SupplierId",
                table: "IngredientPurchases",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_BusinessId",
                table: "Ingredients",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_BusinessId_Name",
                table: "Ingredients",
                columns: new[] { "BusinessId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_InvoiceId",
                table: "InvoiceDetails",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BusinessId",
                table: "Invoices",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BusinessId_IssueDate",
                table: "Invoices",
                columns: new[] { "BusinessId", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_IssueDate",
                table: "Invoices",
                column: "IssueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status",
                table: "Invoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_DocumentCode",
                table: "LegalDocuments",
                column: "DocumentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_DocumentType",
                table: "LegalDocuments",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_Status",
                table: "LegalDocuments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAccounts_BusinessId",
                table: "PaymentAccounts",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAccounts_BusinessId_IsDefault",
                table: "PaymentAccounts",
                columns: new[] { "BusinessId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaidAt",
                table: "Payments",
                column: "PaidAt");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentAccountId",
                table: "Payments",
                column: "PaymentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TransactionId",
                table: "Payments",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFeatures_SubscriptionPlanId",
                table: "PlanFeatures",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_BusinessId",
                table: "ProductCategories",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductIngredients_IngredientId",
                table: "ProductIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_ApplyDate",
                table: "ProductPrices",
                column: "ApplyDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_ProductId_ApplyDate",
                table: "ProductPrices",
                columns: new[] { "ProductId", "ApplyDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessCategoryId",
                table: "Products",
                column: "BusinessCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessId",
                table: "Products",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessId_BusinessCategoryId",
                table: "Products",
                columns: new[] { "BusinessId", "BusinessCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessId_Status",
                table: "Products",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                table: "Products",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductCategoryId",
                table: "Products",
                column: "ProductCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_BusinessId",
                table: "Suppliers",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxCalculationLines_BusinessCategoryId",
                table: "TaxCalculationLines",
                column: "BusinessCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxCalculationLines_TaxCalculationId_SectionCode_IndicatorC~",
                table: "TaxCalculationLines",
                columns: new[] { "TaxCalculationId", "SectionCode", "IndicatorCode", "BusinessLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxCalculations_TaxPeriodId_IsCurrent",
                table: "TaxCalculations",
                columns: new[] { "TaxPeriodId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxCalculations_TaxPeriodId_Version",
                table: "TaxCalculations",
                columns: new[] { "TaxPeriodId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationLines_TaxDeclarationId",
                table: "TaxDeclarationLines",
                column: "TaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationObligations_TaxDeclarationId",
                table: "TaxDeclarationObligations",
                column: "TaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_DeclarationCode",
                table: "TaxDeclarations",
                column: "DeclarationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_TaxCalculationId",
                table: "TaxDeclarations",
                column: "TaxCalculationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_TaxPeriodId_IsCurrent",
                table: "TaxDeclarations",
                columns: new[] { "TaxPeriodId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_TaxPeriodId_Version",
                table: "TaxDeclarations",
                columns: new[] { "TaxPeriodId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxPayments_TaxDeclarationId",
                table: "TaxPayments",
                column: "TaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxPayments_TaxPeriodId",
                table: "TaxPayments",
                column: "TaxPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxPeriods_BusinessId_PeriodType_Year_Month_Quarter",
                table: "TaxPeriods",
                columns: new[] { "BusinessId", "PeriodType", "Year", "Month", "Quarter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxPeriods_BusinessId_Status",
                table: "TaxPeriods",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxPeriods_BusinessId_Year_Month_Quarter",
                table: "TaxPeriods",
                columns: new[] { "BusinessId", "Year", "Month", "Quarter" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxPeriods_BusinessProfileId",
                table: "TaxPeriods",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxPeriods_DueDate",
                table: "TaxPeriods",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_TaxPeriods_Status",
                table: "TaxPeriods",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionItems_ProductId",
                table: "TransactionItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionItems_TransactionId",
                table: "TransactionItems",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BusinessId",
                table: "Transactions",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_InvoiceId",
                table: "Transactions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionCode",
                table: "Transactions",
                column: "TransactionCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionDate",
                table: "Transactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionDate_Status",
                table: "Transactions",
                columns: new[] { "TransactionDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_UserId",
                table: "UserDevices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleId",
                table: "Users",
                column: "GoogleId",
                unique: true,
                filter: "\"GoogleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Phone",
                table: "Users",
                column: "Phone",
                unique: true,
                filter: "\"Phone\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TaxCode",
                table: "Users",
                column: "TaxCode",
                unique: true,
                filter: "\"TaxCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_EndDate",
                table: "UserSubscriptions",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_PaymentOrderCode",
                table: "UserSubscriptions",
                column: "PaymentOrderCode",
                unique: true,
                filter: "\"PaymentOrderCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_Status",
                table: "UserSubscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_SubscriptionPlanId",
                table: "UserSubscriptions",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_UserId_SubscriptionPlanId_Status",
                table: "UserSubscriptions",
                columns: new[] { "UserId", "SubscriptionPlanId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatReferences");

            migrationBuilder.DropTable(
                name: "EInvoiceConfigs");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "Incomes");

            migrationBuilder.DropTable(
                name: "IngredientPurchases");

            migrationBuilder.DropTable(
                name: "InvoiceDetails");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PlanFeatures");

            migrationBuilder.DropTable(
                name: "ProductIngredients");

            migrationBuilder.DropTable(
                name: "ProductPrices");

            migrationBuilder.DropTable(
                name: "TaxCalculationLines");

            migrationBuilder.DropTable(
                name: "TaxDeclarationLines");

            migrationBuilder.DropTable(
                name: "TaxDeclarationObligations");

            migrationBuilder.DropTable(
                name: "TaxPayments");

            migrationBuilder.DropTable(
                name: "TransactionItems");

            migrationBuilder.DropTable(
                name: "UserDevices");

            migrationBuilder.DropTable(
                name: "UserSubscriptions");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "LegalDocuments");

            migrationBuilder.DropTable(
                name: "ExpenseCategories");

            migrationBuilder.DropTable(
                name: "IncomeCategories");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "PaymentAccounts");

            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "TaxDeclarations");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropTable(
                name: "ChatConversations");

            migrationBuilder.DropTable(
                name: "TaxCalculations");

            migrationBuilder.DropTable(
                name: "ProductCategories");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "TaxPeriods");

            migrationBuilder.DropTable(
                name: "BusinessProfiles");

            migrationBuilder.DropTable(
                name: "BusinessCategories");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
