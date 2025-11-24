using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using InnovaTube.Api.Infrastructure;
using InnovaTube.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace InnovaTube.Api.Services.Implementations
{
    public class EmailService : IEmailService
    {
        private readonly EmailOptions _options;

        public EmailService(IOptions<EmailOptions> options)
        {
            _options = options.Value;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.UseSsl,
                Credentials = new NetworkCredential(_options.User, _options.Password)
            };

            var mail = new MailMessage
            {
                From = new MailAddress(_options.From),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            mail.To.Add(to);

            await client.SendMailAsync(mail);
        }
    }
}
