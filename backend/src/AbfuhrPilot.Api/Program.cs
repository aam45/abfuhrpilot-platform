using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AbfuhrPilot.Api.Auth;
using AbfuhrPilot.Api.Middleware;
using AbfuhrPilot.Api.RateLimiting;
using AbfuhrPilot.Application;
using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Infrastructure;
using AbfuhrPilot.Infrastructure.Persistence;
using AbfuhrPilot.Infrastructure.Security;
using AbfuhrPilot.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Keep JWT claim names as-is (don't remap "sub" -> nameidentifier).
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.Services.AddControllers(o =>
{
    // Global idempotency filter — active only on [Idempotent]-marked endpoints.
    o.Filters.Add<AbfuhrPilot.Api.Idempotency.IdempotencyFilter>();
}).AddJsonOptions(o =>
{
    // Serialize enums as strings so the API contract is readable and stable for clients.
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---- Optional live-key health check for the configured GraphHopper routing provider ----
// Uses its own named HttpClient (not GraphHopperProvider's) so its short, independent
// timeout never affects the provider's own retry/timeout behavior.
builder.Services.AddHttpClient(nameof(AbfuhrPilot.Api.Startup.GraphHopperHealthCheck));
builder.Services.AddHealthChecks()
    .AddCheck<AbfuhrPilot.Api.Startup.GraphHopperHealthCheck>("graphhopper", tags: new[] { "routing" });

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();

// ---- Authentication / Authorization ----
var jwt = new JwtOptions();
builder.Configuration.GetSection("Jwt").Bind(jwt);
AbfuhrPilot.Api.Startup.ProductionSecretsGuard.Validate(builder.Configuration, builder.Environment.EnvironmentName);
if (string.IsNullOrWhiteSpace(jwt.SecretKey))
    jwt.SecretKey = "dev-only-insecure-key-change-me-please-32chars-minimum!!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey))
        };
    });
builder.Services.AddAuthorization();

// ---- CORS (Angular dev server) ----
const string CorsPolicy = "frontend";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                 ?? new[] { "http://localhost:4200" })
    .AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("X-Correlation-Id")));

// ---- Rate limiting (anonymous AuthController endpoints) ----
// Split into 4 named policies (login / account-write / token-confirm / session)
// instead of one shared bucket, so heavy traffic on one endpoint (e.g. drivers'
// refresh calls on a shared-NAT depot network) cannot exhaust the same bucket
// and accidentally 429 unrelated legitimate auth traffic (e.g. login, password
// reset) from the same IP. Login keeps its own ILoginThrottle account-lockout
// logic in addition to its policy here; all four remain a coarser, per-IP
// defense-in-depth layer against request-volume abuse.
// NOTE: rateLimiting is bound from app.Configuration further below (after
// builder.Build()), not builder.Configuration here — WebApplicationFactory-based
// tests inject config overrides that only land in the final built configuration,
// same reason Seed:Enabled/Swagger:Enabled are read via app.Configuration.
var rateLimiting = new RateLimitingOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        var httpContext = context.HttpContext;
        var correlationId = httpContext.Items["CorrelationId"]?.ToString()
                             ?? httpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault()
                             ?? Guid.NewGuid().ToString();
        // Resolve the actual matched policy from the rejected request's endpoint
        // metadata so Retry-After reflects that policy's own window, not a
        // hardcoded default — the 4 policies' windows may diverge in the future.
        var policyName = httpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        var windowSeconds = policyName switch
        {
            "auth-login" => rateLimiting.AuthLogin.WindowSeconds,
            "auth-account-write" => rateLimiting.AuthAccountWrite.WindowSeconds,
            "auth-token-confirm" => rateLimiting.AuthTokenConfirm.WindowSeconds,
            "auth-session" => rateLimiting.AuthSession.WindowSeconds,
            _ => rateLimiting.AuthLogin.WindowSeconds // defensive default; not expected in practice
        };
        httpContext.Response.Headers["Retry-After"] = windowSeconds.ToString();
        httpContext.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new
        {
            title = "Too many requests",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Too many requests. Please try again later.",
            correlationId
        });
        await httpContext.Response.WriteAsync(payload, ct);
    };

    static RateLimitPartition<string> FixedWindowByIp(HttpContext httpContext, RateLimitPolicyOptions policy) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                QueueLimit = 0
            });

    options.AddPolicy("auth-login", httpContext => FixedWindowByIp(httpContext, rateLimiting.AuthLogin));
    options.AddPolicy("auth-account-write", httpContext => FixedWindowByIp(httpContext, rateLimiting.AuthAccountWrite));
    options.AddPolicy("auth-token-confirm", httpContext => FixedWindowByIp(httpContext, rateLimiting.AuthTokenConfirm));
    options.AddPolicy("auth-session", httpContext => FixedWindowByIp(httpContext, rateLimiting.AuthSession));
});

// ---- Swagger / OpenAPI ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AbfuhrPilot API", Version = "v1",
        Description = "Route, dispatch and driver navigation platform for waste collection."
    });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer",
        BearerFormat = "JWT", In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

var app = builder.Build();

app.Configuration.GetSection("RateLimiting").Bind(rateLimiting);

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AbfuhrPilot API v1"));
}

app.UseCors(CorsPolicy);
// UseRouting must run before UseRateLimiter so the limiter can read the
// resolved endpoint's [EnableRateLimiting] policy metadata.
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "AbfuhrPilot.Api" })).AllowAnonymous();

// Diagnostic-only surface for the optional GraphHopper live-key check — distinct from the
// liveness "/health" endpoint above; this endpoint reveals only whether the routing provider
// is configured/reachable and always answers 200 (never gates app health/readiness). Unlike
// the anonymous liveness probe, this is gated to Admin/Dispatcher: it is infra-fingerprinting
// (configuration/reachability state), not something an anonymous uptime prober needs.
app.MapGet("/health/routing", async (Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService healthCheckService) =>
{
    var report = await healthCheckService.CheckHealthAsync(r => r.Tags.Contains("routing"));
    var entry = report.Entries.TryGetValue("graphhopper", out var e) ? e : default;
    return Results.Ok(new
    {
        status = entry.Status.ToString(),
        description = entry.Description
    });
}).RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = "Admin,Dispatcher" });

// ---- Startup: migrate/create DB + seed demo data ----
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();

    if (app.Configuration.GetValue("Seed:Enabled", true))
        await sp.GetRequiredService<DataSeeder>().SeedAsync();
}

app.Run();

public partial class Program { }
