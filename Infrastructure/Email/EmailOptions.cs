namespace InnovaTube.Api.Infrastructure;

public class EmailOptions
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public bool UseSsl { get; set; }

    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;
}
