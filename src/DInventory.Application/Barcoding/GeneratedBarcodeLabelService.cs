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

    public Task<PagedResult<GeneratedBarcodeLabel>> GetPagedAsync(PagedRequest request) => _labelRepository.GetPagedAsync(request);

    public Task<GeneratedBarcodeLabel?> GetByBarcodeAsync(string barcode) => _labelRepository.GetByBarcodeAsync(barcode.Trim());

    public async Task<Result<GeneratedBarcodeLabel>> GenerateAsync(string? manualBarcode, string productName, string? brandName, string? sizeName, decimal? price, int? actingUserId)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return Result<GeneratedBarcodeLabel>.Failure("Product name is required for the label.");
        }

        var barcode = manualBarcode?.Trim();
        if (string.IsNullOrWhiteSpace(barcode))
        {
            barcode = await _barcodeNumberGenerator.GenerateNextAsync();
        }
        else if (await _labelRepository.BarcodeExistsAsync(barcode))
        {
            return Result<GeneratedBarcodeLabel>.Failure($"Barcode '{barcode}' has already been generated.");
        }

        var label = new GeneratedBarcodeLabel
        {
            Barcode = barcode,
            ProductName = productName.Trim(),
            BrandName = brandName,
            SizeName = sizeName,
            Price = price,
            IsLinked = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId
        };

        await _labelRepository.CreateAsync(label);
        return Result<GeneratedBarcodeLabel>.Success(label);
    }
}
