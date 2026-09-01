using Backend.Authorization;
using Backend.Configuration;
using Backend.Data;
using Backend.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("Manager", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("Moderator", policy => policy.RequireRole("Admin", "Manager", "Moderator"));
    options.AddPolicy("Dashboard", policy => policy.Requirements.Add(new PermissionRequirement("dashboard.view")));
});

builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();
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


