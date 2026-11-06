using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using LifeFitAI.Api.OpenApi;
using LifeFitAI.Api.Security;
using LifeFitAI.Application;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Infrastructure;
using LifeFitAI.Infrastructure.Platform;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LifeFitAI.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public const string LocalClientsCorsPolicy = "LocalClients";

    public static IServiceCollection AddLifeFitPlatform(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var dataProtection = services.AddDataProtection().SetApplicationName("LifeFitAI");
        var keyRing = new DirectoryInfo(configuration["DataProtection:KeysPath"] ?? "/var/lifefit/dataprotection");
        if (keyRing.Exists)
        {
            dataProtection.PersistKeysToFileSystem(keyRing);
        }
        services.AddApplication();
        services.AddInfrastructure(configuration, environment);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, HttpCurrentActor>();
        services.AddScoped<IClientContext, HttpClientContext>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "LifeFitAI",
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"] ?? "LifeFitAI",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = _ =>
                    {
                        PlatformMetrics.AuthFailures.Add(1, new KeyValuePair<string, object?>("reason", "token"));
                        return Task.CompletedTask;
                    },
                };
            });
        services.AddSignalR();
        services.AddAuthorization();
        services.AddScoped<LifeFitAI.Application.Chat.IChatNotifier, Hubs.SignalRChatNotifier>();
        services.AddScoped<LifeFitAI.Application.Gym.Communications.ICircularNotifier, Hubs.SignalRCircularNotifier>();
        services.AddConfiguredRateLimiter(configuration);

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            };
        });

        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddScoped<Middleware.IdempotencyFilter>();
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        }).AddMvc();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "LifeFit AI API",
                Version = "1.0",
                Description = "LifeFit AI HTTP API version 1. Send X-Correlation-ID (8-64 letters, digits, or hyphens) or the API generates one. Authentication, password reset, AI, and check-in use stricter rate limits. Failures use ProblemDetails. Production allows only configured CORS origins.",
            });
            options.DocumentFilter<ApiVersionDocumentFilter>();
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT access token.",
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer",
                        },
                    },
                    Array.Empty<string>()
                },
            });
        });

        services.AddCors(options =>
        {
            options.AddPolicy(LocalClientsCorsPolicy, policy =>
            {
                policy.WithOrigins(
                    "http://localhost:3000",
                    "http://127.0.0.1:3000",
                    "http://localhost:8081",
                    "http://127.0.0.1:8081")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });

            var allowed = configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ?? [];
            if (environment.IsProduction() && allowed.Length == 0)
            {
                throw new InvalidOperationException("Security:AllowedOrigins is required outside development.");
            }

            options.AddPolicy(ConfiguredCorsPolicy, policy =>
            {
                policy.WithOrigins(allowed)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    public const string ConfiguredCorsPolicy = "ConfiguredClients";

    private static void AddConfiguredRateLimiter(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                PlatformMetrics.RateLimited.Add(1, new KeyValuePair<string, object?>("policy", "rejected"));
                context.HttpContext.Response.ContentType = "application/problem+json";
                return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    type = "https://api.lifefit.ai/errors/rate-limit",
                    title = "Too Many Requests",
                    status = 429,
                    detail = "The request rate limit was reached.",
                }));
            };
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RateLimitPartition.GetFixedWindowLimiter(Partition(http), _ => Window(limits.GlobalPermitLimit, limits.GlobalWindowSeconds)));
            options.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter("auth:" + Partition(http), _ => Window(limits.AuthPermitLimit, limits.AuthWindowSeconds)));
            options.AddPolicy("ai", http => RateLimitPartition.GetFixedWindowLimiter("ai:" + Partition(http), _ => Window(limits.AiPermitLimit, limits.AiWindowSeconds)));
            options.AddPolicy("checkin", http => RateLimitPartition.GetFixedWindowLimiter("checkin:" + Partition(http), _ => Window(limits.CheckInPermitLimit, limits.CheckInWindowSeconds)));
        });
    }

    private static string Partition(HttpContext http) =>
        http.User.FindFirst("sub")?.Value
        ?? http.Connection.RemoteIpAddress?.ToString()
        ?? "anonymous";

    private static FixedWindowRateLimiterOptions Window(int permitLimit, int windowSeconds) => new()
    {
        PermitLimit = Math.Max(1, permitLimit),
        Window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds)),
        QueueLimit = 0,
    };
}

