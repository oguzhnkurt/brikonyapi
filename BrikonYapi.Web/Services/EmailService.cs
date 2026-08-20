using BrikonYapi.Web.Data.Entities;
using System.Net;
using System.Net.Mail;

namespace BrikonYapi.Web.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_config["Smtp:Host"]);

        /// <summary>Genel amaçlı HTML e-posta gönderimi. Smtp:Host boşsa sessizce atlanır (false döner) —
        /// çağıran taraf bu durumda hata fırlatmamalı, sadece göndermemiş sayılmalıdır.</summary>
        public async Task<(bool Success, string? Error)> SendAsync(string toAddress, string toName, string subject, string htmlBody)
        {
            var host = _config["Smtp:Host"];
            if (string.IsNullOrWhiteSpace(host))
                return (false, "E-posta sağlayıcısı yapılandırılmamış.");

            var port      = _config.GetValue<int>("Smtp:Port", 587);
            var username  = _config["Smtp:Username"] ?? "";
            var password  = _config["Smtp:Password"] ?? "";
            var fromAddr  = _config["Smtp:From"] ?? username;
            var enableSsl = _config.GetValue<bool>("Smtp:EnableSsl", true);

            try
            {
                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl   = enableSsl
                };

                var mail = new MailMessage
                {
                    From       = new MailAddress(fromAddr),
                    Subject    = subject,
                    Body       = htmlBody,
                    IsBodyHtml = true
                };
                mail.To.Add(string.IsNullOrWhiteSpace(toName) ? new MailAddress(toAddress) : new MailAddress(toAddress, toName));

                await client.SendMailAsync(mail);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "E-posta gönderilemedi: {To}", toAddress);
                return (false, "E-posta gönderilemedi.");
            }
        }

        public async Task SendContactNotificationAsync(ContactMessage msg)
        {
            var host = _config["Smtp:Host"];
            if (string.IsNullOrWhiteSpace(host)) return;

            var port      = _config.GetValue<int>("Smtp:Port", 587);
            var username  = _config["Smtp:Username"] ?? "";
            var password  = _config["Smtp:Password"] ?? "";
            var fromAddr  = _config["Smtp:From"] ?? username;
            var toAddr    = _config["Smtp:NotifyEmail"] ?? "info@brikonyapi.com";
            var enableSsl = _config.GetValue<bool>("Smtp:EnableSsl", true);

            try
            {
                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl   = enableSsl
                };

                var body = $"""
                    <html><body style="font-family:Arial,sans-serif;color:#222;">
                    <h2 style="color:#0d6efd;">Yeni İletişim Mesajı — brikonyapi.tech</h2>
                    <table style="border-collapse:collapse;width:100%;max-width:560px;">
                      <tr><td style="padding:6px 12px;background:#f5f5f5;font-weight:600;width:130px;">Ad Soyad</td><td style="padding:6px 12px;">{System.Web.HttpUtility.HtmlEncode(msg.FullName)}</td></tr>
                      <tr><td style="padding:6px 12px;background:#f5f5f5;font-weight:600;">E-posta</td><td style="padding:6px 12px;"><a href="mailto:{msg.Email}">{System.Web.HttpUtility.HtmlEncode(msg.Email)}</a></td></tr>
                      <tr><td style="padding:6px 12px;background:#f5f5f5;font-weight:600;">Telefon</td><td style="padding:6px 12px;">{System.Web.HttpUtility.HtmlEncode(msg.Phone ?? "—")}</td></tr>
                      <tr><td style="padding:6px 12px;background:#f5f5f5;font-weight:600;">Konu</td><td style="padding:6px 12px;">{System.Web.HttpUtility.HtmlEncode(msg.Subject ?? "—")}</td></tr>
                      <tr><td style="padding:6px 12px;background:#f5f5f5;font-weight:600;vertical-align:top;">Mesaj</td><td style="padding:6px 12px;">{System.Web.HttpUtility.HtmlEncode(msg.Message).Replace("\n", "<br/>")}</td></tr>
                      <tr><td style="padding:6px 12px;background:#f5f5f5;font-weight:600;">Tarih</td><td style="padding:6px 12px;">{msg.CreatedAt:dd.MM.yyyy HH:mm}</td></tr>
                    </table>
                    <p style="margin-top:16px;font-size:.85rem;color:#888;">Bu mesajı <a href="https://brikonyapi.tech/Admin/Messages">Admin Panel → Mesajlar</a> üzerinden de görebilirsiniz.</p>
                    </body></html>
                    """;

                var mail = new MailMessage(fromAddr, toAddr)
                {
                    Subject    = $"[brikonyapi.tech] Yeni mesaj: {msg.FullName}",
                    Body       = body,
                    IsBodyHtml = true
                };

                // Gönderen kişiye cevap verebilmek için Reply-To
                mail.ReplyToList.Add(new MailAddress(msg.Email, msg.FullName));

                await client.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "E-posta gönderilemedi: {To}", toAddr);
            }
        }
    }
}
