using DInventory.Domain.Entities;
using DInventory.Application.Common.Models;

namespace DInventory.Application.Customers;

public interface ILoyaltyService
{
    Task<LoyaltySettings> GetSettingsAsync();
    Task<Result> UpdateSettingsAsync(LoyaltySettings settings, int? actingUserId);
    Task<int> GetBalanceAsync(int customerId);
    Task<IEnumerable<LoyaltyTransaction>> GetHistoryAsync(int customerId);

    /// <summary>Points earned for spending this much, per the given settings snapshot. 0 if the
    /// program is off or the earn rate is unset.</summary>
    int ComputeEarnedPoints(decimal netAmount, LoyaltySettings settings);

    /// <summary>Money value of this many points, per the given settings snapshot. 0 if the
    /// redeem rate is unset.</summary>
    decimal ComputeRedeemValue(int points, LoyaltySettings settings);

    /// <summary>Records points earned (e.g. from a completed sale). Always succeeds - earning
    /// never has anything to validate against.</summary>
    Task EarnAsync(int customerId, int points, string referenceType, int? referenceId, string? remarks, int? actingUserId);

    /// <summary>Records points redeemed for value. Fails if the customer doesn't have enough
    /// balance.</summary>
    Task<Result> RedeemAsync(int customerId, int points, string referenceType, int? referenceId, string? remarks, int? actingUserId);

    /// <summary>Manual balance correction (positive or negative). When
    /// <paramref name="enforceBalanceFloor"/> is true (the default, used by the admin "Adjust
    /// Points" screen) a negative adjustment can't take the balance below zero. System-initiated
    /// reversals (e.g. cancelling a sale that had already earned/redeemed points) pass false,
    /// since the reversal must go through even if the customer's balance has since moved.</summary>
    Task<Result> AdjustAsync(int customerId, int points, string? remarks, int? actingUserId, bool enforceBalanceFloor = true);
}
