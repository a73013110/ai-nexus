namespace AiNexus.Features.Monitoring;

/// <summary>Counts application body bytes at the shared boundary, including streamed writes. It never retains content.</summary>
public sealed class TrafficCountingStream(Stream inner, Action<long>? received = null, Action<long>? written = null) : Stream
{
    // Stream's own Dispose leaves `inner` open: the request/response feature owns it.
    private long read, sent;
    public long ReadBytes => Interlocked.Read(ref read);
    public long WrittenBytes => Interlocked.Read(ref sent);
    private void CountRead(int count) { Interlocked.Add(ref read, count); if (count > 0) received?.Invoke(count); }
    private void CountWritten(int count) { Interlocked.Add(ref sent, count); if (count > 0) written?.Invoke(count); }
    public override bool CanRead => inner.CanRead; public override bool CanSeek => inner.CanSeek; public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length; public override long Position { get => inner.Position; set => inner.Position = value; }
    public override void Flush() => inner.Flush();
    public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => inner.SetLength(value);
    public override int Read(byte[] buffer, int offset, int count) { var n = inner.Read(buffer, offset, count); CountRead(n); return n; }
    public override int Read(Span<byte> buffer) { var n = inner.Read(buffer); CountRead(n); return n; }
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) { var n = await inner.ReadAsync(buffer.AsMemory(offset, count), ct); CountRead(n); return n; }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) { var n = await inner.ReadAsync(buffer, ct); CountRead(n); return n; }
    public override void Write(byte[] buffer, int offset, int count) { inner.Write(buffer, offset, count); CountWritten(count); }
    public override void Write(ReadOnlySpan<byte> buffer) { inner.Write(buffer); CountWritten(buffer.Length); }
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) { await inner.WriteAsync(buffer.AsMemory(offset, count), ct); CountWritten(count); }
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) { await inner.WriteAsync(buffer, ct); CountWritten(buffer.Length); }
}
