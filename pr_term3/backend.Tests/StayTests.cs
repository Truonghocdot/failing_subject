using System.Net;
using System.Net.Http.Json;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;

namespace backend.Tests;

/// <summary>Giá từng đêm, báo giá, tìm kiếm, lịch trống (docs/02-nghiep-vu.md §4–5).</summary>
public class StayTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static Room DodRoom() => new()
    {
        Name = "Phòng Đồi Thông", BaseGuests = 2, MaxGuests = 4,
        BasePrice = 800_000, WeekendPrice = 1_000_000, ExtraGuestFee = 150_000, CleaningFee = 100_000,
    };

    /// <summary>DoD Phase 2 – đúng ngày trong tài liệu: thứ Năm 31/12/2026 → Chủ nhật 03/01/2027, 3 khách, 01/01 giá riêng 1 500 000.</summary>
    [Fact]
    public void Pricing_DodExample_ThursdayToSunday_WithNewYearOverride()
    {
        var checkIn = new DateOnly(2026, 12, 31);
        Assert.Equal(DayOfWeek.Thursday, checkIn.DayOfWeek);
        var overrides = new Dictionary<DateOnly, decimal> { [new DateOnly(2027, 1, 1)] = 1_500_000 };

        var price = Pricing.Calculate(DodRoom(), 30, checkIn, new DateOnly(2027, 1, 3), 3, overrides);

        Assert.Equal([(800_000m, PriceSource.Base), (1_500_000m, PriceSource.Override), (1_000_000m, PriceSource.Weekend)],
            price.Nights.Select(n => (n.Price, n.Source)));
        Assert.Equal(3_300_000m, price.RoomTotal);
        Assert.Equal(450_000m, price.ExtraGuestTotal);
        Assert.Equal(3_850_000m, price.Total);
        Assert.Equal(1_155_000m, price.DepositAmount);
    }

    [Theory]
    [InlineData(3_850_000, 30, 1_155_000)]
    [InlineData(1_234_567, 30, 371_000)] // 370 370,1 → làm tròn lên hàng nghìn
    [InlineData(1_500, 100, 1_500)] // không vượt tổng tiền
    [InlineData(2_000_000, 0, 0)]
    public void Deposit_RoundsUpToThousand_NeverAboveTotal(int total, int percent, int expected) =>
        Assert.Equal(expected, Pricing.Deposit(total, percent));

    [Fact]
    public async Task Quote_ViaApi_FridayOverride_SaturdayWeekend_ExtraGuests()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var stay = await admin.CreateStayAsync();
        var thursday = factory.Today.AddDays(7).Next(DayOfWeek.Thursday);
        var friday = thursday.AddDays(1);
        (await admin.Client.PutAsJsonAsync("/api/admin/rates",
            new { roomIds = new[] { stay.RoomId }, from = friday, to = friday, price = 1_500_000, note = "Lễ" })).EnsureSuccessStatusCode();

        var quote = await QuoteAsync(stay.RoomId, thursday, thursday.AddDays(3), 3);

        Assert.Equal([PriceSource.Base, PriceSource.Override, PriceSource.Weekend], quote.NightPrices.Select(n => n.Source));
        Assert.Equal((3_850_000m, 1_155_000m, (byte)30, 3, true), (quote.Total, quote.DepositAmount, quote.DepositPercent, quote.Nights, quote.Available));
    }

    [Fact]
    public async Task Quote_InvalidDatesGuestsMinNights_400WithFieldErrors()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var stay = await admin.CreateStayAsync(room: new { name = "Phòng 2 đêm", baseGuests = 2, maxGuests = 2, minNights = 2, basePrice = 500_000 });
        var anon = factory.CreateRawClient();
        var today = factory.Today;

        async Task<(HttpStatusCode, string?, string?)> Try(object body, string field)
        {
            var r = await anon.PostAsJsonAsync("/api/quotes", body);
            return (r.StatusCode, r.StatusCode == HttpStatusCode.BadRequest ? await r.FieldErrorAsync(field) : null, field);
        }

        var past = await Try(new { roomId = stay.RoomId, checkIn = today.AddDays(-1), checkOut = today.AddDays(2), guests = 2 }, "checkIn");
        var reversed = await Try(new { roomId = stay.RoomId, checkIn = today.AddDays(5), checkOut = today.AddDays(5), guests = 2 }, "checkOut");
        var tooLong = await Try(new { roomId = stay.RoomId, checkIn = today.AddDays(1), checkOut = today.AddDays(32), guests = 2 }, "checkOut");
        var tooMany = await Try(new { roomId = stay.RoomId, checkIn = today.AddDays(1), checkOut = today.AddDays(3), guests = 3 }, "guests");
        var tooShort = await Try(new { roomId = stay.RoomId, checkIn = today.AddDays(1), checkOut = today.AddDays(2), guests = 2 }, "checkOut");

        Assert.Equal("Ngày nhận phòng không được ở quá khứ", past.Item2);
        Assert.Equal("Ngày trả phòng phải sau ngày nhận phòng", reversed.Item2);
        Assert.Equal("Mỗi lần đặt tối đa 30 đêm", tooLong.Item2);
        Assert.Equal("Phòng này tối đa 2 khách", tooMany.Item2);
        Assert.Equal("Phòng này cần đặt tối thiểu 2 đêm", tooShort.Item2);
    }

    [Fact]
    public async Task Search_ExcludesHiddenAndFullyBlocked_FiltersGuestsAmenitiesPrice()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var province = TestCatalog.NewProvince();
        var open = await admin.CreateStayAsync(province: province, amenities: ["wifi", "pool"], name: $"Mở {Guid.NewGuid():N}"[..15]);
        var hidden = await admin.CreateStayAsync(province: province, status: "hidden");
        var full = await admin.CreateStayAsync(province: province, amenities: ["wifi"]);
        var checkIn = factory.Today.AddDays(30);
        (await staff.Client.PostAsJsonAsync("/api/admin/blocks",
            new { roomId = full.RoomId, from = checkIn, to = checkIn.AddDays(1), reason = "Sửa điện" })).EnsureSuccessStatusCode();

        var noDates = await SearchAsync($"province={Uri.EscapeDataString(province)}");
        Assert.Equal([open.HomestayId, full.HomestayId], noDates.Items.Select(i => i.Id).Order());
        Assert.All(noDates.Items, i => Assert.Null(i.AvailableRooms));

        var dates = $"province={Uri.EscapeDataString(province)}&checkIn={checkIn:yyyy-MM-dd}&checkOut={checkIn.AddDays(2):yyyy-MM-dd}";
        var withDates = await SearchAsync(dates);
        var card = Assert.Single(withDates.Items);
        Assert.Equal(open.HomestayId, card.Id);
        Assert.Equal(1, card.AvailableRooms);
        // 2 đêm (giá tuỳ thứ) + phí dọn 100 000, chia số đêm
        Assert.InRange(card.PriceFrom, 850_000m, 1_050_000m);

        Assert.Empty((await SearchAsync(dates + "&guests=5")).Items); // phòng tối đa 4 khách
        Assert.Single((await SearchAsync(dates + "&guests=3")).Items);
        Assert.Single((await SearchAsync($"province={Uri.EscapeDataString(province)}&amenities=pool")).Items);
        Assert.Empty((await SearchAsync($"province={Uri.EscapeDataString(province)}&amenities=pool,bbq")).Items);
        Assert.Empty((await SearchAsync(dates + "&priceMax=500000")).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateRawClient().GetAsync($"/api/stays/{hidden.Slug}")).StatusCode);

        var bad = await factory.CreateRawClient().GetAsync($"/api/stays?checkIn={checkIn:yyyy-MM-dd}&amenities=helipad&sort=cheap");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = (await bad.ReadProblemAsync()).GetProperty("errors");
        foreach (var field in new[] { "checkOut", "amenities", "sort" }) Assert.True(errors.TryGetProperty(field, out _), field);
    }

    [Fact]
    public async Task Provinces_ListOnlyOpenHomestays()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var province = TestCatalog.NewProvince();
        var hiddenProvince = TestCatalog.NewProvince();
        await admin.CreateStayAsync(province: province);
        await admin.CreateStayAsync(province: province);
        await admin.CreateStayAsync(province: hiddenProvince, status: "hidden");

        var provinces = await factory.CreateRawClient().GetJsonAsync<List<ProvinceDto>>("/api/provinces");
        Assert.Contains(new ProvinceDto(province, 2), provinces!);
        Assert.DoesNotContain(provinces!, p => p.Province == hiddenProvince);
    }

    [Fact]
    public async Task Calendar_ShowsBlockedNightsAndPrices()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var stay = await admin.CreateStayAsync();
        var from = factory.Today.AddDays(3);
        (await admin.Client.PostAsJsonAsync("/api/admin/blocks",
            new { roomId = stay.RoomId, from = from.AddDays(1), to = from.AddDays(2), reason = "Sơn lại" })).EnsureSuccessStatusCode();

        var calendar = await factory.CreateRawClient().GetJsonAsync<StayCalendarDto>(
            $"/api/stays/{stay.Slug}/calendar?from={from:yyyy-MM-dd}&to={from.AddDays(6):yyyy-MM-dd}");

        var days = Assert.Single(calendar!.Rooms).Days;
        Assert.Equal(7, days.Count);
        Assert.Equal([true, false, false, true], days.Take(4).Select(d => d.Available));
        Assert.All(days, d => Assert.Equal(Pricing.IsWeekendNight(d.Date) ? 1_000_000m : 800_000m, d.Price));

        var tooLong = await factory.CreateRawClient().GetAsync($"/api/stays/{stay.Slug}/calendar?from={from:yyyy-MM-dd}&to={from.AddDays(70):yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    private async Task<QuoteDto> QuoteAsync(long roomId, DateOnly checkIn, DateOnly checkOut, int guests)
    {
        var response = await factory.CreateRawClient().PostAsJsonAsync("/api/quotes", new { roomId, checkIn, checkOut, guests });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadAsync<QuoteDto>())!;
    }

    private async Task<PagedResult<StayCardDto>> SearchAsync(string query) =>
        (await factory.CreateRawClient().GetJsonAsync<PagedResult<StayCardDto>>($"/api/stays?{query}"))!;
}
