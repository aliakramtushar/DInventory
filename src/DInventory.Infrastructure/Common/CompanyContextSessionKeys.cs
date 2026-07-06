namespace DInventory.Infrastructure.Common;

/// <summary>Shared session key names for CompanyContextService/BusinessUnitContextService, kept in
/// one place so they never drift out of sync (e.g. when clearing the business unit pick on a company
/// switch).</summary>
internal static class CompanyContextSessionKeys
{
    public const string SelectedCompanyId = "SelectedCompanyId";
    public const string SelectedBusinessUnitId = "SelectedBusinessUnitId";
}
