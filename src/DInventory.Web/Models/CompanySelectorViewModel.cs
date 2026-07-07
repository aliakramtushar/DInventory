using DInventory.Domain.Entities;

namespace DInventory.Web.Models;

public class CompanySelectorViewModel
{
    public List<Company> Companies { get; set; } = new();
    public int SelectedCompanyId { get; set; }

    /// <summary>Display name used when CanChange is false - the user's own company, shown but locked.</summary>
    public string SelectedCompanyName { get; set; } = string.Empty;

    /// <summary>True only for SuperAdmin (CompanyId 0). Every other user is pinned to their own
    /// company: the selector is still shown (so they can see which company they're in) but locked.</summary>
    public bool CanChange { get; set; }
}
