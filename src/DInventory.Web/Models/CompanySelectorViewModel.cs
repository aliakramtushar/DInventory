using DInventory.Domain.Entities;

namespace DInventory.Web.Models;

public class CompanySelectorViewModel
{
    public List<Company> Companies { get; set; } = new();
    public int SelectedCompanyId { get; set; }
}
