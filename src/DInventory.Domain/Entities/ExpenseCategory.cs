namespace DInventory.Domain.Entities;

/// <summary>Parent-level, company-manageable expense category (e.g. "Utilities", "Payroll",
/// "Marketing") - replaces the old fixed 8-value string list on Expense.Category so each company
/// can define its own chart of expense categories, the same way Category/Subcategory work for
/// Products. See [[ExpenseSubcategory]] for the child level.</summary>
public class ExpenseCategory
{
    public int ExpenseCategoryId { get; set; }
    public string ExpenseCategoryName { get; set; } = string.Empty;
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
}
