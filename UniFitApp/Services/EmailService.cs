using System.Net;
using System.Net.Mail;

namespace UniFitApp.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            // Берем настройки из твоего appsettings.json
            var host = _config["SmtpSettings:Host"];
            var port = int.Parse(_config["SmtpSettings:Port"]);
            var username = _config["SmtpSettings:Username"];
            var password = _config["SmtpSettings:Password"];

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(username, "Служба поддержки UniFitApp"),
                Subject = subject,
                Body = message,
                IsBodyHtml = true // Включаем поддержку HTML
            };
            mailMessage.To.Add(email);

            await client.SendMailAsync(mailMessage);
        }
    }
}