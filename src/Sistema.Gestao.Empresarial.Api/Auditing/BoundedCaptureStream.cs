namespace Sistema.Gestao.Empresarial.Api.Auditing;

internal sealed class BoundedCaptureStream(Stream inner, int maximumBytes) : Stream
{
    private readonly MemoryStream _capture = new(Math.Min(maximumBytes, 8192));
    private readonly object _sync = new();

    public bool IsTruncated { get; private set; }
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }

    public byte[] GetCapturedBytes()
    {
        lock (_sync)
            return _capture.ToArray();
    }

    public override void Flush() => inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
    {
        Capture(buffer.AsSpan(offset, count));
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Capture(buffer);
        inner.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Capture(buffer.AsSpan(offset, count));
        return inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Capture(buffer.Span);
        return inner.WriteAsync(buffer, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _capture.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _capture.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private void Capture(ReadOnlySpan<byte> bytes)
    {
        lock (_sync)
        {
            var remaining = maximumBytes - (int)_capture.Length;
            if (remaining > 0)
                _capture.Write(bytes[..Math.Min(remaining, bytes.Length)]);
            if (bytes.Length > remaining)
                IsTruncated = true;
        }
    }
}
