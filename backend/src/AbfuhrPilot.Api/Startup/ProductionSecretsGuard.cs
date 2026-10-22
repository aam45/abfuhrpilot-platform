namespace AbfuhrPilot.Api.Startup;

/// <summary>
/// Fail-fast guard that prevents the API from starting in Production with
/// committed dev-only default secrets (JWT signing key, Postgres password,
/// GraphHopper API key). Never affects Development/Testing/local runs.
/// </summary>
public static class ProductionSecretsGuard
{
    private const string ProductionEnvironmentName = "Production";
    private const string DevJwtSecretKey = "dev-only-insecure-key-change-me-please-32chars-minimum!!";
    private const string DevPostgresPassword = "abfuhrpilot";

    public static void Validate(IConfiguration configuration, string? environmentName)
    {
        if (!string.Equals(environmentName, ProductionEnvironmentName, StringComparison.OrdinalIgnoreCase))
            return;

        var problems = new List<string>();

        var jwtSecretKey = configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(jwtSecretKey))
        {
            problems.Add("Jwt:SecretKey is empty. Set a strong, unique signing key via configuration or secrets.");
        }
        else if (string.Equals(jwtSecretKey, DevJwtSecretKey, StringComparison.Ordinal))
        {
            problems.Add("Jwt:SecretKey is still set to the committed dev-only default value. Set a strong, unique signing key.");
        }

        var connectionString = configuration["ConnectionStrings:Default"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            problems.Add("ConnectionStrings:Default is empty. Configure a production database connection string.");
        }
        else if (HasDevPostgresPassword(connectionString))
        {
            problems.Add("ConnectionStrings:Default still uses the committed dev-only Postgres password. Set a strong, unique database password.");
        }

        var graphHopperApiKey = configuration["Routing:GraphHopper:ApiKey"];
        if (string.IsNullOrWhiteSpace(graphHopperApiKey))
        {
            problems.Add("Routing:GraphHopper:ApiKey is empty. Configure a production GraphHopper API key.");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "Refusing to start in Production with insecure/default configuration:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, problems.Select(p => "- " + p)));
        }
    }

    private static bool HasDevPostgresPassword(string connectionString)
    {
        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            if (parts.Length != 2)
                continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            if (string.Equals(key, "Password", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(value, DevPostgresPassword, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
