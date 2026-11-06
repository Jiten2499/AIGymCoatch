using System.Reflection;
using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Common.Behaviors;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Fitness.Ai;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LifeFitAI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        params Assembly[] moduleAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(moduleAssemblies);

        var assemblies = new List<Assembly> { typeof(DependencyInjection).Assembly };
        assemblies.AddRange(moduleAssemblies);

        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblies([.. assemblies]));

        services.AddValidatorsFromAssemblies(assemblies);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<ISessionIssuer, SessionIssuer>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IEntitlementStore, NullEntitlementStore>();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();
        services.AddScoped<IOperationGuard, OperationGuard>();
        services.AddScoped<IDashboardAccess, DashboardAccess>();
        services.AddScoped<InsightFactLoader>();
        services.AddScoped<IAccountLinker, AccountLinker>();
        services.AddScoped<IMemberAppSeatAssigner, MemberAppSeatAssigner>();
        services.AddScoped<IAiSafetyService, AiSafetyService>();
        services.AddScoped<IAiQuotaService, AiQuotaService>();
        services.AddScoped<IAiContextBuilder, AiContextBuilder>();

        return services;
    }
}
