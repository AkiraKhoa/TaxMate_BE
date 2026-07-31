namespace TaxMate.Service.Services;

public static class S2aHkdTaxCalculator
{
    public static decimal RoundVnd(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero);

    public static (decimal VatTax, decimal PitTax) CalculateGroupTaxes(
        decimal subtotal,
        decimal vatRate,
        decimal pitRate)
    {
        var vatTax = RoundVnd(subtotal * vatRate / 100m);
        var pitTax = RoundVnd(subtotal * pitRate / 100m);
        return (vatTax, pitTax);
    }
}
