namespace DInventory.Domain.Entities;

public class LoyaltyTransaction
{
    public int LoyaltyTransactionId { get; set; }
    public int CustomerId { get; set; }

    /// <summary>EARN, REDEEM, or ADJUST.</summary>
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>Positive for EARN and positive ADJUST; negative for REDEEM and negative ADJUST.
    /// A customer's balance is always SUM(Points) across their transactions.</summary>
    public int Points { get; set; }

    /// <summary>SALE or MANUAL.</summary>
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }

    // Populated via joins, not a DB column
    public string? CreatedByName { get; set; }
}
