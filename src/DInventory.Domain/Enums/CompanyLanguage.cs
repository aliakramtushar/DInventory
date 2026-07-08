namespace DInventory.Domain.Enums;

/// <summary>Fixed set of languages a Company can be tagged with. Deliberately just a label on the
/// Company record - no localization/translation behavior is driven by this value anywhere else in
/// the app (yet). Stored as a TINYINT in dbo.Companies (0/1) so Dapper maps it natively with no
/// custom type handler needed.</summary>
public enum CompanyLanguage
{
    English = 0,
    Bangla = 1
}
