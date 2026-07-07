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

        if (!currentUser.IsSuperCompany && currentUser.BusinessUnitId.HasValue)
        {
            // Pinned to one specific business unit on their own account - no navbar picker for them.
            // That unit if it still belongs to their company, else "whole company".
            return units.Any(u => u.BusinessUnitId == currentUser.BusinessUnitId.Value)
                ? currentUser.BusinessUnitId
                : null;
        }

        // SuperAdmin, or a company-scoped user with no fixed business unit of their own: whatever
        // was last picked from the navbar dropdown this session, or "whole company" until they pick.
        var selected = _httpContextAccessor.HttpContext?.Session.GetInt32(CompanyContextSessionKeys.SelectedBusinessUnitId);
        return selected.HasValue && units.Any(u => u.BusinessUnitId == selected.Value)
            ? selected
            : null;
    }

    public async Task SetSelectedBusinessUnitIdAsync(int? businessUnitId)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!currentUser.IsSuperCompany && currentUser.BusinessUnitId.HasValue)
        {
            // Pinned to one specific business unit on their own account - they don't get to change it.
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
