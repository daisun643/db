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

public class MediaStorageSettings
{
    public string Provider { get; set; } = "s3";
    public MediaStorageS3Settings S3 { get; set; } = new();
}

public class MediaStorageS3Settings
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string Bucket { get; set; } = "forum-media";
    public string Region { get; set; } = "us-east-1";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool UseSsl { get; set; }
    public string? PublicBaseUrl { get; set; }
}
