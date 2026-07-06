using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Purchasing;

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IPurchaseRepository _purchaseRepository;

    public SupplierService(ISupplierRepository supplierRepository, IPurchaseRepository purchaseRepository)
    {
        _supplierRepository = supplierRepository;
        _purchaseRepository = purchaseRepository;
    }

    public Task<Supplier?> GetByIdAsync(int supplierId) => _supplierRepository.GetByIdAsync(supplierId);

    public Task<Supplier?> GetByIdWithDueAsync(int supplierId) => _supplierRepository.GetByIdWithDueAsync(supplierId);

    public Task<IEnumerable<Supplier>> GetAllAsync(int companyId = 0, bool onlyActive = false) => _supplierRepository.GetAllAsync(companyId, onlyActive);

    public Task<PagedResult<Supplier>> GetPagedAsync(PagedRequest request, int companyId, bool onlyActive = false)
        => _supplierRepository.GetPagedAsync(request, companyId, onlyActive);

    public async Task<Result<int>> CreateAsync(Supplier supplier, int? actingUserId)
    {
        if (string.IsNullOrWhiteSpace(supplier.SupplierName))
        {
            return Result<int>.Failure("Supplier name is required.");
        }

        if (await _supplierRepository.NameExistsAsync(supplier.SupplierName))
        {
            return Result<int>.Failure("A supplier with this name already exists.");
        }

        supplier.CreatedBy = actingUserId;
        supplier.CreatedAt = DateTime.UtcNow;
        supplier.IsActive = true;

        try
        {
            var id = await _supplierRepository.CreateAsync(supplier);
            return Result<int>.Success(id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Unable to save supplier: {ex.Message}");
        }
    }

    public async Task<Result> UpdateAsync(Supplier supplier, int? actingUserId)
    {
        var existing = await _supplierRepository.GetByIdAsync(supplier.SupplierId);
        if (existing is null)
        {
            return Result.Failure("Supplier not found.");
        }

        if (await _supplierRepository.NameExistsAsync(supplier.SupplierName, supplier.SupplierId))
        {
            return Result.Failure("A supplier with this name already exists.");
        }

        existing.SupplierName = supplier.SupplierName;
        existing.Phone = supplier.Phone;
        existing.Address = supplier.Address;
        existing.Remarks = supplier.Remarks;
        existing.IsActive = supplier.IsActive;
        existing.UpdatedBy = actingUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        try
        {
            var ok = await _supplierRepository.UpdateAsync(existing);
            return ok ? Result.Success() : Result.Failure("Unable to update supplier.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to update supplier: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(int supplierId)
    {
        if (await _supplierRepository.HasPurchasesAsync(supplierId))
        {
            return Result.Failure("Cannot delete a supplier that already has purchase history. Consider deactivating it instead.");
        }

        try
        {
            var ok = await _supplierRepository.DeleteAsync(supplierId);
            return ok ? Result.Success() : Result.Failure("Unable to delete supplier.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unable to delete supplier: {ex.Message}");
        }
    }

    public Task<IEnumerable<Purchase>> GetPurchaseHistoryAsync(int supplierId) => _purchaseRepository.GetForSupplierAsync(supplierId);
}
