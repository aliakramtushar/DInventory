namespace DInventory.Domain.Entities;

/// <summary>Single-row shop-wide loyalty configuration: how many points a customer earns per
/// amount spent, and what one point is worth when redeemed. Both are owner-editable; earning and
/// redeeming are effectively off (rate 0) until real numbers are set.</summary>
public class LoyaltySettings
{
    public int LoyaltySettingsId { get; set; }
    public bool IsEnabled { get; set; }

    /// <summary>Earn 1 point per this many currency units of a sale's NetAmount. 0 = no points
    /// are earned.</summary>
    public decimal PointsPerAmountSpent { get; set; }

    /// <summary>Money value of 1 point when redeemed. 0 = points can't be redeemed for value.</summary>
    public decimal PointValueOnRedeem { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}
