using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Infrastructure.Persistence;
using AbfuhrPilot.Infrastructure.Security;
using AbfuhrPilot.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AbfuhrPilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=abfuhrpilot;Username=abfuhrpilot;Password=abfuhrpilot";

        // Allow an in-memory fallback so the API can boot without PostgreSQL for demos/CI.
        var useInMemory = config.GetValue<bool>("Database:UseInMemory");
        if (useInMemory)
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("abfuhrpilot"));
        else
            services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        var jwt = new JwtOptions();
        config.GetSection("Jwt").Bind(jwt);
        if (string.IsNullOrWhiteSpace(jwt.SecretKey))
            jwt.SecretKey = "dev-only-insecure-key-change-me-please-32chars-minimum!!";
        services.AddSingleton(jwt);

        var authTokens = new AbfuhrPilot.Application.Auth.AuthTokenOptions();
        config.GetSection("Auth").Bind(authTokens);
        services.AddSingleton(authTokens);
        services.AddScoped<AbfuhrPilot.Application.Auth.IEmailSender, LogEmailSender>();

        // ---- Routing providers ----
        var routing = new AbfuhrPilot.Application.Routing.RoutingOptions();
        config.GetSection("Routing").Bind(routing);
        services.AddSingleton(routing);
        services.AddHttpClient<AbfuhrPilot.Infrastructure.Routing.GraphHopperProvider>();
        services.AddTransient<AbfuhrPilot.Application.Routing.IRoutingProvider>(sp =>
            sp.GetRequiredService<AbfuhrPilot.Infrastructure.Routing.GraphHopperProvider>());
        services.AddTransient<AbfuhrPilot.Application.Routing.IRouteOptimizationProvider>(sp =>
            sp.GetRequiredService<AbfuhrPilot.Infrastructure.Routing.GraphHopperProvider>());
        services.AddTransient<AbfuhrPilot.Application.Routing.INavigationProvider>(sp =>
            sp.GetRequiredService<AbfuhrPilot.Infrastructure.Routing.GraphHopperProvider>());

        // ---- Outbox (microservice-ready event publication) ----
        services.AddScoped<AbfuhrPilot.Infrastructure.Outbox.OutboxDispatcher>();
        services.AddHostedService<AbfuhrPilot.Infrastructure.Outbox.OutboxProcessor>();

        // ---- Idempotency (safe retries / native offline replay) ----
        services.AddScoped<AbfuhrPilot.Infrastructure.Idempotency.IdempotencyService>();
        services.AddHostedService<AbfuhrPilot.Infrastructure.Idempotency.IdempotencyCleanupService>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IPhotoStorage, AbfuhrPilot.Infrastructure.Storage.LocalPhotoStorage>();
        services.AddSingleton<AbfuhrPilot.Application.Auth.ILoginThrottle, InMemoryLoginThrottle>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<DataSeeder>();
        return services;
    }
}
