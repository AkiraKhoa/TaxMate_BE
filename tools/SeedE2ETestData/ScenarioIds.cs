namespace SeedE2ETestData;

internal static class ScenarioIds
{
    internal const int PrimaryTaxYear = 2026;
    internal const decimal AnnualRevenueThreshold = 1_000_000_000m;
    internal const decimal OwnerCExpectedAnnualRevenue = 800_000_000m;

    internal static readonly Guid OwnerA = Guid.Parse("a1000000-0000-4000-8000-000000000001");
    internal static readonly Guid OwnerB = Guid.Parse("b1000000-0000-4000-8000-000000000001");
    internal static readonly Guid OwnerC = Guid.Parse("c1000000-0000-4000-8000-000000000001");

    internal static readonly Guid A1 = Guid.Parse("a1000000-0000-4000-8000-000000000101");
    internal static readonly Guid A2 = Guid.Parse("a1000000-0000-4000-8000-000000000102");
    internal static readonly Guid B1 = Guid.Parse("b1000000-0000-4000-8000-000000000101");
    internal static readonly Guid B2 = Guid.Parse("b1000000-0000-4000-8000-000000000102");
    internal static readonly Guid C1 = Guid.Parse("c1000000-0000-4000-8000-000000000101");

    internal static readonly Guid A1ProductCategory = Guid.Parse("a1000000-0000-4000-8000-000000000201");
    internal static readonly Guid A2ProductCategory = Guid.Parse("a1000000-0000-4000-8000-000000000202");
    internal static readonly Guid B1ProductCategory = Guid.Parse("b1000000-0000-4000-8000-000000000201");
    internal static readonly Guid B2ProductCategory = Guid.Parse("b1000000-0000-4000-8000-000000000202");
    internal static readonly Guid C1ProductCategory = Guid.Parse("c1000000-0000-4000-8000-000000000201");

    // Source-category IDs are public test-fixture coordinates: API-driven flows
    // can create BusinessRevenue and negative-control NonRevenueCashIn records
    // without querying the database or relying on category display names.
    internal static readonly Guid A1BusinessRevenueIncomeCategory = DerivedId(A1, 0x61);
    internal static readonly Guid A1NonRevenueIncomeCategory = DerivedId(A1, 0x62);
    internal static readonly Guid A2BusinessRevenueIncomeCategory = DerivedId(A2, 0x61);
    internal static readonly Guid A2NonRevenueIncomeCategory = DerivedId(A2, 0x62);
    internal static readonly Guid B1BusinessRevenueIncomeCategory = DerivedId(B1, 0x61);
    internal static readonly Guid B1NonRevenueIncomeCategory = DerivedId(B1, 0x62);
    internal static readonly Guid B2BusinessRevenueIncomeCategory = DerivedId(B2, 0x61);
    internal static readonly Guid B2NonRevenueIncomeCategory = DerivedId(B2, 0x62);
    internal static readonly Guid C1BusinessRevenueIncomeCategory = DerivedId(C1, 0x61);
    internal static readonly Guid C1NonRevenueIncomeCategory = DerivedId(C1, 0x62);

    internal static readonly Guid Pot = Guid.Parse("a1000000-0000-4000-8000-000000000301");
    internal static readonly Guid Meal = Guid.Parse("a1000000-0000-4000-8000-000000000302");
    internal static readonly Guid Rice = Guid.Parse("a1000000-0000-4000-8000-000000000401");
    internal static readonly Guid Chicken = Guid.Parse("a1000000-0000-4000-8000-000000000402");
    internal static readonly Guid B1Product = Guid.Parse("b1000000-0000-4000-8000-000000000301");
    internal static readonly Guid B2Product = Guid.Parse("b1000000-0000-4000-8000-000000000302");
    internal static readonly Guid C1Product = Guid.Parse("c1000000-0000-4000-8000-000000000301");

    internal static readonly Guid A1Cash = Guid.Parse("a1000000-0000-4000-8000-000000000501");
    internal static readonly Guid A1Bank = Guid.Parse("a1000000-0000-4000-8000-000000000502");
    internal static readonly Guid A2Cash = Guid.Parse("a1000000-0000-4000-8000-000000000503");
    internal static readonly Guid A2OldBank = Guid.Parse("a1000000-0000-4000-8000-000000000504");
    internal static readonly Guid A2NewBank = Guid.Parse("a1000000-0000-4000-8000-000000000505");
    internal static readonly Guid B1Cash = Guid.Parse("b1000000-0000-4000-8000-000000000501");
    internal static readonly Guid B2Cash = Guid.Parse("b1000000-0000-4000-8000-000000000502");
    internal static readonly Guid C1Cash = Guid.Parse("c1000000-0000-4000-8000-000000000501");
    internal static readonly Guid C1RefundBank = Guid.Parse("c1000000-0000-4000-8000-000000000502");

    internal static readonly Guid[] Owners = [OwnerA, OwnerB, OwnerC];
    internal static readonly Guid[] Businesses = [A1, A2, B1, B2, C1];
    internal static readonly Guid[] Products = [Pot, Meal, B1Product, B2Product, C1Product];
    internal static readonly Guid[] Accounts =
        [A1Cash, A1Bank, A2Cash, A2OldBank, A2NewBank, B1Cash, B2Cash, C1Cash, C1RefundBank];
    internal static readonly Guid[] IncomeCategories =
    [
        A1BusinessRevenueIncomeCategory, A1NonRevenueIncomeCategory,
        A2BusinessRevenueIncomeCategory, A2NonRevenueIncomeCategory,
        B1BusinessRevenueIncomeCategory, B1NonRevenueIncomeCategory,
        B2BusinessRevenueIncomeCategory, B2NonRevenueIncomeCategory,
        C1BusinessRevenueIncomeCategory, C1NonRevenueIncomeCategory
    ];

    private static Guid DerivedId(Guid source, byte suffix)
    {
        var bytes = source.ToByteArray();
        bytes[^2] = suffix;
        return new Guid(bytes);
    }
}
