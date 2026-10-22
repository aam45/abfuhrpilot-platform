using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AbfuhrPilot.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests.Integration;

/// <summary>
/// End-to-end HTTP-level coverage for the <c>MultipartRequestHasher</c> hardening
/// fix: a same <c>Idempotency-Key</c> retry of the
/// EXACT same file must still replay cleanly (boundary noise ignored), while a
/// same-key upload of a genuinely DIFFERENT file must now be rejected as a 409
/// conflict instead of silently returning the original photo.
/// </summary>
public class ProblemPhotosIdempotencyTests : IClassFixture<WebApiFactory>
{
    private readonly WebApiFactory _factory;

    public ProblemPhotosIdempotencyTests(WebApiFactory factory) => _factory = factory;

    private static MultipartFormDataContent PhotoContent(byte[] bytes, string fileName = "photo.jpg")
    {
        var content = new MultipartFormDataContent("test-boundary-" + Guid.NewGuid().ToString("N"))
        {
            { new ByteArrayContent(bytes) { Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") } }, "file", fileName },
        };
        return content;
    }

    [Fact]
    public async Task Retried_upload_of_the_SAME_file_with_the_same_key_replays_the_original_photo()
    {
        var scenario = await _factory.SeedInProgressScenarioAsync();
        var token = _factory.MintToken(scenario.CompanyId, scenario.DriverUserId, UserRole.Driver, scenario.DriverId);
        var client = _factory.CreateAuthenticatedClient(token);
        var key = Guid.NewGuid().ToString();
        var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "api/v1/problem-photos")
        {
            Content = PhotoContent(bytes),
        };
        firstRequest.Headers.Add("Idempotency-Key", key);
        var firstResponse = await client.SendAsync(firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        using var firstDoc = JsonDocument.Parse(firstBody);
        var firstId = firstDoc.RootElement.GetProperty("id").GetGuid();

        // Second attempt: SAME bytes, but a fresh (different) multipart boundary —
        // exactly what a real client retry produces.
        using var retryRequest = new HttpRequestMessage(HttpMethod.Post, "api/v1/problem-photos")
        {
            Content = PhotoContent(bytes),
        };
        retryRequest.Headers.Add("Idempotency-Key", key);
        var retryResponse = await client.SendAsync(retryRequest);

        retryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        retryResponse.Headers.TryGetValues("Idempotency-Replayed", out var replayed).Should().BeTrue();
        replayed!.Should().ContainSingle().Which.Should().Be("true");

        var retryBody = await retryResponse.Content.ReadAsStringAsync();
        using var retryDoc = JsonDocument.Parse(retryBody);
        retryDoc.RootElement.GetProperty("id").GetGuid().Should().Be(firstId); // replayed, not a new photo
    }

    [Fact]
    public async Task Reused_key_with_a_DIFFERENT_file_is_rejected_as_a_409_conflict()
    {
        var scenario = await _factory.SeedInProgressScenarioAsync();
        var token = _factory.MintToken(scenario.CompanyId, scenario.DriverUserId, UserRole.Driver, scenario.DriverId);
        var client = _factory.CreateAuthenticatedClient(token);
        var key = Guid.NewGuid().ToString();

        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "api/v1/problem-photos")
        {
            Content = PhotoContent(new byte[] { 1, 2, 3, 4 }, "photo-one.jpg"),
        };
        firstRequest.Headers.Add("Idempotency-Key", key);
        var firstResponse = await client.SendAsync(firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Same key, genuinely different file content — must now be a 409, not a
        // silent replay of the first photo (the pre-fix documented gap).
        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, "api/v1/problem-photos")
        {
            Content = PhotoContent(new byte[] { 9, 9, 9, 9, 9, 9 }, "photo-two.jpg"),
        };
        secondRequest.Headers.Add("Idempotency-Key", key);
        var secondResponse = await client.SendAsync(secondRequest);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var json = await secondResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("title").GetString().Should().Be("Idempotency key conflict");
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(409);
    }
}
