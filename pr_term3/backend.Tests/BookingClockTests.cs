using System.Net;
using System.Net.Http.Json;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

/// <summary>
/// Test tua đồng hồ giả: chạy tuần tự, SAU các test song song (hết hạn giữ chỗ xử lý mọi đặt phòng quá hạn trong DB test chung).
/// Mỗi test 1 ApiFactory riêng; chỉ dùng token cấp TRƯỚC khi tua (token cấp sau có nbf ở "tương lai" so với giờ thật).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ClockCollection
{
    public const string Name = "Đồng hồ giả";
}

[Collection(ClockCollection.Name)]
public class BookingClockTests
{
    /// <summary>DoD Phase 3: đặt online → hết hạn sau 24 giờ → phòng trống lại.</summary>
    [Fact]
    public async Task OnlineBooking_ExpiresAfter24Hours_RoomAvailableAgain()
    {
        await using var factory = new ApiFactory();
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var stay = await admin.CreateStayAsync();
        var guest = await factory.CreateGuestAsync();
        var checkIn = factory.Today.AddDays(10);
        var booking = (await (await guest.Client.PostAsJsonAsync("/api/bookings", BookingTests.Booking(stay.RoomId, checkIn)))
            .ReadAsync<BookingDto>())!;

        factory.Time.Advance(TimeSpan.FromHours(23));
        await ExpireAsync(factory);
        Assert.Equal(BookingStatus.Pending, (await guest.Client.GetJsonAsync<BookingDto>($"/api/bookings/{booking.Code}"))!.Status);

        factory.Time.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));
        Assert.True(await ExpireAsync(factory) >= 1);

        var expired = await guest.Client.GetJsonAsync<BookingDto>($"/api/bookings/{booking.Code}");
        Assert.Equal(BookingStatus.Expired, expired!.Status);
        Assert.Contains(expired.Events, e => e is { FromStatus: BookingStatus.Pending, ToStatus: BookingStatus.Expired });
        Assert.Equal(0, await factory.WithDbAsync(db => db.RoomNights.CountAsync(n => n.BookingId == booking.Id)));

        var quote = await factory.CreateRawClient().PostAsJsonAsync("/api/quotes",
            new { roomId = stay.RoomId, checkIn, checkOut = checkIn.AddDays(2), guests = 2 });
        Assert.True((await quote.ReadAsync<QuoteDto>())!.Available);

        var lateDeposit = await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{booking.Id}/payments",
            new { kind = "deposit", method = "bank_transfer", amount = 500_000 });
        Assert.Equal(HttpStatusCode.Conflict, lateDeposit.StatusCode);
    }

    [Fact]
    public async Task NoShow_DayAfterArrival_KeepsDeposit_ReleasesNights()
    {
        await using var factory = new ApiFactory();
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var stay = await admin.CreateStayAsync(room: TestCatalog.FlatRoom());
        var booking = await CreateWalkInAsync(factory, staff, stay, nights: 2, paid: 700_000);

        factory.Time.Advance(TimeSpan.FromHours(25));
        var noShow = await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{booking.Id}/no-show", new { reason = "Không liên lạc được" });
        var dto = (await noShow.ReadAsync<BookingDto>())!;

        Assert.Equal((BookingStatus.NoShow, 700_000m, 0m), (dto.Status, dto.Money.Paid, dto.Money.AmountDue));
        Assert.Equal(0, await factory.WithDbAsync(db => db.RoomNights.CountAsync(n => n.BookingId == booking.Id)));
    }

    [Fact]
    public async Task EarlyCheckOut_ReleasesRemainingNights_CalendarBarEndsOnDepartureDay()
    {
        await using var factory = new ApiFactory();
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var stay = await admin.CreateStayAsync(room: TestCatalog.FlatRoom());
        var arrival = factory.Today;
        var booking = await CreateWalkInAsync(factory, staff, stay, nights: 3, paid: 3_400_000);
        (await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{booking.Id}/check-in", new { })).EnsureSuccessStatusCode();

        factory.Time.Advance(TimeSpan.FromHours(25));
        var departure = factory.Today;
        var checkOut = await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{booking.Id}/check-out", new { });
        var dto = (await checkOut.ReadAsync<BookingDto>())!;
        Assert.Equal(BookingStatus.CheckedOut, dto.Status);
        Assert.Contains(dto.Events, e => e.Message?.Contains("sớm") == true);

        var calendar = await staff.Client.GetJsonAsync<AdminCalendarDto>(
            $"/api/admin/calendar?homestayId={stay.HomestayId}&from={arrival:yyyy-MM-dd}&to={arrival.AddDays(5):yyyy-MM-dd}");
        Assert.Equal(departure, Assert.Single(Assert.Single(calendar!.Rooms).Bookings).CheckOut);

        var free = await factory.CreateRawClient().PostAsJsonAsync("/api/quotes",
            new { roomId = stay.RoomId, checkIn = departure, checkOut = arrival.AddDays(3), guests = 2 });
        Assert.True((await free.ReadAsync<QuoteDto>())!.Available);
    }

    [Fact]
    public async Task DeletePayment_StaffWithin24Hours_AdminAnytime()
    {
        await using var factory = new ApiFactory();
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var stay = await admin.CreateStayAsync(room: TestCatalog.FlatRoom());
        var booking = await CreateWalkInAsync(factory, staff, stay, nights: 1, paid: 400_000);
        var paymentId = booking.Payments.Single().Id;

        factory.Time.Advance(TimeSpan.FromHours(25));
        var byStaff = await staff.Client.DeleteAsync($"/api/admin/bookings/{booking.Id}/payments/{paymentId}");
        Assert.Equal(HttpStatusCode.Conflict, byStaff.StatusCode);
        var byAdmin = await admin.Client.DeleteAsync($"/api/admin/bookings/{booking.Id}/payments/{paymentId}");
        Assert.Equal(HttpStatusCode.OK, byAdmin.StatusCode);
    }

    private static Task<BookingDto> CreateWalkInAsync(ApiFactory factory, TestUser staff, TestStay stay, int nights, decimal paid) =>
        TestCatalog.PostAsync<BookingDto>(staff, "/api/admin/bookings", new
        {
            roomId = stay.RoomId, checkIn = factory.Today, checkOut = factory.Today.AddDays(nights), guests = 2,
            guestName = "Khách Vãng Lai", guestPhone = "0905123456", source = "walk_in",
            payment = new { kind = "deposit", method = "cash", amount = paid },
        });

    private static Task<int> ExpireAsync(ApiFactory factory) =>
        factory.WithServiceAsync<BookingService, int>(s => s.ExpireDueAsync(CancellationToken.None));
}
