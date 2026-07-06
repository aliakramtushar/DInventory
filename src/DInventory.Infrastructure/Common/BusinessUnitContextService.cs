using System.Linq;
using DInventory.Application.Common.Interfaces;
using DInventory.Application.Tenancy;
using DInventory.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace DInventory.Infrastructure.Common;

public class BusinessUnitContextService : IBusinessUnitContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyContextService _companyContextService;
    private readonly IBusinessUnitService _businessUnitService;

    public BusinessUnitContextService(
        IHttpContextAccessor httpContextAccessor,
        ICurrentUserService currentUserService,
        ICompanyContextService companyContextService,
        IBusinessUnitService businessUnitService)
    {
        _httpContextAccessor = httpContextAccessor;
        _currentUserService = currentUserService;
        _companyContextService = companyContextService;
        _businessUnitService = businessUnitService;
    }

    public async Task<IReadOnlyList<BusinessUnit>> GetSelectableBusinessUnitsAsync()
    {
        var effectiveCompanyId = _companyContextService.GetEffectiveCompanyId();
        if (effectiveCompanyId <= 0)
        {
            return Array.Empty<BusinessUnit>();
        }

        var units = await _businessUnitService.GetAllAsync(effectiveCompanyId, onlyActive: true);
        return units.OrderBy(u => u.BusinessUnitName).ToList();
    }

    public async Task<int?> GetEffectiveBusinessUnitIdAsync()
    {
        var units = await GetSelectableBusinessUnitsAsync();
        if (units.Count == 0)
        {
            return null;
        }

        if (units.Count == 1)
        {
            return units[0].BusinessUnitId;
        }

        var currentUser = _currentUserService.GetCurrentUser();

        if (!currentUser.IsSuperCompany)
        {
            // Not a superuser - no navbar picker for them. Their own assigned business unit if it
            // still belongs to their company, else "whole company".
            return currentUser.BusinessUnitId.HasValue && units.Any(u => u.BusinessUnitId == currentUser.BusinessUnitId.Value)
                ? currentUser.BusinessUnitId
                : null;
        }

        var selected = _httpContextAccessor.HttpContext?.Session.GetInt32(CompanyContextSessionKeys.SelectedBusinessUnitId);
        return selected.HasValue && units.Any(u => u.BusinessUnitId == selected.Value)
            ? selected
            : null; // "whole company" until SuperAdmin explicitly picks one from the navbar
    }

    public async Task SetSelectedBusinessUnitIdAsync(int? businessUnitId)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany)
        {
            // Not a superuser - they don't get to change which business unit they operate on.
            return;
        }

        var session = _httpContextAccessor.HttpContext?.Session;
        if (session is null)
        {
            return;
        }

        if (businessUnitId is null)
        {
            session.Remove(CompanyContextSessionKeys.SelectedBusinessUnitId);
            return;
        }

        var units = await GetSelectableBusinessUnitsAsync();
        if (units.Any(u => u.BusinessUnitId == businessUnitId.Value))
        {
            session.SetInt32(CompanyContextSessionKeys.SelectedBusinessUnitId, businessUnitId.Value);
        }
    }
}
