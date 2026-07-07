namespace DInventory.Domain.Entities;

/// <summary>Child-level expense category - must always belong to a parent
/// [[ExpenseCategory]] (enforced server-side, mirroring how Subcategory must belong to a
/// Category for Products). Optional on an individual Expense - a company can record expenses at
/// just the category level if it doesn't need finer detail.</summary>
public class ExpenseSubcategory
{
    public int ExpenseSubcategoryId { get; set; }
    public int ExpenseCategoryId { get; set; }
    public string ExpenseSubcategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Which company this record belongs to. 0 = the built-in superuser company
    /// (bypasses company filtering everywhere); every other value is a real tenant.</summary>
    public int CompanyId { get; set; }
    public int? BusinessUnitId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    // Populated via join, not a DB column
    public string? ExpenseCategoryName { get; set; }
}
