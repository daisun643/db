using Backend.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Backend.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;
    private const int SmtpTimeoutMilliseconds = 5000;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendVerificationCodeAsync(string email, string code)
    {
        var subject = "【同济论坛】注册验证码";
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <h2 style='color: #4f46e5;'>欢迎注册同济论坛</h2>
                <p>您的验证码是：</p>
                <div style='background-color: #f3f4f6; padding: 20px; text-align: center; font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #4f46e5;'>
                    {code}
                </div>
                <p style='color: #6b7280; margin-top: 20px;'>验证码有效期为 5 分钟，请尽快完成注册。</p>
                <p style='color: #6b7280;'>如果这不是您的操作，请忽略此邮件。</p>
                <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 30px 0;'>
                <p style='color: #9ca3af; font-size: 12px;'>此邮件由系统自动发送，请勿回复。</p>
            </div>
        ";

        return await SendEmailAsync(email, subject, body);
    }

    public async Task<bool> SendPasswordResetCodeAsync(string email, string code)
    {
        var subject = "【同济论坛】密码重置验证码";
        var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <h2 style='color: #4f46e5;'>密码重置请求</h2>
                <p>您正在重置密码，验证码是：</p>
                <div style='background-color: #f3f4f6; padding: 20px; text-align: center; font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #4f46e5;'>
                    {code}
                </div>
                <p style='color: #6b7280; margin-top: 20px;'>验证码有效期为 5 分钟。</p>
                <p style='color: #ef4444; font-weight: bold;'>如果这不是您的操作，请立即修改密码并联系管理员！</p>
                <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 30px 0;'>
                <p style='color: #9ca3af; font-size: 12px;'>此邮件由系统自动发送，请勿回复。</p>
            </div>
        ";

        return await SendEmailAsync(email, subject, body);
    }

    private async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            client.Timeout = SmtpTimeoutMilliseconds;
            
            using var cts = new CancellationTokenSource(SmtpTimeoutMilliseconds);
            await client.ConnectAsync(
                _settings.SmtpServer,
                _settings.SmtpPort,
                _settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
                cts.Token);
            await client.AuthenticateAsync(_settings.SenderEmail, _settings.SenderPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("邮件发送成功: {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "邮件发送失败: {Email}", toEmail);
            return false;
        }
    }
}
