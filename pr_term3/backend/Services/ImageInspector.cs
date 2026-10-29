using System.Buffers.Binary;

namespace backend.Services;

/// <summary>
/// Nhận diện ảnh theo nội dung thật (magic bytes) thay vì đuôi file / Content-Type do client gửi,
/// đồng thời đọc kích thước từ header – file đổi đuôi (.php → .jpg) hay ảnh hỏng đều bị từ chối.
/// </summary>
public static class ImageInspector
{
    public const int MaxDimension = 20_000;

    public static bool TryInspect(ReadOnlySpan<byte> data, out string extension, out int width, out int height)
    {
        extension = "";
        width = height = 0;
        var ok = data switch
        {
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => TryPng(data, out width, out height),
            [0xFF, 0xD8, 0xFF, ..] => TryJpeg(data, out width, out height),
            [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] =>
                TryWebp(data, out width, out height),
            _ => false,
        };
        if (!ok || width is < 1 or > MaxDimension || height is < 1 or > MaxDimension) return false;

        extension = data[0] switch { 0x89 => ".png", 0xFF => ".jpg", _ => ".webp" };
        return true;
    }

    private static bool TryPng(ReadOnlySpan<byte> d, out int width, out int height)
    {
        width = height = 0;
        // Chunk đầu tiên luôn là IHDR: độ dài (4) + "IHDR" (4) + width (4) + height (4)
        if (d.Length < 24 || !d.Slice(12, 4).SequenceEqual("IHDR"u8)) return false;
        width = BinaryPrimitives.ReadInt32BigEndian(d.Slice(16, 4));
        height = BinaryPrimitives.ReadInt32BigEndian(d.Slice(20, 4));
        return true;
    }

    private static bool TryJpeg(ReadOnlySpan<byte> d, out int width, out int height)
    {
        width = height = 0;
        var i = 2;
        while (i + 9 < d.Length)
        {
            if (d[i] != 0xFF) return false;
            var marker = d[i + 1];
            if (marker == 0xFF) { i++; continue; } // byte đệm
            if (marker is 0xD8 or 0x01 or >= 0xD0 and <= 0xD7) { i += 2; continue; } // marker không có độ dài
            var length = BinaryPrimitives.ReadUInt16BigEndian(d.Slice(i + 2, 2));
            if (length < 2) return false;
            // SOF0..SOF15 (trừ DHT C4, JPG C8, DAC CC) chứa kích thước ảnh
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                height = BinaryPrimitives.ReadUInt16BigEndian(d.Slice(i + 5, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(d.Slice(i + 7, 2));
                return true;
            }
            i += 2 + length;
        }
        return false;
    }

    private static bool TryWebp(ReadOnlySpan<byte> d, out int width, out int height)
    {
        width = height = 0;
        if (d.Length < 30) return false;
        var chunk = d.Slice(12, 4);
        if (chunk.SequenceEqual("VP8 "u8))
        {
            // Lossy: khung khoá có mã bắt đầu 9D 01 2A rồi width/height 14 bit
            if (d[23] != 0x9D || d[24] != 0x01 || d[25] != 0x2A) return false;
            width = BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(26, 2)) & 0x3FFF;
            height = BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(28, 2)) & 0x3FFF;
            return true;
        }
        if (chunk.SequenceEqual("VP8L"u8))
        {
            // Lossless: chữ ký 0x2F rồi (width-1) 14 bit, (height-1) 14 bit
            if (d[20] != 0x2F) return false;
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(d.Slice(21, 4));
            width = (int)(bits & 0x3FFF) + 1;
            height = (int)((bits >> 14) & 0x3FFF) + 1;
            return true;
        }
        if (chunk.SequenceEqual("VP8X"u8))
        {
            // Mở rộng: (width-1), (height-1) mỗi số 24 bit
            width = (d[24] | d[25] << 8 | d[26] << 16) + 1;
            height = (d[27] | d[28] << 8 | d[29] << 16) + 1;
            return true;
        }
        return false;
    }
}
