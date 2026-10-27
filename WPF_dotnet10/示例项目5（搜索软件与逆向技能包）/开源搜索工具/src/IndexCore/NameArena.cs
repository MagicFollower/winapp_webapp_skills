using System.Text;

namespace IndexCore;

/// <summary>UTF-8 名称的连续存储；条目只保存偏移/长度，1M 文件不会产生 1M 个字符串对象。</summary>
internal sealed class NameArena
{
    private byte[] _buffer;

    public NameArena(int capacity = 1 << 16)
    {
        _buffer = new byte[Math.Max(16, capacity)];
    }

    public NameArena(byte[] buffer)
    {
        _buffer = buffer ?? Array.Empty<byte>();
        Length = _buffer.Length;
    }

    public int Length { get; private set; }
    public byte[] Buffer => _buffer;

    public int Add(string value, out int length)
    {
        if (string.IsNullOrEmpty(value))
        {
            length = 0;
            return Length;
        }

        int count = Encoding.UTF8.GetByteCount(value);
        Ensure(count);
        Encoding.UTF8.GetBytes(value, 0, value.Length, _buffer, Length);
        int offset = Length;
        Length += count;
        length = count;
        return offset;
    }

    public ReadOnlySpan<byte> Get(int offset, int length)
    {
        if (offset < 0 || length <= 0 || offset + length > Length) return ReadOnlySpan<byte>.Empty;
        return _buffer.AsSpan(offset, length);
    }

    public string GetString(int offset, int length)
    {
        var span = Get(offset, length);
        return span.IsEmpty ? string.Empty : Encoding.UTF8.GetString(span);
    }

    private void Ensure(int extra)
    {
        int required = Length + extra;
        if (required <= _buffer.Length) return;

        int size = _buffer.Length;
        while (size < required) size = size <= int.MaxValue / 2 ? size * 2 : required;
        Array.Resize(ref _buffer, size);
    }
}
