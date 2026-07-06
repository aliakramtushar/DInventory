using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using DInventory.Domain.Entities;

namespace DInventory.Application.Barcoding;

public class GeneratedBarcodeLabelService : IGeneratedBarcodeLabelService
{
    private readonly IGeneratedBarcodeLabelRepository _labelRepository;
    private readonly IBarcodeNumberGenerator _barcodeNumberGenerator;

    public GeneratedBarcodeLabelService(IGeneratedBarcodeLabelRepository labelRepository, IBarcodeNumberGenerator barcodeNumberGenerator)
    {
        _labelRepository = labelRepository;
        _barcodeNumberGenerator = barcodeNumberGenerator;
    }

    public Task<PagedResult<GeneratedBarcodeLabel>> GetPagedAsync(PagedRequest request, int companyId) => _labelRepository.GetPagedAsync(request, companyId);

    public Task<GeneratedBarcodeLabel?> GetByBarcodeAsync(string barcode) => _labelRepository.GetByBarcodeAsync(barcode.Trim());

    // Keep printed labels legible and scanner-friendly - module width of 0 (invisible bars) or a
    // multi-page-tall label are both user-input mistakes, not real print sizes, so we clamp rather
    // than reject the request outright.
    private const int MinBarcodeWidth = 1;
    private const int MaxBarcodeWidth = 6;
    private const int MinBarcodeHeight = 20;
    private const int MaxBarcodeHeight = 150;

    public async Task<Result<GeneratedBarcodeLabel>> GenerateAsync(int companyId, int? businessUnitId, string? manualBarcode, string productName, string? brandName, string? sizeName, string? companyName, decimal? price, int? barcodeWidth, int? barcodeHeight, int? actingUserId)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return Result<GeneratedBarcodeLabel>.Failure("Product name is required for the label.");
        }

        var barcode = manualBarcode?.Trim();
        if (string.IsNullOrWhiteSpace(barcode))
        {
            barcode = await _barcodeNumberGenerator.GenerateNextAsync(companyId);
        }
        else if (await _labelRepository.BarcodeExistsAsync(barcode))
        {
            return Result<GeneratedBarcodeLabel>.Failure($"Barcode '{barcode}' has already been generated.");
        }

        var width = Math.Clamp(barcodeWidth ?? 2, MinBarcodeWidth, MaxBarcodeWidth);
        var height = Math.Clamp(barcodeHeight ?? 50, MinBarcodeHeight, MaxBarcodeHeight);

        var label = new GeneratedBarcodeLabel
        {
            CompanyId = companyId,
            BusinessUnitId = businessUnitId,
            Barcode = barcode,
            ProductName = productName.Trim(),
            BrandName = brandName,
            SizeName = sizeName,
            CompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim(),
            Price = price,
            BarcodeWidth = width,
            BarcodeHeight = height,
            IsLinked = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId
        };

        await _labelRepository.CreateAsync(label);
        return Result<GeneratedBarcodeLabel>.Success(label);
    }
}
