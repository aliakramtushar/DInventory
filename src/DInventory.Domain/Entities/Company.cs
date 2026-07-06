namespace DInventory.Domain.Entities;

/// <summary>A tenant using this installation. CompanyId 0 is the built-in "Super Admin / All
/// Companies" row - a user whose Users.CompanyId is 0 is a superuser and sees data across every
/// real company (see the WHERE (@companyId = 0 OR t.CompanyId = @companyId) pattern used by every
/// company-scoped repository). Every other company is a normal tenant whose users only ever see
/// their own company's data.</summary>
public class Company
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Short, all-caps mnemonic (min 3 chars) used as the barcode prefix for every code
    /// generated under this company, e.g. "ABC" -> ABC000000123. Must be unique across companies so
    /// generated barcodes never collide between tenants.</summary>
    public string ShortName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
}
