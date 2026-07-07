using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Tenancy;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _companyRepository;

    public CompanyService(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public Task<Company?> GetByIdAsync(int companyId) => _companyRepository.GetByIdAsync(companyId);

    public Task<IEnumerable<Company>> GetAllAsync(string? search = null, bool onlyActive = false)
        => _companyRepository.GetAllAsync(search, onlyActive);

    public Task<PagedResult<Company>> GetPagedAsync(PagedRequest request, bool onlyActive = false)
        => _companyRepository.GetPagedAsync(request, onlyActive);

    public async Task<Result<int>> CreateAsync(Company company, int? actingUserId)
    {
        if (string.IsNullOrWhiteSpace(company.CompanyName))
        {
            return Result<int>.Failure("Company name is required.");
        }

        if (await _companyRepository.NameExistsAsync(company.CompanyName))
        {
            return Result<int>.Failure("A company with this name already exists.");
        }

        var shortNameResult = NormalizeShortName(company.ShortName);
        if (!shortNameResult.Succeeded)
        {
            return Result<int>.Failure(shortNameResult.Error ?? "Invalid short name.");
        }

        company.ShortName = shortNameResult.Data!;
        if (await _companyRepository.ShortNameExistsAsync(company.ShortName))
        {
            return Result<int>.Failure($"Short name '{company.ShortName}' is already used by another company.");
        }

        company.CreatedBy = actingUserId;
        company.CreatedAt = DateTime.UtcNow;
        company.IsActive = true;

        try
        {
            var id = await _companyRepository.CreateAsync(company);
            return Result<int>.Success(id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Unable to save company: {ex.Message}");
        }
    }

    public async Task<Result> UpdateAsync(Company company, int? actingUserId)
    {
        var existing = await _companyRepository.GetByIdAsync(company.CompanyId);
        if (existing is null)
        {
            return Result.Failure("Company not found.");
        }

        if (await _companyRepository.NameExistsAsync(company.CompanyName, company.CompanyId))
        {
            return Result.Failure("A company with this name already exists.");
        }

        var shortNameResult = NormalizeShortName(company.ShortName);
        if (!shortNameResult.Succeeded)
        {
            return Result.Failure(shortNameResult.Error ?? "Invalid short name.");
        }

        if (await _companyRepository.ShortNameExistsAsync(shortNameResult.Data!, company.CompanyId))
        {
            return Result.Failure($"Short name '{shortNameResult.Data}' is already used by another company.");
        }

        existing.CompanyName = company.CompanyName;
        existing.ShortName = shortNameResult.Data!;
        existing.Phone = company.Phone;
        existing.Email = company.Email;
        existing.Address = company.Address;
        existing.HasECommerce = company.HasECommerce;
        existing.IsActive = company.IsActive;
        existing.UpdatedBy = actingUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        try
        {
            var ok = await _companyRepository.UpdateAsync(existing);
            return ok ? Result.Success() : Result.Failure("Unable to update company.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to update company: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(int companyId)
    {
        if (companyId == 0)
        {
            return Result.Failure("The built-in superuser company cannot be deleted.");
        }

        if (await _companyRepository.HasDependentDataAsync(companyId))
        {
            return Result.Failure("Cannot delete a company that still has users, business units, or catalog data. Deactivate it instead.");
        }

        try
        {
            var ok = await _companyRepository.DeleteAsync(companyId);
            return ok ? Result.Success() : Result.Failure("Unable to delete company.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to delete company: {ex.Message}");
        }
    }

    /// <summary>Uppercases and validates the short name used as this company's barcode prefix -
    /// mandatory, letters/digits only, at least 3 characters, so every generated barcode is
    /// unambiguous about which tenant it belongs to.</summary>
    private static Result<string> NormalizeShortName(string? shortName)
    {
        var normalized = (shortName ?? string.Empty).Trim().ToUpperInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, "^[A-Z0-9]{3,15}$"))
        {
            return Result<string>.Failure("Short name is required: at least 3 characters, letters/numbers only (it will be shown in capitals).");
        }

        return Result<string>.Success(normalized);
    }
}
