using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;

namespace backend.Tests;

/// <summary>Quản trị homestay / phòng / ảnh / giá theo ngày (docs/02-nghiep-vu.md §2–4).</summary>
public class CatalogTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("Đà Lạt Pine House", "da-lat-pine-house")]
    [InlineData("  Nhà Gỗ  Đồi Thông – Sương Mù! ", "nha-go-doi-thong-suong-mu")]
    [InlineData("Homestay Hội An 2", "homestay-hoi-an-2")]
    [InlineData("!!!", "homestay")]
    public void Slug_StripsVietnameseDiacritics(string name, string expected) => Assert.Equal(expected, Slug.From(name));

    [Fact]
    public async Task CreateHomestay_GeneratesSlug_AppendsSuffixWhenTaken_AndKeepsDefaults()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var name = $"Nhà Đá Ông Ba {Guid.NewGuid():N}"[..22];

        var first = await TestCatalog.PostAsync<AdminHomestayDto>(admin, "/api/admin/homestays",
            new { name, province = "Lâm Đồng", address = "1 Đường Test", amenities = new[] { "wifi", "BBQ", "wifi" } });
        var second = await TestCatalog.PostAsync<AdminHomestayDto>(admin, "/api/admin/homestays",
            new { name, province = "Lâm Đồng", address = "2 Đường Test" });

        Assert.Equal(Slug.From(name), first.Slug);
        Assert.Equal(first.Slug + "-2", second.Slug);
        Assert.Equal(["bbq", "wifi"], first.Amenities);
        Assert.Equal(("14:00", "12:00", (byte)30, (byte)24, (byte)7, (byte)3, (byte)50),
            (first.CheckInTime, first.CheckOutTime, first.DepositPercent, first.HoldHours, first.FreeCancelDays, first.PartialRefundDays,
                first.PartialRefundPercent));
    }

    [Fact]
    public async Task UpdateHomestay_KeepsSlug_RejectsTakenSlugAndBadPolicy()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var a = await admin.CreateStayAsync();
        var b = await admin.CreateStayAsync();

        var renamed = await admin.Client.PutAsJsonAsync($"/api/admin/homestays/{a.HomestayId}",
            new { name = "Tên Mới Hoàn Toàn", province = "Lâm Đồng", address = "1 Đường Test", status = "hidden" });
        var dto = (await renamed.ReadAsync<AdminHomestayDto>())!;
        Assert.Equal(a.Slug, dto.Slug); // đổi tên không làm hỏng link đã chia sẻ
        Assert.Equal(HomestayStatus.Hidden, dto.Status);

        var taken = await admin.Client.PutAsJsonAsync($"/api/admin/homestays/{a.HomestayId}",
            new { name = "X", slug = b.Slug, province = "Lâm Đồng", address = "1" });
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.NotNull(await taken.FieldErrorAsync("slug"));

        var bad = await admin.Client.PutAsJsonAsync($"/api/admin/homestays/{a.HomestayId}",
            new { name = "X", slug = "Có Dấu", province = "", address = "1", depositPercent = 120, freeCancelDays = 2, partialRefundDays = 5,
                amenities = new[] { "helipad" }, checkInTime = "25:00" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = (await bad.ReadProblemAsync()).GetProperty("errors");
        foreach (var field in new[] { "slug", "province", "depositPercent", "partialRefundDays", "amenities", "checkInTime" })
            Assert.True(errors.TryGetProperty(field, out _), field);
    }

    [Fact]
    public async Task Rooms_ValidateGuestsAndPrices_RejectDuplicateName()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var stay = await admin.CreateStayAsync();

        var bad = await admin.Client.PostAsJsonAsync($"/api/admin/homestays/{stay.HomestayId}/rooms",
            new { name = "Phòng Lỗi", baseGuests = 4, maxGuests = 2, basePrice = 0, weekendPrice = 1.5m, minNights = 31 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = (await bad.ReadProblemAsync()).GetProperty("errors");
        foreach (var field in new[] { "maxGuests", "basePrice", "weekendPrice", "minNights" })
            Assert.True(errors.TryGetProperty(field, out _), field);

        var duplicate = await admin.Client.PostAsJsonAsync($"/api/admin/homestays/{stay.HomestayId}/rooms", TestCatalog.DefaultRoom());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.NotNull(await duplicate.FieldErrorAsync("name"));
    }

    [Fact]
    public async Task CatalogWrites_AdminOnly_StaffCanRead()
    {
        var staff = await factory.CreateStaffAsync();
        var guest = await factory.CreateGuestAsync();
        var body = new { name = "Không được tạo", province = "Lâm Đồng", address = "1" };

        Assert.Equal(HttpStatusCode.OK, (await staff.Client.GetAsync("/api/admin/homestays")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.Client.PostAsJsonAsync("/api/admin/homestays", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.Client.GetAsync("/api/admin/rates?roomIds=1&from=2030-01-01&to=2030-01-02")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.Client.GetAsync("/api/admin/homestays")).StatusCode);
    }

    [Fact]
    public async Task Delete_OnlyWithoutBookings_OtherwiseConflict()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var unused = await admin.CreateStayAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.Client.DeleteAsync($"/api/admin/homestays/{unused.HomestayId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.GetAsync($"/api/admin/homestays/{unused.HomestayId}")).StatusCode);

        var used = await admin.CreateStayAsync();
        var guest = await factory.CreateGuestAsync();
        var checkIn = factory.Today.AddDays(20);
        var booked = await guest.Client.PostAsJsonAsync("/api/bookings", new
        {
            roomId = used.RoomId, checkIn, checkOut = checkIn.AddDays(1), guests = 2, guestName = "Khách", guestPhone = "0912345678",
        });
        booked.EnsureSuccessStatusCode();

        var deleteHomestay = await admin.Client.DeleteAsync($"/api/admin/homestays/{used.HomestayId}");
        Assert.Equal(HttpStatusCode.Conflict, deleteHomestay.StatusCode);
        Assert.Contains("hãy ẩn", await deleteHomestay.DetailAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Client.DeleteAsync($"/api/admin/rooms/{used.RoomId}")).StatusCode);
    }

    [Fact]
    public async Task Photos_ChecksMagicBytes_ServesWithCacheHeaders_ReorderAndDelete()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var stay = await admin.CreateStayAsync();

        var first = await UploadAsync(admin, stay.HomestayId, TestCatalog.Png(1200, 800), "anh-1.png");
        var second = await UploadAsync(admin, stay.HomestayId, TestCatalog.Png(640, 480), "anh-2.png");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var photo = (await first.ReadAsync<PhotoDto>())!;
        var photo2 = (await second.ReadAsync<PhotoDto>())!;
        Assert.Equal((1200, 800, 0), (photo.Width, photo.Height, photo.SortOrder));
        Assert.Matches($"^/uploads/{stay.HomestayId}/[0-9a-f]{{32}}\\.png$", photo.Url);

        var served = await factory.CreateRawClient().GetAsync(photo.Url);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
        Assert.Contains("immutable", served.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", served.Headers.GetValues("X-Content-Type-Options").Single());

        // Script đổi đuôi .jpg → bị từ chối theo nội dung thật
        var fake = await UploadAsync(admin, stay.HomestayId, "<?php system($_GET['c']); ?>"u8.ToArray(), "shell.jpg", "image/jpeg");
        Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);
        Assert.NotNull(await fake.FieldErrorAsync("file"));

        var reordered = await admin.Client.PutAsJsonAsync($"/api/admin/homestays/{stay.HomestayId}/photos/order",
            new { photoIds = new[] { photo2.Id, photo.Id } });
        var order = (await reordered.ReadAsync<List<PhotoDto>>())!;
        Assert.Equal([photo2.Id, photo.Id], order.Select(p => p.Id));

        var detail = await factory.CreateRawClient().GetJsonAsync<StayDetailDto>($"/api/stays/{stay.Slug}");
        Assert.Equal(photo2.Url, detail!.Photos[0].Url); // ảnh bìa mới

        Assert.Equal(HttpStatusCode.NoContent, (await admin.Client.DeleteAsync($"/api/admin/homestays/{stay.HomestayId}/photos/{photo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateRawClient().GetAsync(photo.Url)).StatusCode);
    }

    [Fact]
    public async Task Rates_SetListClear_ForSeveralRooms()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var stay = await admin.CreateStayAsync();
        var room2 = await admin.AddRoomAsync(stay.HomestayId, TestCatalog.DefaultRoom("Phòng Hai"));
        var from = factory.Today.AddDays(40);
        var to = from.AddDays(2);

        var set = await admin.Client.PutAsJsonAsync("/api/admin/rates",
            new { roomIds = new[] { stay.RoomId, room2.Id }, from, to, price = 2_000_000, note = "Lễ" });
        Assert.Equal(6, (await set.ReadAsync<RatesChangedDto>())!.Count);

        var list = await admin.Client.GetJsonAsync<List<RoomRatesDto>>(
            $"/api/admin/rates?roomIds={stay.RoomId},{room2.Id}&from={from:yyyy-MM-dd}&to={to.AddDays(1):yyyy-MM-dd}");
        Assert.Equal(2, list!.Count);
        Assert.All(list, r => Assert.Equal(4, r.Days.Count));
        Assert.All(list, r => Assert.All(r.Days.Take(3), d => Assert.Equal((2_000_000m, PriceSource.Override, "Lễ"), (d.Price, d.Source, d.Note))));
        Assert.NotEqual(PriceSource.Override, list[0].Days[3].Source);

        var cleared = await admin.Client.DeleteAsync($"/api/admin/rates?roomIds={stay.RoomId}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        Assert.Equal(3, (await cleared.ReadAsync<RatesChangedDto>())!.Count);
    }

    private static Task<HttpResponseMessage> UploadAsync(TestUser admin, long homestayId, byte[] content, string fileName, string type = "image/png")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(file, "file", fileName);
        return admin.Client.PostAsync($"/api/admin/homestays/{homestayId}/photos", form);
    }
}
