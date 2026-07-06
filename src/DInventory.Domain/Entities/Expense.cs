namespace DInventory.Domain.Entities;

public class Expense
{
    public int ExpenseId { get; set; }
    public DateTime ExpenseDate { get; set; }

    /// <summary>One of a fixed set: Shop Rent, Electricity, Salary, Internet, Packaging, Marketing,
    /// Courier, Other (enforced by a CHECK constraint in the DB, kept as a plain string - no separate
    /// lookup table for something this small and fixed).</summary>
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// <summary>Which company this record belongs to. 0 = the built-in superuser company
    /// (bypasses company filtering everywhere); every other value is a real tenant.</summary>
    public int CompanyId { get; set; }
    public int? BusinessUnitId { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }

    // Populated via joins, not DB columns
    public string? CreatedByName { get; set; }
}
