namespace DInventory.Domain.Entities;

public class Expense
{
    public int ExpenseId { get; set; }
    public DateTime ExpenseDate { get; set; }

    /// <summary>Parent expense category - company-manageable master data (see
    /// [[ExpenseCategory]]), replacing the old fixed 8-value string list.</summary>
    public int ExpenseCategoryId { get; set; }
    /// <summary>Optional child category - if set, must belong to ExpenseCategoryId (enforced
    /// server-side in ExpenseService, same as Product's Category/Subcategory pair).</summary>
    public int? ExpenseSubcategoryId { get; set; }
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
    public string? ExpenseCategoryName { get; set; }
    public string? ExpenseSubcategoryName { get; set; }
}
