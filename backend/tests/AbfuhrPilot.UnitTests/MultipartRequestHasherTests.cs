using System.Security.Cryptography;
using System.Text;
using AbfuhrPilot.Api.Idempotency;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

/// <summary>
/// Unit-level coverage for <see cref="MultipartRequestHasher"/>, complementing the
/// HTTP-level scenarios in <c>ProblemPhotosIdempotencyTests</c>. Covers (1) the
/// malformed/missing-boundary fallback path, and (2) the core design property that
/// the multipart boundary itself is excluded from the hash while section order and
/// body bytes are not.
/// </summary>
public class MultipartRequestHasherTests
{
    private static Stream Body(byte[] bytes) => new MemoryStream(bytes);
    private static Stream Body(string s) => Body(Encoding.UTF8.GetBytes(s));

    private static byte[] ExpectedNoBoundaryMarkerHash()
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes("multipart-no-boundary"));
    }

    // ---- Malformed / missing boundary fallback path -----------------------

    [Theory]
    [InlineData("multipart/form-data")] // no boundary parameter at all
    [InlineData("   ")] // whitespace-only content type
    [InlineData("")] // empty content type
    [InlineData("not a valid content type;;;===")] // unparseable
    [InlineData("multipart/form-data; boundary=\"\"")] // quoted-empty boundary
    public async Task Malformed_or_missing_boundary_falls_back_to_the_stable_marker_hash(string contentType)
    {
        var hash = await MultipartRequestHasher.ComputeAsync(Body("irrelevant body bytes"), contentType, default);

        hash.Should().BeEquivalentTo(ExpectedNoBoundaryMarkerHash());
    }

    [Fact]
    public async Task Two_different_malformed_requests_hash_identically_to_the_marker_hash()
    {
        var hash1 = await MultipartRequestHasher.ComputeAsync(Body("body-one"), "multipart/form-data", default);
        var hash2 = await MultipartRequestHasher.ComputeAsync(Body("a completely different dummy body"), "multipart/form-data; boundary=\"\"", default);

        hash1.Should().BeEquivalentTo(hash2);
        hash1.Should().BeEquivalentTo(ExpectedNoBoundaryMarkerHash());
    }

    [Fact]
    public async Task Fallback_path_never_consults_the_body_bytes()
    {
        var hashA = await MultipartRequestHasher.ComputeAsync(Body(new byte[] { 1, 2, 3, 4, 5 }), "multipart/form-data", default);
        var hashB = await MultipartRequestHasher.ComputeAsync(Body(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9, 9 }), "multipart/form-data", default);

        hashA.Should().BeEquivalentTo(hashB);
        hashA.Should().BeEquivalentTo(ExpectedNoBoundaryMarkerHash());
    }

    // ---- Malformed body with a VALID boundary (truncated/garbage) ---------

    private static byte[] ExpectedMalformedBodyMarkerHash()
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes("multipart-malformed-body"));
    }

    [Fact]
    public async Task Valid_boundary_with_truncated_body_falls_back_to_the_malformed_body_marker_hash_without_throwing()
    {
        // Proper opening of a part, but cut off mid-section: no closing boundary.
        var body = Body("--boundary\r\nContent-Disposition: form-data; name=\"file\"; filename=\"a.jpg\"\r\nContent-Type: image/jpeg\r\n\r\nsome partial byte");

        var hash = await MultipartRequestHasher.ComputeAsync(body, ContentType("boundary"), default);

        hash.Should().BeEquivalentTo(ExpectedMalformedBodyMarkerHash());
    }

    [Fact]
    public async Task Valid_boundary_with_body_that_never_contains_the_boundary_falls_back_to_the_malformed_body_marker_hash()
    {
        var body = Body("this body never contains the configured boundary token at all");

        var hash = await MultipartRequestHasher.ComputeAsync(body, ContentType("boundary"), default);

        hash.Should().BeEquivalentTo(ExpectedMalformedBodyMarkerHash());
    }

    [Fact]
    public async Task Two_different_malformed_bodies_with_a_valid_boundary_hash_identically()
    {
        var body1 = Body("--boundary\r\nContent-Disposition: form-data; name=\"file\"\r\n\r\ntruncated-one");
        var body2 = Body("completely different garbage that never closes properly");

        var hash1 = await MultipartRequestHasher.ComputeAsync(body1, ContentType("boundary"), default);
        var hash2 = await MultipartRequestHasher.ComputeAsync(body2, ContentType("boundary"), default);

        hash1.Should().BeEquivalentTo(hash2);
        hash1.Should().BeEquivalentTo(ExpectedMalformedBodyMarkerHash());
    }

    [Fact]
    public async Task Malformed_body_marker_hash_differs_from_the_no_boundary_marker_hash()
    {
        var body = Body("--boundary\r\nContent-Disposition: form-data; name=\"file\"\r\n\r\ntruncated");

        var hash = await MultipartRequestHasher.ComputeAsync(body, ContentType("boundary"), default);

        hash.Should().NotBeEquivalentTo(ExpectedNoBoundaryMarkerHash());
    }

    private sealed class CancellationHonoringStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(0);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(0);
        }
    }

    [Fact]
    public async Task Cancellation_during_body_read_propagates_and_is_not_swallowed_into_the_marker()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            MultipartRequestHasher.ComputeAsync(new CancellationHonoringStream(), ContentType("boundary"), cts.Token));
    }

    // ---- Genuine multiple-file-parts case (problem-photos shape) ----------

    private sealed record Part(string FieldName, string FileName, string ContentType, byte[] Bytes);

    private static readonly Part PartOne = new("file", "photo-one.jpg", "image/jpeg", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
    private static readonly Part PartTwo = new("file2", "photo-two.png", "image/png", new byte[] { 10, 20, 30, 40, 50 });
    private static readonly Part PartTwoChangedBytes = PartTwo with { Bytes = new byte[] { 99, 98, 97, 96, 95 } };

    private static byte[] BuildMultipartBody(string boundary, params Part[] parts)
    {
        using var stream = new MemoryStream();
        foreach (var part in parts)
        {
            WriteAscii(stream, $"--{boundary}\r\n");
            WriteAscii(stream, $"Content-Disposition: form-data; name=\"{part.FieldName}\"; filename=\"{part.FileName}\"\r\n");
            WriteAscii(stream, $"Content-Type: {part.ContentType}\r\n");
            WriteAscii(stream, "\r\n");
            stream.Write(part.Bytes, 0, part.Bytes.Length);
            WriteAscii(stream, "\r\n");
        }
        WriteAscii(stream, $"--{boundary}--\r\n");
        return stream.ToArray();
    }

    private static void WriteAscii(Stream stream, string s)
    {
        var bytes = Encoding.ASCII.GetBytes(s);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string ContentType(string boundary) => $"multipart/form-data; boundary={boundary}";

    [Fact]
    public async Task Same_parts_and_same_boundary_produce_the_same_hash()
    {
        var body1 = BuildMultipartBody("boundaryA", PartOne, PartTwo);
        var body2 = BuildMultipartBody("boundaryA", PartOne, PartTwo);

        var hash1 = await MultipartRequestHasher.ComputeAsync(Body(body1), ContentType("boundaryA"), default);
        var hash2 = await MultipartRequestHasher.ComputeAsync(Body(body2), ContentType("boundaryA"), default);

        hash1.Should().BeEquivalentTo(hash2);

        // Guard against vacuous equality: prove the reader actually parsed
        // sections (an unparsed body would degenerate to hashing empty input
        // and could otherwise agree on both sides without testing anything).
        hash1.Should().NotBeEquivalentTo(ExpectedNoBoundaryMarkerHash());
    }

    [Fact]
    public async Task Same_parts_with_a_different_boundary_produce_the_same_hash()
    {
        var bodyA = BuildMultipartBody("boundaryA", PartOne, PartTwo);
        var bodyB = BuildMultipartBody("totally-different-boundary-42", PartOne, PartTwo);

        var hashA = await MultipartRequestHasher.ComputeAsync(Body(bodyA), ContentType("boundaryA"), default);
        var hashB = await MultipartRequestHasher.ComputeAsync(Body(bodyB), ContentType("totally-different-boundary-42"), default);

        hashA.Should().BeEquivalentTo(hashB);
    }

    [Fact]
    public async Task Changing_the_bytes_of_the_second_part_changes_the_hash()
    {
        var boundary = "boundaryA";
        var original = BuildMultipartBody(boundary, PartOne, PartTwo);
        var changed = BuildMultipartBody(boundary, PartOne, PartTwoChangedBytes);

        var hash1 = await MultipartRequestHasher.ComputeAsync(Body(original), ContentType(boundary), default);
        var hash2 = await MultipartRequestHasher.ComputeAsync(Body(changed), ContentType(boundary), default);

        hash1.Should().NotBeEquivalentTo(hash2);
    }

    [Fact]
    public async Task Swapping_the_order_of_the_two_parts_changes_the_hash()
    {
        var boundary = "boundaryA";
        var inOrder = BuildMultipartBody(boundary, PartOne, PartTwo);
        var swapped = BuildMultipartBody(boundary, PartTwo, PartOne);

        var hash1 = await MultipartRequestHasher.ComputeAsync(Body(inOrder), ContentType(boundary), default);
        var hash2 = await MultipartRequestHasher.ComputeAsync(Body(swapped), ContentType(boundary), default);

        hash1.Should().NotBeEquivalentTo(hash2);
    }

    [Fact]
    public async Task Two_parts_hash_differently_from_only_the_first_part()
    {
        var boundary = "boundaryA";
        var twoParts = BuildMultipartBody(boundary, PartOne, PartTwo);
        var onePart = BuildMultipartBody(boundary, PartOne);

        var hash1 = await MultipartRequestHasher.ComputeAsync(Body(twoParts), ContentType(boundary), default);
        var hash2 = await MultipartRequestHasher.ComputeAsync(Body(onePart), ContentType(boundary), default);

        hash1.Should().NotBeEquivalentTo(hash2);
    }
}
