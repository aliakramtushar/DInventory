namespace DInventory.Web.Common;

/// <summary>Shared guard for every "upload a product/content image" endpoint. Before this, upload
/// handlers only checked file.Length > 0 and trusted whatever extension the browser sent, which let
/// anyone post an arbitrarily large file (disk-fill DoS) or an arbitrary file type disguised with an
/// image-like name into wwwroot/uploads. Every caller should validate with this before saving.</summary>
public static class ImageUploadValidator
{
    /// <summary>Generous enough for a real product/banner photo without leaving the upload folder
    /// open to being filled up by a handful of oversized requests.</summary>
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp"
    };

    public static bool TryValidate(IFormFile file, out string? error)
    {
        error = null;

        if (file.Length <= 0)
        {
            error = "The selected file is empty.";
            return false;
        }

        if (file.Length > MaxFileSizeBytes)
        {
            error = "Image is too large - please upload a file under 5 MB.";
            return false;
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            error = "Unsupported file type - please upload a JPG, PNG, GIF, or WEBP image.";
            return false;
        }

        if (string.IsNullOrEmpty(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType))
        {
            error = "Unsupported file type - please upload a JPG, PNG, GIF, or WEBP image.";
            return false;
        }

        return true;
    }
}
