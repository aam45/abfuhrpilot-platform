using AbfuhrPilot.Application.Contracts;
using AbfuhrPilot.Application.Routing;
using AbfuhrPilot.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AbfuhrPilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Module contracts (in-process implementations; swappable for HTTP/gRPC
        // clients when contexts are extracted into microservices).
        services.AddScoped<IFleetReadService, FleetReadService>();
        services.AddScoped<IWasteCompatibilityService, WasteCompatibilityQueryService>();

        // Routing: the local fallback provider is always available (no key needed).
        services.AddSingleton<LocalFallbackProvider>();
        services.AddSingleton<IRoutingProvider>(sp => sp.GetRequiredService<LocalFallbackProvider>());
        services.AddSingleton<IRouteOptimizationProvider>(sp => sp.GetRequiredService<LocalFallbackProvider>());
        services.AddSingleton<INavigationProvider>(sp => sp.GetRequiredService<LocalFallbackProvider>());
        services.AddSingleton<INavigationLinkBuilder, NavigationLinkBuilder>();
        services.AddScoped<RoutingService>();

        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<WasteTypeService>();
        services.AddScoped<VehicleService>();
        services.AddScoped<DriverService>();
        services.AddScoped<DestinationService>();
        services.AddScoped<ContainerService>();
        services.AddScoped<RouteService>();
        services.AddScoped<StopService>();
        services.AddScoped<PhotoService>();
        services.AddScoped<OrderService>();
        services.AddScoped<DashboardService>();
        return services;
    }
}
