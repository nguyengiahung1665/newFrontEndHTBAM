using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using HTBAM.Api.Support;
using HTBAM.Application.Interfaces;
using HTBAM.Application.Services;
using HTBAM.Infrastructure.Data;
using HTBAM.Infrastructure.Services;
using HTBAM.Infrastructure.Services.Llm;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "web",
        policy =>
            policy
                .WithOrigins(
                    builder.Configuration
                        .GetSection("Cors:Origins")
                        .Get<string[]>()
                    ?? ["http://localhost:5173"])
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("SqlServer"),
        sql =>
            sql.EnableRetryOnFailure(
                5,
                TimeSpan.FromSeconds(5),
                null));
});

builder.Services.AddScoped<IAppDbContext>(
    serviceProvider =>
        serviceProvider.GetRequiredService<AppDbContext>());

builder.Services.AddSingleton<IObjectStorage, MinioObjectStorage>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAlertEngine, AlertEngine>();
builder.Services.AddScoped<IFaceEnrollmentService, FaceEnrollmentService>();
builder.Services.AddScoped<ISummaryService, SummaryService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IAiEventService, AiEventService>();
builder.Services.AddScoped<IAutoCommentService, AutoCommentService>();
builder.Services.AddSingleton<GeminiLlmProvider>();
builder.Services.AddSingleton<UnavailableLlmProvider>();
builder.Services.AddSingleton<ILlmProviderResolver, LlmProviderResolver>();

builder.Services.AddHttpClient<IAiClient, AiClient>(client =>
{
    client.BaseAddress =
        new Uri(
            builder.Configuration["AiService:BaseUrl"]
            ?? "http://localhost:8001/");

    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter(
        "auth",
        limiter =>
        {
            limiter.PermitLimit = 10;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
            limiter.AutoReplenishment = true;
        });

    options.RejectionStatusCode = 429;
});

var key = Encoding.UTF8.GetBytes(
    builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret missing"));

if (key.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Secret phải tối thiểu 32 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer =
                    builder.Configuration["Jwt:Issuer"],
                ValidAudience =
                    builder.Configuration["Jwt:Audience"],
                IssuerSigningKey =
                    new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.FromMinutes(1),
            };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token =
                    context.Request.Query["access_token"];

                if (
                    !string.IsNullOrEmpty(token) &&
                    context.HttpContext.Request.Path
                        .StartsWithSegments("/hubs"))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },

            OnTokenValidated = async context =>
            {
                var idText =
                    context.Principal?.FindFirstValue(
                        ClaimTypes.NameIdentifier);

                var versionText =
                    context.Principal?.FindFirstValue(
                        "token_version");

                if (
                    !long.TryParse(idText, out var id) ||
                    !int.TryParse(versionText, out var version))
                {
                    context.Fail("Invalid token claims");
                    return;
                }

                var db =
                    context.HttpContext.RequestServices
                        .GetRequiredService<AppDbContext>();

                var user =
                    await db.UsersSet
                        .AsNoTracking()
                        .Where(item => item.Id == id)
                        .Select(item => new
                        {
                            item.Status,
                            item.TokenVersion,
                        })
                        .FirstOrDefaultAsync();

                if (
                    user is null ||
                    user.Status != "ACTIVE" ||
                    user.TokenVersion != version)
                {
                    context.Fail("Token revoked");
                }
            },
        };
    });

builder.Services.AddAuthorization();

QuestPDF.Settings.License =
    LicenseType.Community;

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("web");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet(
        "/health",
        () =>
            Results.Ok(
                new
                {
                    status = "ok",
                }))
    .AllowAnonymous();

app.MapControllers();

app.MapHub<HTBAM.Api.Hubs.SessionHub>(
    "/hubs/session");

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("HTBAM.Startup");

    var db =
        scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

    await RetryAsync(
        async () =>
        {
            if (!await db.Database.CanConnectAsync())
            {
                throw new InvalidOperationException(
                    "SQL Server chưa sẵn sàng.");
            }
        },
        logger,
        "SQL Server");

    if (
        builder.Configuration.GetValue(
            "Database:EnsureCreated",
            false))
    {
        await RetryAsync(
            () => db.Database.EnsureCreatedAsync(),
            logger,
            "Database EnsureCreated");
    }

    if (
        app.Environment.IsDevelopment() &&
        builder.Configuration.GetValue(
            "Seed:Development",
            true))
    {
        await RetryAsync(
            () => DevelopmentSeeder.SeedAsync(db),
            logger,
            "Development seed");
    }

    if (
        builder.Configuration.GetValue(
            "ObjectStorage:EnsureBucketOnStartup",
            true))
    {
        var storage =
            scope.ServiceProvider
                .GetRequiredService<IObjectStorage>();

        await RetryAsync(
            () => storage.EnsureReadyAsync(),
            logger,
            "MinIO");
    }
}

app.Run();

static async Task RetryAsync(
    Func<Task> operation,
    ILogger logger,
    string operationName,
    int maxAttempts = 20,
    int delaySeconds = 3)
{
    Exception? lastError = null;

    for (
        var attempt = 1;
        attempt <= maxAttempts;
        attempt++)
    {
        try
        {
            await operation();

            logger.LogInformation(
                "{Operation} ready on attempt {Attempt}.",
                operationName,
                attempt);

            return;
        }
        catch (Exception error)
        {
            lastError = error;

            logger.LogWarning(
                error,
                "{Operation} chưa sẵn sàng. Lần thử {Attempt}/{MaxAttempts}.",
                operationName,
                attempt,
                maxAttempts);

            if (attempt < maxAttempts)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(delaySeconds));
            }
        }
    }

    throw new InvalidOperationException(
        $"{operationName} không sẵn sàng sau {maxAttempts} lần thử.",
        lastError);
}
