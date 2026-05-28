namespace Backend.Configuration;

public class EmailSettings
{
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public bool UseSsl { get; set; }
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderPassword { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string AllowedDomain { get; set; } = string.Empty;
}

public class AuthSettings
{
    public int CodeExpirationMinutes { get; set; } = 5;
    public int CookieExpirationDays { get; set; } = 7;
    public int PasswordMinLength { get; set; } = 8;
}
