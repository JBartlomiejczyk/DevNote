using System.Net;
using System.Net.Mail;
using DevNote.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DevNote.Services;

public class SmtpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        await SendEmailAsync(email, "Potwierdź adres email — DevNote",
            $"<p>Kliknij poniższy link, aby potwierdzić swój adres email:</p><p><a href=\"{confirmationLink}\">Potwierdź email</a></p>");
    }

    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        await SendEmailAsync(email, "Resetowanie hasła — DevNote",
            $"<p>Kliknij poniższy link, aby zresetować hasło:</p><p><a href=\"{resetLink}\">Zresetuj hasło</a></p><p>Jeśli nie prosiłeś o reset hasła, zignoruj tę wiadomość.</p>");
    }

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        await SendEmailAsync(email, "Kod resetowania hasła — DevNote",
            $"<p>Twój kod resetowania hasła: <strong>{resetCode}</strong></p>");
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            _logger.LogWarning("SMTP not configured. Email to {Email} with subject '{Subject}' not sent. Body: {Body}",
                toEmail, subject, htmlBody);
            return;
        }

        using var message = new MailMessage();
        message.From = new MailAddress(_options.FromAddress, _options.FromName);
        message.To.Add(new MailAddress(toEmail));
        message.Subject = subject;
        message.Body = htmlBody;
        message.IsBodyHtml = true;

        using var client = new SmtpClient(_options.Host, _options.Port);
        client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        client.EnableSsl = true;

        await client.SendMailAsync(message);
        _logger.LogInformation("Email sent to {Email} with subject '{Subject}'", toEmail, subject);
    }
}
