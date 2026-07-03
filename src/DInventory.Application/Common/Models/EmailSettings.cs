namespace DInventory.Application.Common.Models;

public class EmailSettings
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "DInventory";
    /// <summary>When true and SMTP settings are not configured, emails are written to a local file instead of sent.</summary>
    public bool FallbackToLocalFile { get; set; } = true;
}
