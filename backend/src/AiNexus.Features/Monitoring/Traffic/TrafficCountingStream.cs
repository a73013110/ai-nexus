namespace AiNexus.Features.Monitoring.Traffic;

/// <summary>在共用邊界統計應用程式的本文位元組數（含串流寫入）；絕不保存內容。</summary>
public sealed class TrafficCountingStream(Stream inner, Action<long>? received = null, Action<long>? written = null) : Stream
{
    // Dispose 不會關閉 inner，它的生命週期由 request/response 的擁有者負責。
    private long readBytes, writtenBytes;
    public long ReadBytes => Interlocked.Read(ref readBytes);
    public long WrittenBytes => Interlocked.Read(ref writtenBytes);
    private void CountRead(int count) { Interlocked.Add(ref readBytes, count); if (count > 0) received?.Invoke(count); }
    private void CountWritten(int count) { Interlocked.Add(ref writtenBytes, count); if (count > 0) written?.Invoke(count); }
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }
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
