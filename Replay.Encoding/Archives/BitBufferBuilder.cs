namespace Replay.Encoding.Archives;

public sealed class BitBufferBuilder
{
    private byte[] _buffer = [];
    private int _bitLength;

    public int BitLength => _bitLength;

    public void Append(FBitArchive source, int bitCount)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (bitCount < 0)
        {
            throw new ArchiveReadException(ArchiveErrorCode.InvalidBitCount, nameof(Append), source.Position, source.Length, bitCount);
        }

        EnsureCapacity(_bitLength + bitCount);
        for (var i = 0; i < bitCount; i++)
        {
            if (source.ReadBit())
            {
                var targetBit = _bitLength + i;
                _buffer[targetBit >> 3] |= (byte)(1 << (targetBit & 7));
            }
        }

        _bitLength += bitCount;
    }

    public BitArchiveReader BuildReader()
    {
        var byteCount = (_bitLength + 7) / 8;
        var output = new byte[byteCount];
        Array.Copy(_buffer, output, byteCount);
        return new BitArchiveReader(output, _bitLength);
    }

    private void EnsureCapacity(int bitCount)
    {
        var byteCount = (bitCount + 7) / 8;
        if (_buffer.Length >= byteCount)
        {
            return;
        }

        var nextLength = Math.Max(byteCount, Math.Max(8, _buffer.Length * 2));
        Array.Resize(ref _buffer, nextLength);
    }
}
