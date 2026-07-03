using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Customers;

public class LoyaltyService : ILoyaltyService
{
    private readonly ILoyaltyRepository _loyaltyRepository;

    public LoyaltyService(ILoyaltyRepository loyaltyRepository)
    {
        _loyaltyRepository = loyaltyRepository;
    }

    public Task<LoyaltySettings> GetSettingsAsync() => _loyaltyRepository.GetSettingsAsync();

    public async Task<Result> UpdateSettingsAsync(LoyaltySettings settings, int? actingUserId)
    {
        if (settings.PointsPerAmountSpent < 0 || settings.PointValueOnRedeem < 0)
        {
            return Result.Failure("Loyalty rates can't be negative.");
        }

        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedBy = actingUserId;

        var ok = await _loyaltyRepository.UpdateSettingsAsync(settings);
        return ok ? Result.Success() : Result.Failure("Unable to update loyalty settings.");
    }

    public Task<int> GetBalanceAsync(int customerId) => _loyaltyRepository.GetBalanceAsync(customerId);

    public Task<IEnumerable<LoyaltyTransaction>> GetHistoryAsync(int customerId) => _loyaltyRepository.GetHistoryAsync(customerId);

    public int ComputeEarnedPoints(decimal netAmount, LoyaltySettings settings)
    {
        if (!settings.IsEnabled || settings.PointsPerAmountSpent <= 0 || netAmount <= 0)
        {
            return 0;
        }

        return (int)Math.Floor(netAmount / settings.PointsPerAmountSpent);
    }

    public decimal ComputeRedeemValue(int points, LoyaltySettings settings)
    {
        if (!settings.IsEnabled || settings.PointValueOnRedeem <= 0 || points <= 0)
        {
            return 0;
        }

        return Math.Round(points * settings.PointValueOnRedeem, 2);
    }

    public async Task EarnAsync(int customerId, int points, string referenceType, int? referenceId, string? remarks, int? actingUserId)
    {
        if (points <= 0)
        {
            return;
        }

        await _loyaltyRepository.CreateTransactionAsync(new LoyaltyTransaction
        {
            CustomerId = customerId,
            TransactionType = "EARN",
            Points = points,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Remarks = remarks,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId
        });
    }

    public async Task<Result> RedeemAsync(int customerId, int points, string referenceType, int? referenceId, string? remarks, int? actingUserId)
    {
        if (points <= 0)
        {
            return Result.Failure("Redeem quantity must be greater than zero.");
        }

        var balance = await _loyaltyRepository.GetBalanceAsync(customerId);
        if (points > balance)
        {
            return Result.Failure($"Customer only has {balance} point(s) available.");
        }

        await _loyaltyRepository.CreateTransactionAsync(new LoyaltyTransaction
        {
            CustomerId = customerId,
            TransactionType = "REDEEM",
            Points = -points,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Remarks = remarks,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId
        });

        return Result.Success();
    }

    public async Task<Result> AdjustAsync(int customerId, int points, string? remarks, int? actingUserId, bool enforceBalanceFloor = true)
    {
        if (points == 0)
        {
            return Result.Failure("Adjustment must be a non-zero number of points.");
        }

        if (enforceBalanceFloor && points < 0)
        {
            var balance = await _loyaltyRepository.GetBalanceAsync(customerId);
            if (-points > balance)
            {
                return Result.Failure($"Customer only has {balance} point(s) - can't subtract {-points}.");
            }
        }

        await _loyaltyRepository.CreateTransactionAsync(new LoyaltyTransaction
        {
            CustomerId = customerId,
            TransactionType = "ADJUST",
            Points = points,
            ReferenceType = "MANUAL",
            Remarks = remarks,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId
        });

        return Result.Success();
    }
}
