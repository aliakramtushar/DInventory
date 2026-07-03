using DInventory.Application.Common.Interfaces;
using DInventory.Application.Common.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DInventory.Infrastructure.Email;

public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailSettings> options, IHostEnvironment hostEnvironment, ILogger<SmtpEmailService> logger)
    {
        _settings = options.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.SmtpHost) || string.IsNullOrWhiteSpace(_settings.SmtpUser))
        {
            if (_settings.FallbackToLocalFile)
            {
                await WriteToLocalFileAsync(toEmail, subject, htmlBody, cancellationToken);
                return;
            }

            throw new InvalidOperationException("SMTP is not configured. Set Email:SmtpHost and Email:SmtpUser in appsettings.json.");
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, string.IsNullOrWhiteSpace(_settings.FromEmail) ? _settings.SmtpUser : _settings.FromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort,
                _settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, cancellationToken);
            await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPassword, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail}. Falling back to local file.", toEmail);
            if (_settings.FallbackToLocalFile)
            {
                await WriteToLocalFileAsync(toEmail, subject, htmlBody, cancellationToken);
            }
            else
            {
                throw;
            }
        }
    }

    private async Task WriteToLocalFileAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(_hostEnvironment.ContentRootPath, "App_Data", "outbox");
        Directory.CreateDirectory(folder);

        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}.html";
        var filePath = Path.Combine(folder, fileName);

        var content = $"<!-- To: {toEmail} | Subject: {subject} -->\n{htmlBody}";
        await File.WriteAllTextAsync(filePath, content, cancellationToken);

        _logger.LogInformation("SMTP not configured. Email to {ToEmail} with subject '{Subject}' saved to {FilePath}", toEmail, subject, filePath);
    }
}
