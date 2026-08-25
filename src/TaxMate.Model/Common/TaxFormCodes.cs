namespace TaxMate.Model.Common;

public static class TaxFormCodes
{
    public const string Form01Cnkd = "01/CNKD";
    public const string Form01TknCnkd = "01/TKN-CNKD";
    public const string Form02CnkdTncnQtt = "02/CNKD-TNCN-QTT";

    public static readonly IReadOnlyCollection<string> All =
    [
        Form01Cnkd,
        Form01TknCnkd,
        Form02CnkdTncnQtt
    ];
}
