using System.Threading.Tasks;

namespace InnovaTube.Api.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
}
