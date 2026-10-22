using AbfuhrPilot.Api.Startup;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace AbfuhrPilot.UnitTests;

public class ProductionSecretsGuardTests
{
    private static IConfiguration BuildConfiguration(
        string? jwtSecretKey = "a-real-production-signing-key-that-is-long-enough",
        string? connectionString = "Host=prod-db;Port=5432;Database=abfuhrpilot;Username=abfuhrpilot;Password=SomeReallyStrongProdPassword!",
        string? graphHopperApiKey = "real-graphhopper-key")
    {
        var data = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = jwtSecretKey,
            ["ConnectionStrings:Default"] = connectionString,
            ["Routing:GraphHopper:ApiKey"] = graphHopperApiKey
        };

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    [Fact]
    public void Validate_Production_AllSecretsSet_DoesNotThrow()
    {
        var configuration = BuildConfiguration();

        var act = () => ProductionSecretsGuard.Validate(configuration, "Production");

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_Production_EmptyJwtSecretKey_Throws()
    {
        var configuration = BuildConfiguration(jwtSecretKey: "");

        var act = () => ProductionSecretsGuard.Validate(configuration, "Production");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:SecretKey*");
    }

    [Fact]
    public void Validate_Production_DevDefaultJwtSecretKey_Throws()
    {
        var configuration = BuildConfiguration(jwtSecretKey: "dev-only-insecure-key-change-me-please-32chars-minimum!!");

        var act = () => ProductionSecretsGuard.Validate(configuration, "Production");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:SecretKey*");
    }

    [Fact]
    public void Validate_Production_DevDefaultPostgresPassword_Throws()
    {
        var configuration = BuildConfiguration(
            connectionString: "Host=db;Port=5432;Database=abfuhrpilot;Username=abfuhrpilot;Password=abfuhrpilot");

        var act = () => ProductionSecretsGuard.Validate(configuration, "Production");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionStrings:Default*");
    }

    [Fact]
    public void Validate_Production_EmptyGraphHopperApiKey_Throws()
    {
        var configuration = BuildConfiguration(graphHopperApiKey: "");

        var act = () => ProductionSecretsGuard.Validate(configuration, "Production");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Routing:GraphHopper:ApiKey*");
    }

    [Fact]
    public void Validate_Production_MultipleProblems_SingleExceptionMentionsAll()
    {
        var configuration = BuildConfiguration(
            jwtSecretKey: "",
            connectionString: "Host=db;Port=5432;Database=abfuhrpilot;Username=abfuhrpilot;Password=abfuhrpilot",
            graphHopperApiKey: "");

        var act = () => ProductionSecretsGuard.Validate(configuration, "Production");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:SecretKey*")
            .Where(ex => ex.Message.Contains("ConnectionStrings:Default") && ex.Message.Contains("Routing:GraphHopper:ApiKey"));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Validate_NonProduction_WithInsecureDefaults_DoesNotThrow(string environmentName)
    {
        var configuration = BuildConfiguration(
            jwtSecretKey: "dev-only-insecure-key-change-me-please-32chars-minimum!!",
            connectionString: "Host=db;Port=5432;Database=abfuhrpilot;Username=abfuhrpilot;Password=abfuhrpilot",
            graphHopperApiKey: "");

        var act = () => ProductionSecretsGuard.Validate(configuration, environmentName);

        act.Should().NotThrow();
    }
}
