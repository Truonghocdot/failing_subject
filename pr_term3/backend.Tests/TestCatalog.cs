using System.Net.Http.Json;
using backend.Contracts;

namespace backend.Tests;

public sealed record TestStay(long HomestayId, string Slug, long RoomId);

/// <summary>
/// Mỗi test tự tạo homestay + phòng riêng (tên / tỉnh ngẫu nhiên) qua API quản trị → chạy song song không giẫm dữ liệu nhau.
/// Phòng mặc định giống DoD Phase 2: 800 000 / cuối tuần 1 000 000, phụ thu 150 000 / khách vượt 2, phí dọn 100 000.
/// </summary>
public static class TestCatalog
{
    public static object DefaultRoom(string name = "Phòng Đồi Thông") => new
    {
        name, baseGuests = 2, maxGuests = 4, basePrice = 800_000, weekendPrice = 1_000_000, extraGuestFee = 150_000, cleaningFee = 100_000,
    };

    /// <summary>DoD Phase 3: 3 đêm × 1 100 000 + 3 khách (phụ thu 1 × 150 000 × 3) + phí dọn 100 000 = 3 850 000 – cọc 30% = 1 155 000.</summary>
    public static object FlatRoom(string name = "Phòng Giá Phẳng") => new
    {
        name, baseGuests = 2, maxGuests = 4, basePrice = 1_100_000, extraGuestFee = 150_000, cleaningFee = 100_000,
    };

    public static string NewProvince() => $"Tỉnh {Guid.NewGuid():N}"[..20];

    public static async Task<TestStay> CreateStayAsync(
        this TestUser admin, object? room = null, string? province = null, int depositPercent = 30, string[]? amenities = null,
        string status = "active", string? name = null)
    {
        var homestay = await PostAsync<AdminHomestayDto>(admin, "/api/admin/homestays", new
        {
            name = name ?? $"Homestay {Guid.NewGuid():N}"[..20],
            province = province ?? NewProvince(),
            address = "1 Đường Test",
            depositPercent,
            amenities = amenities ?? ["wifi"],
            status,
        });
        var created = await PostAsync<RoomDto>(admin, $"/api/admin/homestays/{homestay.Id}/rooms", room ?? DefaultRoom());
        return new TestStay(homestay.Id, homestay.Slug, created.Id);
    }

    public static async Task<RoomDto> AddRoomAsync(this TestUser admin, long homestayId, object room) =>
        await PostAsync<RoomDto>(admin, $"/api/admin/homestays/{homestayId}/rooms", room);

    public static async Task<T> PostAsync<T>(TestUser user, string url, object body)
    {
        var response = await user.Client.PostAsJsonAsync(url, body);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"POST {url} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.ReadAsync<T>())!;
    }

    public static DateOnly Next(this DateOnly from, DayOfWeek day)
    {
        var d = from.AddDays(1);
        while (d.DayOfWeek != day) d = d.AddDays(1);
        return d;
    }

    /// <summary>PNG hợp lệ tối thiểu (chữ ký + IHDR) – đủ để kiểm tra magic bytes và đọc kích thước.</summary>
    public static byte[] Png(int width, int height)
    {
        byte[] bytes =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R',
            (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
            (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
            8, 2, 0, 0, 0, 0, 0, 0, 0,
        ];
        return bytes;
    }
}
