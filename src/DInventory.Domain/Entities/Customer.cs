namespace DInventory.Domain.Entities;

public class Customer
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    // Populated via joins/aggregation, not DB columns
    public int? OrderCount { get; set; }
    public decimal? TotalSpend { get; set; }

    /// <summary>Simple static formula (1 point per ৳100 spent, completed orders only) - not a
    /// persisted ledger. Good enough for a Phase 1 "at a glance" loyalty indicator.</summary>
    public int LoyaltyPoints => (int)((TotalSpend ?? 0m) / 100m);
}
