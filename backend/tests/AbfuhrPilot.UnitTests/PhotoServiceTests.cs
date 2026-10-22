using System.Text;
using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Services;
using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

public class PhotoServiceTests
{
    private readonly FakeCurrentUser _me = new();

    /// <summary>In-memory IPhotoStorage for tests.</summary>
    private sealed class FakeStorage : IPhotoStorage
    {
        public readonly Dictionary<string, byte[]> Files = new();
        public Task<string> SaveAsync(Stream content, string ext, Guid companyId, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            var key = $"{companyId}/{Guid.NewGuid():N}{ext}";
            Files[key] = ms.ToArray();
            return Task.FromResult(key);
        }
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<Stream>(new MemoryStream(Files[key]));
        public Task DeleteAsync(string key, CancellationToken ct = default) { Files.Remove(key); return Task.CompletedTask; }
    }

    private static Stream Bytes(int n = 10) => new MemoryStream(Encoding.UTF8.GetBytes(new string('x', n)));

    [Fact]
    public async Task Upload_rejects_disallowed_content_type()
    {
        var svc = new PhotoService(TestDb.Create(), _me, new FakeStorage());
        var act = async () => await svc.UploadAsync(Bytes(), "application/pdf", "x.pdf", 10, default);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*JPEG*");
    }

    [Fact]
    public async Task Upload_rejects_oversized_image()
    {
        var svc = new PhotoService(TestDb.Create(), _me, new FakeStorage());
        var act = async () => await svc.UploadAsync(Bytes(), "image/png", "big.png", PhotoService.MaxBytes + 1, default);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*MB*");
    }

    [Fact]
    public async Task Upload_stores_and_returns_a_streaming_url_not_a_filesystem_path()
    {
        var db = TestDb.Create();
        var svc = new PhotoService(db, _me, new FakeStorage());
        var dto = await svc.UploadAsync(Bytes(20), "image/jpeg", "unfall.jpg", 20, default);
        dto.Url.Should().Be($"/api/v1/problem-photos/{dto.Id}/content");
        dto.Url.Should().NotContain(":\\").And.NotContain("/App_Data");
        dto.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task Content_fetch_is_company_scoped()
    {
        var db = TestDb.Create();
        var storage = new FakeStorage();
        var mine = new PhotoService(db, _me, storage);
        var dto = await mine.UploadAsync(Bytes(), "image/png", "a.png", 10, default);

        var otherCompany = new PhotoService(db, new FakeCurrentUser { CompanyId = Guid.NewGuid() }, storage);
        var act = async () => await otherCompany.GetContentAsync(dto.Id, default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Reporting_problem_links_uploaded_photos_to_the_report()
    {
        var db = TestDb.Create();
        var storage = new FakeStorage();
        var photos = new PhotoService(db, _me, storage);
        var p1 = await photos.UploadAsync(Bytes(), "image/jpeg", "1.jpg", 10, default);

        var route = new Route { CompanyId = _me.CompanyId, RouteNumber = "R1", Name = "R",
            DriverId = Guid.NewGuid(), VehicleId = Guid.NewGuid(), Status = RouteStatus.InProgress };
        var stop = new Stop { CompanyId = _me.CompanyId, RouteId = route.Id, Address = "A", SequenceNumber = 1 };
        db.AddRange(route, stop);
        await db.SaveChangesAsync();

        var stops = new StopService(db, _me); // admin role → allowed
        await stops.ReportProblemAsync(stop.Id, new ReportProblemRequest(ProblemType.BlockedByCar, "Auto im Weg",
            new List<Guid> { p1.Id }), default);

        var linked = await photos.GetAsync(p1.Id, default);
        linked.Should().NotBeNull();
        var report = db.ProblemReports.Single();
        db.ProblemPhotos.Single(p => p.Id == p1.Id).ProblemReportId.Should().Be(report.Id);
        report.PhotoUrl.Should().Contain(p1.Id.ToString());
    }
}
