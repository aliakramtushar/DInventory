using System.Linq;
using DInventory.Application.Common.Interfaces;
using DInventory.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace DInventory.Infrastructure.Common;

public class CompanyContextService : ICompanyContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyRepository _companyRepository;

    public CompanyContextService(
        IHttpContextAccessor httpContextAccessor,
        ICurrentUserService currentUserService,
        ICompanyRepository companyRepository)
    {
        _httpContextAccessor = httpContextAccessor;
        _currentUserService = currentUserService;
        _companyRepository = companyRepository;
    }

    public bool IsSuperCompany => _currentUserService.GetCurrentUser().IsSuperCompany;

    public int GetEffectiveCompanyId()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany)
        {
            return currentUser.CompanyId;
        }

        // SuperAdmin: whatever they last picked from the navbar dropdown this session. Nothing
        // picked yet (new login) defaults to 0 - "All Companies".
        return _httpContextAccessor.HttpContext?.Session.GetInt32(CompanyContextSessionKeys.SelectedCompanyId) ?? 0;
    }

    public void SetSelectedCompanyId(int companyId)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany)
        {
            // Not a superuser - they don't get to change which company they operate on.
            return;
        }

        var session = _httpContextAccessor.HttpContext?.Session;
        session?.SetInt32(CompanyContextSessionKeys.SelectedCompanyId, companyId);

        // The previously-picked business unit almost certainly doesn't belong to the newly picked
        // company - drop it so the Business Unit navbar picker recomputes cleanly from scratch
        // (auto-select-if-one / disabled-if-none / "whole company" until re-picked).
        session?.Remove(CompanyContextSessionKeys.SelectedBusinessUnitId);
    }

    public async Task<IEnumerable<Company>> GetSelectableCompaniesAsync()
    {
        var companies = await _companyRepository.GetAllAsync(onlyActive: true);
        return companies.Where(c => c.CompanyId != 0).OrderBy(c => c.CompanyName).ToList();
    }
}
