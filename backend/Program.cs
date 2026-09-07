using Backend.Authorization;
using Backend.Configuration;
using Backend.Data;
using Backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// HTTPS is terminated by Nginx in production. Trust the forwarded protocol
// and client IP headers sent by that private Docker-network proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("AuthSettings"));
builder.Services.Configure<MediaStorageSettings>(builder.Configuration.GetSection("MediaStorage"));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseOracle(builder.Configuration.GetConnectionString("Oracle")));

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICreditService, CreditService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
// SSE 推送广播：跨请求共享在线连接，必须 Singleton
builder.Services.AddSingleton<INotificationPushService, NotificationPushService>();
builder.Services.AddScoped<IMediaStorageService>((sp) =>
{
    var mediaStorageSettings = sp.GetRequiredService<IOptions<MediaStorageSettings>>().Value;
    if (!string.Equals(mediaStorageSettings.Provider, "s3", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Only MediaStorage.Provider=s3 is supported in this deployment mode.");
    }

    var logger = sp.GetRequiredService<ILogger<S3MediaStorageService>>();
    return new S3MediaStorageService(
        sp.GetRequiredService<IOptions<MediaStorageSettings>>(),
        logger);
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "TongjiForumAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            var userIdValue = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieSessionVersion = context.Principal?.FindFirst(AuthenticationSession.ClaimType)?.Value;
            if (!int.TryParse(userIdValue, out var userId)
                || string.IsNullOrWhiteSpace(cookieSessionVersion))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var currentSession = await db.Users
                .AsNoTracking()
                .Where(user => user.UserID == userId)
                .Select(user => new { user.SessionVersion, user.Status })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

            if (currentSession == null
                || !string.Equals(currentSession.Status, "Active", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    currentSession.SessionVersion,
                    cookieSessionVersion,
                    StringComparison.Ordinal))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Manager", policy => policy.RequireRole("Manager"));
    options.AddPolicy("Moderator", policy => policy.RequireRole("Manager", "Moderator"));
    options.AddPolicy("Dashboard", policy => policy.Requirements.Add(new PermissionRequirement("dashboard.view")));
});

builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

builder.Services.AddCors(options =>
{
    var configuredOrigins = builder.Configuration
        .GetSection("Cors:Origins")
        .GetChildren()
        .Select(section => section.Value)
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Cast<string>()
        .ToArray();

    if (configuredOrigins.Length == 0)
    {
        configuredOrigins = ["http://localhost:5173"];
    }

    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(configuredOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Must run before authentication and endpoint handling so Request.IsHttps and
// the remote IP reflect the original browser request behind Nginx.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapGet("/uploads/{bucket}/{*objectKey}", async (string bucket, string? objectKey, IMediaStorageService mediaStorageService) =>
{
    bucket = bucket?.Trim().ToLowerInvariant() ?? string.Empty;
    if (bucket is not ("posts" or "products" or "avatars"))
    {
        return Results.NotFound();
    }

    if (string.IsNullOrWhiteSpace(objectKey) || !MediaStorageShared.IsSafeObjectKey(objectKey))
    {
        return Results.BadRequest();
    }

    var objectPath = $"{bucket}/{objectKey}";
    var stored = await mediaStorageService.ReadObjectAsync(objectPath);
    if (stored == null)
    {
        return Results.NotFound();
    }

    return Results.File(stored.Content, stored.ContentType, stored.FileName);
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
