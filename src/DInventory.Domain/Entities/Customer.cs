namespace DInventory.Domain.Entities;

public class Customer
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>The customer's mobile number - stored as "Phone" (original column name) but
    /// treated as the mobile number everywhere in the UI.</summary>
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    // Populated via joins/aggregation, not DB columns
    public int? OrderCount { get; set; }
    public decimal? TotalSpend { get; set; }

    /// <summary>SUM(Points) across dbo.LoyaltyTransactions for this customer - the real,
    /// persisted-ledger loyalty balance (earn on sale / redeem at checkout / manual adjust),
    /// replacing the earlier "spend / 100" estimate.</summary>
    public int LoyaltyPointsBalance { get; set; }
}
