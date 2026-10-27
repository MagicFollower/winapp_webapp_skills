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
    public int Capacity => _buffer.Length;

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

    /// <summary>压缩索引时直接搬 UTF-8 字节，避免为 1M 个旧名称再造 1M 个临时 string。</summary>
    public int Add(ReadOnlySpan<byte> utf8, out int length)
    {
        if (utf8.IsEmpty)
        {
            length = 0;
            return Length;
        }

        Ensure(utf8.Length);
        utf8.CopyTo(_buffer.AsSpan(Length));
        int offset = Length;
        Length += utf8.Length;
        length = utf8.Length;
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

    public void Trim()
    {
        int target = Math.Max(16, Length);
        if (_buffer.Length <= target + 1024 * 1024) return;
        Array.Resize(ref _buffer, target);
    }

    private void Ensure(int extra)
    {
        int required = Length + extra;
        if (required <= _buffer.Length) return;

        // 与 FileIndex 的条目数组同口径：25% 增长，避免增量写一条名称就把 arena 整块翻倍。
        int size = _buffer.Length == 0 ? 16 : _buffer.Length + Math.Max(4096, _buffer.Length / 4);
        while (size < required)
        {
            int grow = Math.Max(4096, size / 4);
            if (size > int.MaxValue - grow) { size = required; break; }
            size += grow;
        }
        Array.Resize(ref _buffer, size);
    }
}
