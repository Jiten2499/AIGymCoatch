using System.Text;
using LifeFitAI.Application.Abstractions.Persistence;
using LifeFitAI.Application.Ai;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.Infrastructure.Ai;
using LifeFitAI.Infrastructure.Gym;
using LifeFitAI.Infrastructure.Platform;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.Infrastructure.Persistence.Interceptors;
using LifeFitAI.Infrastructure.Security;
using LifeFitAI.Infrastructure.Time;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LifeFitAI.Infrastructure;

public static class DependencyInjection
{
    public const string SqlServerConnectionName = "SqlServer";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var connectionString = configuration.GetConnectionString(SqlServerConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ConnectionStrings:SqlServer' is not configured.");
        }

        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 bytes.");
        }

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<GymOptions>(configuration.GetSection(GymOptions.SectionName));
        services.Configure<PasswordOptions>(configuration.GetSection(PasswordOptions.SectionName));
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IPasswordPolicy, PasswordPolicy>();
        services.AddSingleton<ISecretTokenFactory, Sha256SecretTokenFactory>();
        services.AddSingleton<IAccessTokenFactory, JwtAccessTokenFactory>();
        services.AddSingleton<ITokenSettings, TokenSettings>();
        services.AddSingleton<IPasswordResetNotifier, NoopPasswordResetNotifier>();
        services.AddScoped<ICurrentActor, AnonymousActor>();
        services.AddScoped<IClientContext, AnonymousClient>();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantMembershipRepository, TenantMembershipRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IBusinessProfileRepository, BusinessProfileRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IPeopleRepository, PeopleRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IDashboardQuery, DashboardQuery>();
        services.AddScoped<IDashboardAnalytics, DashboardAnalyticsQuery>();
        services.AddSingleton<IBusinessInsightCache, MemoryBusinessInsightCache>();
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.AddSingleton<IAiLimits, AiLimits>();
        services.AddHttpClient("llm", client => client.Timeout = TimeSpan.FromSeconds(60));
        services.AddScoped<ILlmConnectionStore, LlmConnectionStore>();
        services.AddSingleton<ILlmSecretProtector, DataProtectionLlmSecretProtector>();
        services.AddScoped<ILlmProbe, OpenAiCompatibleLlmProbe>();
        services.AddScoped<ILlmRuntime, DatabaseLlmRuntime>();
        services.AddScoped<IAiProvider, OpenAiCompatibleAiProvider>();
        services.AddScoped<IFitnessRepository, FitnessRepository>();
        services.AddScoped<IWorkoutRepository, WorkoutRepository>();
        services.AddScoped<INutritionRepository, NutritionRepository>();
        services.AddScoped<IAiRepository, AiRepository>();
        services.AddScoped<IChallengeRepository, ChallengeRepository>();
        services.AddScoped<IMemberDashboardQuery, MemberDashboardQuery>();
        services.AddSingleton<IQrSettings, QrSettings>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var timeout = Math.Clamp(configuration.GetValue("Security:CommandTimeoutSeconds", 30), 5, 120);
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 5);
                sql.CommandTimeout(timeout);
            });

            options.AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>());

            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
            }
        });

        services.AddScoped<IApplicationDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<ApplicationDbContext>());

        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<ObservabilityOptions>(configuration.GetSection(ObservabilityOptions.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<ISecretProvider, EnvironmentSecretProvider>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<ICacheService>(serviceProvider => CreateCache(serviceProvider, configuration));
        services.AddScoped<IPermissionCacheInvalidator, PermissionCacheInvalidator>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IDataExportService, PersonalDataExportService>();
        services.AddScoped<IPlanDefinitionRepository, LicenseAdminStore>();
        services.AddScoped<ILicenseAdminStore, LicenseAdminStore>();
        services.AddScoped<IEntitlementStore, EfEntitlementStore>();
        services.AddScoped<FeatureFlagService>();
        services.AddScoped<IFeatureFlagService, CachingFeatureFlagService>();
        services.AddScoped<IPermissionService, CachingPermissionService>();
        services.AddHostedService<TokenCleanupJob>();
        services.AddPlatformTelemetry(configuration);

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>("sqlserver", tags: ["ready"]);

        return services;
    }

    private static ICacheService CreateCache(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        var redis = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
        if (!redis.Enabled || string.IsNullOrWhiteSpace(redis.ConnectionString))
        {
            return ActivatorUtilities.CreateInstance<MemoryCacheService>(serviceProvider);
        }

        try
        {
            var options = StackExchange.Redis.ConfigurationOptions.Parse(redis.ConnectionString);
            options.AbortOnConnectFail = false;
            var connection = StackExchange.Redis.ConnectionMultiplexer.Connect(options);
            return new RedisCacheService(connection, serviceProvider.GetRequiredService<ILogger<RedisCacheService>>());
        }
        catch (Exception exception) when (exception is StackExchange.Redis.RedisException or InvalidOperationException)
        {
            return ActivatorUtilities.CreateInstance<MemoryCacheService>(serviceProvider);
        }
    }
}
