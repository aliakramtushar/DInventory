using System.Globalization;

namespace DInventory.Web.Helpers;

/// <summary>
/// This is a Bangladeshi business - every money amount in the UI is formatted in BDT (Taka), not the
/// server's default culture's currency (which would otherwise render "$" via decimal.ToString("C")).
/// Formats independent of server/OS culture settings so it looks the same everywhere it runs.
/// </summary>
public static class MoneyExtensions
{
    private const string CurrencySymbol = "৳";

    public static string ToMoney(this decimal value)
        => CurrencySymbol + value.ToString("N2", CultureInfo.InvariantCulture);

    public static string ToMoney(this decimal? value)
        => (value ?? 0m).ToMoney();
}
