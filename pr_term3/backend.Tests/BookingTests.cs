using System.Net;
using System.Net.Http.Json;
using backend.Contracts;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

/// <summary>Đặt phòng, khoá đêm, vòng đời, cọc – huỷ – hoàn, khoá phòng (docs/02-nghiep-vu.md §6–9).</summary>
public class BookingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    public static object Booking(long roomId, DateOnly checkIn, int nights = 2, int guests = 2, decimal? expectedTotal = null) => new
    {
        roomId, checkIn, checkOut = checkIn.AddDays(nights), guests, guestName = "Nguyễn Văn Khách", guestPhone = "+84 912 345 678",
        expectedTotal,
    };

    [Fact]
    public async Task ParallelRequests_SameRoomSameNights_ExactlyOneSucceeds()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var stay = await admin.CreateStayAsync();
        var guests = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => factory.CreateGuestAsync()));
        var checkIn = factory.Today.AddDays(15);

        // Mỗi khách chọn khoảng ngày chồng lên nhau (cùng đêm checkIn + 1) → chỉ 1 người được
        var responses = await Task.WhenAll(guests.Select((g, i) =>
            g.Client.PostAsJsonAsync("/api/bookings", Booking(stay.RoomId, checkIn.AddDays(i % 2), nights: 2))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Contains("vừa có người đặt", await responses.First(r => r.StatusCode == HttpStatusCode.Conflict).DetailAsync());
        var nights = await factory.WithDbAsync(db => db.RoomNights.CountAsync(n => n.RoomId == stay.RoomId));
        Assert.Equal(2, nights);
    }

    [Fact]
    public async Task GuestBooking_PendingWithHold_TransferInfo_CancelReleasesNights_RebookWorks()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var stay = await admin.CreateStayAsync();
        var guest = await factory.CreateGuestAsync();
        var checkIn = factory.Today.AddDays(12);

        var created = await guest.Client.PostAsJsonAsync("/api/bookings", Booking(stay.RoomId, checkIn));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = (await created.ReadAsync<BookingDto>())!;
        Assert.Matches("^HS-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}$", booking.Code);
        Assert.Equal((BookingStatus.Pending, BookingSource.Web, "0912345678", guest.Auth.User.Email),
            (booking.Status, booking.Source, booking.GuestPhone, booking.GuestEmail));
        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime.AddHours(24), booking.HoldUntil!.Value, TimeSpan.FromSeconds(5));
        Assert.Equal((booking.Code, booking.DepositAmount), (booking.Transfer!.Content, booking.Transfer.Amount));
        Assert.Null(booking.InternalNote);

        var quote = await factory.CreateRawClient().PostAsJsonAsync("/api/quotes",
            new { roomId = stay.RoomId, checkIn, checkOut = checkIn.AddDays(2), guests = 2 });
        Assert.False((await quote.ReadAsync<QuoteDto>())!.Available);

        var mine = await guest.Client.GetJsonAsync<List<BookingSummaryDto>>("/api/bookings?status=pending");
        Assert.Equal(booking.Code, Assert.Single(mine!).Code);

        var preview = await guest.Client.GetJsonAsync<CancelPreviewDto>($"/api/bookings/{booking.Code}/cancel-preview");
        Assert.Equal((true, 0m, 0m), (preview!.CanCancel, preview.Paid, preview.RefundAmount)); // chưa trả gì → huỷ miễn phí
        var cancelled = await guest.Client.PostAsJsonAsync($"/api/bookings/{booking.Code}/cancel", new { reason = "Đổi kế hoạch" });
        Assert.Equal(BookingStatus.Cancelled, (await cancelled.ReadAsync<BookingDto>())!.Status);

        var other = await factory.CreateGuestAsync();
        Assert.Equal(HttpStatusCode.Created, (await other.Client.PostAsJsonAsync("/api/bookings", Booking(stay.RoomId, checkIn))).StatusCode);
    }

    [Fact]
    public async Task PriceChanged_Returns409WithNewQuote()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var stay = await admin.CreateStayAsync(room: TestCatalog.FlatRoom());
        var guest = await factory.CreateGuestAsync();

        var response = await guest.Client.PostAsJsonAsync("/api/bookings",
            Booking(stay.RoomId, factory.Today.AddDays(9), nights: 2, expectedTotal: 2_000_000));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.StartsWith("Giá đã thay đổi", problem.GetProperty("detail").GetString());
        Assert.Equal(2_300_000m, problem.GetProperty("quote").GetProperty("total").GetDecimal());

        var ok = await guest.Client.PostAsJsonAsync("/api/bookings", Booking(stay.RoomId, factory.Today.AddDays(9), nights: 2, expectedTotal: 2_300_000));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }

    /// <summary>DoD Phase 3: đã cọc 1 155 000, huỷ trước 5 ngày → hoàn 50% = 577 500.</summary>
    [Fact]
    public async Task Cancel_FiveDaysBefore_AfterDeposit_Refunds577500_ThenRefundRecorded()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var stay = await admin.CreateStayAsync(room: TestCatalog.FlatRoom());
        var guest = await factory.CreateGuestAsync();

        var booking = (await (await guest.Client.PostAsJsonAsync("/api/bookings",
            Booking(stay.RoomId, factory.Today.AddDays(5), nights: 3, guests: 3))).ReadAsync<BookingDto>())!;
        Assert.Equal((3_850_000m, 1_155_000m), (booking.Total, booking.DepositAmount));

        var paid = await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{booking.Id}/payments",
            new { kind = "deposit", method = "bank_transfer", amount = 1_155_000, note = "CK Vietcombank" });
        var afterDeposit = (await paid.ReadAsync<BookingDto>())!;
        Assert.Equal(BookingStatus.Confirmed, afterDeposit.Status); // đủ cọc → tự xác nhận
        Assert.Equal((PaymentStatus.DepositPaid, 2_695_000m), (afterDeposit.Money.Status, afterDeposit.Money.AmountDue));
        Assert.Null(afterDeposit.Transfer);

        var preview = await guest.Client.GetJsonAsync<CancelPreviewDto>($"/api/bookings/{booking.Code}/cancel-preview");
        Assert.Equal((true, 5, 50, 577_500m), (preview!.CanCancel, preview.DaysBefore, preview.RefundPercent, preview.RefundAmount));

        var cancelled = (await (await guest.Client.PostAsJsonAsync($"/api/bookings/{booking.Code}/cancel", new { })).ReadAsync<BookingDto>())!;
        Assert.Equal((BookingStatus.Cancelled, 577_500m, 577_500m), (cancelled.Status, cancelled.RefundAmount, cancelled.Money.PendingRefund));

        var tooMuch = await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{booking.Id}/payments",
            new { kind = "refund", method = "bank_transfer", amount = 2_000_000 });
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        var depositAgain = await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{booking.Id}/payments",
            new { kind = "deposit", method = "cash", amount = 100_000 });
        Assert.Equal(HttpStatusCode.Conflict, depositAgain.StatusCode);

        var refunded = (await (await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{booking.Id}/payments",
            new { kind = "refund", method = "bank_transfer", amount = 577_500 })).ReadAsync<BookingDto>())!;
        Assert.Equal((PaymentStatus.Refunded, 0m, 577_500m), (refunded.Money.Status, refunded.Money.PendingRefund, refunded.Money.Paid));
        Assert.Contains(refunded.Events, e => e.Type == "payment" && e.Message!.Contains("Hoàn tiền 577.500 đ"));
    }

    [Theory]
    [InlineData(10, 100)]
    [InlineData(7, 100)]
    [InlineData(6, 50)]
    [InlineData(3, 50)]
    [InlineData(2, 0)]
    public async Task CancelPreview_FollowsPolicy(int daysBefore, int expectedPercent)
    {
        var admin = await factory.LoginSeedAdminAsync();
        var stay = await admin.CreateStayAsync(room: TestCatalog.FlatRoom());
        var staff = await factory.CreateStaffAsync();
        var created = await TestCatalog.PostAsync<BookingDto>(staff, "/api/admin/bookings", new
        {
            roomId = stay.RoomId, checkIn = factory.Today.AddDays(daysBefore), checkOut = factory.Today.AddDays(daysBefore + 1), guests = 2,
            guestName = "Khách Điện Thoại", guestPhone = "0987654321", source = "phone",
            payment = new { method = "cash", amount = 400_000 },
        });

        var preview = await staff.Client.GetJsonAsync<CancelPreviewDto>($"/api/admin/bookings/{created.Id}/cancel-preview");
        Assert.Equal((expectedPercent, 400_000m * expectedPercent / 100), (preview!.RefundPercent, preview.RefundAmount));
    }

    [Fact]
    public async Task OtherGuestsBooking_Is404_LookupNeedsCodeAndPhone()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var stay = await admin.CreateStayAsync();
        var owner = await factory.CreateGuestAsync();
        var stranger = await factory.CreateGuestAsync();
        var booking = (await (await owner.Client.PostAsJsonAsync("/api/bookings", Booking(stay.RoomId, factory.Today.AddDays(25))))
            .ReadAsync<BookingDto>())!;

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.GetAsync($"/api/bookings/{booking.Code}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.PostAsJsonAsync($"/api/bookings/{booking.Code}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateRawClient().GetAsync($"/api/bookings/{booking.Code}")).StatusCode);

        var anon = factory.CreateRawClient();
        var found = await anon.PostAsJsonAsync("/api/bookings/lookup", new { code = booking.Code.ToLowerInvariant(), phone = "0912 345 678" });
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var detail = (await found.ReadAsync<BookingDto>())!;
        Assert.All(detail.Events, e => Assert.Null(e.Message));
        var wrongPhone = await anon.PostAsJsonAsync("/api/bookings/lookup", new { code = booking.Code, phone = "0987654321" });
        Assert.Equal(HttpStatusCode.NotFound, wrongPhone.StatusCode);
    }

    /// <summary>DoD Phase 3: đặt → ghi cọc → confirmed → check-in → thu đủ → check-out.</summary>
    [Fact]
    public async Task WalkIn_Lifecycle_DepositConfirmCheckInPayCheckOut()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var stay = await admin.CreateStayAsync(room: TestCatalog.FlatRoom());

        var created = await staff.Client.PostAsJsonAsync("/api/admin/bookings", new
        {
            roomId = stay.RoomId, checkIn = factory.Today, checkOut = factory.Today.AddDays(2), guests = 2,
            guestName = "Khách Vãng Lai", guestPhone = "028 3822 1234", source = "walk_in", internalNote = "Khách quen",
            payment = new { kind = "deposit", method = "cash", amount = 700_000 },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = (await created.ReadAsync<BookingDto>())!;
        Assert.Equal((BookingStatus.Confirmed, 2_300_000m, 690_000m, "Khách quen", (Guid?)null),
            (booking.Status, booking.Total, booking.DepositAmount, booking.InternalNote, booking.UserId));
        Assert.NotNull(booking.Payments.Single().RecordedBy);

        var calendar = await staff.Client.GetJsonAsync<AdminCalendarDto>(
            $"/api/admin/calendar?homestayId={stay.HomestayId}&from={factory.Today:yyyy-MM-dd}&to={factory.Today.AddDays(6):yyyy-MM-dd}");
        var bar = Assert.Single(Assert.Single(calendar!.Rooms).Bookings);
        Assert.Equal((booking.Code, PaymentStatus.DepositPaid), (bar.Code, bar.PaymentStatus));
        var dashboard = await staff.Client.GetJsonAsync<DashboardDto>("/api/admin/dashboard");
        Assert.Contains(dashboard!.Arrivals, a => a.Id == booking.Id);

        await PostOkAsync(staff, $"/api/admin/bookings/{booking.Id}/check-in");
        var blocked = await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{booking.Id}/check-out", new { });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("1.600.000 đ", await blocked.DetailAsync());

        await PostOkAsync(staff, $"/api/admin/bookings/{booking.Id}/payments", new { kind = "balance", method = "card", amount = 1_600_000 });
        var done = await PostOkAsync(staff, $"/api/admin/bookings/{booking.Id}/check-out");
        Assert.Equal((BookingStatus.CheckedOut, PaymentStatus.Paid, 0m), (done.Status, done.Money.Status, done.Money.AmountDue));
        Assert.Equal(0, await factory.WithDbAsync(db => db.RoomNights.CountAsync(n => n.BookingId == booking.Id)));
        // trả ngay trong ngày nhận phòng: cả 2 đêm (kể cả đêm nay) đã nhả – đặt lại được ngay
        Assert.Contains(done.Events, e => e.Message == "Trả phòng sớm – nhả 2 đêm còn lại");
        var afterCheckOut = await staff.Client.GetJsonAsync<DashboardDto>("/api/admin/dashboard");
        Assert.Contains(afterCheckOut!.Departures, d => d.Id == booking.Id); // trả sớm trong ngày vẫn hiện ở "khách đi hôm nay"
        Assert.Equal(["created", "payment", "status", "status", "payment", "status"], done.Events.Select(e => e.Type));
    }

    [Fact]
    public async Task StateRules_ConfirmNeedsReason_CheckInOnlyOnStayDates_GuestCannotCancelOnArrivalDay()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var stay = await admin.CreateStayAsync();
        var guest = await factory.CreateGuestAsync();

        var future = (await (await guest.Client.PostAsJsonAsync("/api/bookings", Booking(stay.RoomId, factory.Today.AddDays(3))))
            .ReadAsync<BookingDto>())!;
        var checkInPending = await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{future.Id}/check-in", new { });
        Assert.Equal(HttpStatusCode.Conflict, checkInPending.StatusCode);

        var noReason = await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{future.Id}/confirm", new { });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.NotNull(await noReason.FieldErrorAsync("reason"));
        await PostOkAsync(staff, $"/api/admin/bookings/{future.Id}/confirm", new { reason = "Khách quen, cọc khi tới" });

        var early = await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{future.Id}/check-in", new { });
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.StartsWith("Chưa tới ngày nhận phòng", await early.DetailAsync());
        var noShowEarly = await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{future.Id}/no-show", new { });
        Assert.Equal(HttpStatusCode.Conflict, noShowEarly.StatusCode);

        var today = (await (await guest.Client.PostAsJsonAsync("/api/bookings", Booking(stay.RoomId, factory.Today, nights: 1)))
            .ReadAsync<BookingDto>())!;
        var guestCancel = await guest.Client.PostAsJsonAsync($"/api/bookings/{today.Code}/cancel", new { });
        Assert.Equal(HttpStatusCode.Conflict, guestCancel.StatusCode);
        var staffNoReason = await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{today.Id}/cancel", new { });
        Assert.Equal(HttpStatusCode.BadRequest, staffNoReason.StatusCode);
        var staffCancel = await PostOkAsync(staff, $"/api/admin/bookings/{today.Id}/cancel", new { reason = "Khách gọi huỷ" });
        Assert.Equal(BookingStatus.Cancelled, staffCancel.Status);
    }

    [Fact]
    public async Task Block_ConflictsListBookingCodes_MakesRoomUnavailable_DeleteFrees()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var stay = await admin.CreateStayAsync();
        var guest = await factory.CreateGuestAsync();
        var checkIn = factory.Today.AddDays(40);
        var booking = (await (await guest.Client.PostAsJsonAsync("/api/bookings", Booking(stay.RoomId, checkIn))).ReadAsync<BookingDto>())!;

        var overlap = await staff.Client.PostAsJsonAsync("/api/admin/blocks",
            new { roomId = stay.RoomId, from = checkIn.AddDays(1), to = checkIn.AddDays(4), reason = "Sửa mái" });
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        Assert.Equal(booking.Code, (await overlap.ReadProblemAsync()).GetProperty("bookings")[0].GetString());

        var block = await TestCatalog.PostAsync<BlockDto>(staff, "/api/admin/blocks",
            new { roomId = stay.RoomId, from = checkIn.AddDays(2), to = checkIn.AddDays(4), reason = "Sửa mái" });
        var blockedBooking = await guest.Client.PostAsJsonAsync("/api/bookings", Booking(stay.RoomId, checkIn.AddDays(3), nights: 1));
        Assert.Equal(HttpStatusCode.Conflict, blockedBooking.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await staff.Client.DeleteAsync($"/api/admin/blocks/{block.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await guest.Client.PostAsJsonAsync("/api/bookings", Booking(stay.RoomId, checkIn.AddDays(3), nights: 1))).StatusCode);
    }

    [Fact]
    public async Task Reschedule_MovesNights_RepricesOrKeepsPrice_ConflictKeepsOriginal()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var stay = await admin.CreateStayAsync(room: TestCatalog.FlatRoom());
        var cheap = await admin.AddRoomAsync(stay.HomestayId, new { name = "Phòng Rẻ", baseGuests = 2, maxGuests = 2, basePrice = 500_000 });
        var guest = await factory.CreateGuestAsync();
        var checkIn = factory.Today.AddDays(50);
        var booking = (await (await guest.Client.PostAsJsonAsync("/api/bookings", Booking(stay.RoomId, checkIn))).ReadAsync<BookingDto>())!;
        var blocker = (await (await factory.CreateGuestAsync()).Client.PostAsJsonAsync("/api/bookings",
            Booking(stay.RoomId, checkIn.AddDays(10)))).StatusCode;
        Assert.Equal(HttpStatusCode.Created, blocker);

        var conflict = await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{booking.Id}/reschedule",
            new { checkIn = checkIn.AddDays(9), checkOut = checkIn.AddDays(11) });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var unchanged = await staff.Client.GetJsonAsync<BookingDto>($"/api/admin/bookings/{booking.Id}");
        Assert.Equal(checkIn, unchanged!.CheckIn);
        Assert.Equal(2, await factory.WithDbAsync(db => db.RoomNights.CountAsync(n => n.BookingId == booking.Id && n.Night >= checkIn)));

        var moved = await PostOkAsync(staff, $"/api/admin/bookings/{booking.Id}/reschedule",
            new { roomId = cheap.Id, checkIn = checkIn.AddDays(1), checkOut = checkIn.AddDays(3) });
        Assert.Equal((cheap.Id, checkIn.AddDays(1), 1_000_000m, 300_000m), (moved.RoomId, moved.CheckIn, moved.Total, moved.DepositAmount));
        var nights = await factory.WithDbAsync(db => db.RoomNights.Where(n => n.BookingId == booking.Id).Select(n => n.RoomId).ToListAsync());
        Assert.Equal([cheap.Id, cheap.Id], nights);

        var keepNoReason = await staff.Client.PostAsJsonAsync($"/api/admin/bookings/{booking.Id}/reschedule",
            new { roomId = stay.RoomId, checkIn = checkIn.AddDays(20), checkOut = checkIn.AddDays(22), keepPrice = true });
        Assert.Equal(HttpStatusCode.BadRequest, keepNoReason.StatusCode);
        var kept = await PostOkAsync(staff, $"/api/admin/bookings/{booking.Id}/reschedule",
            new { roomId = stay.RoomId, checkIn = checkIn.AddDays(20), checkOut = checkIn.AddDays(22), keepPrice = true, reason = "Bù khách" });
        Assert.Equal((stay.RoomId, 1_000_000m), (kept.RoomId, kept.Total));
        Assert.All(kept.NightPrices, n => Assert.Equal(500_000m, n.Price));
        Assert.Contains(kept.Events, e => e.Type == "rescheduled" && e.Message!.Contains("giữ giá cũ"));
    }

    [Fact]
    public async Task AdminList_FiltersByStatusAndPhone_DeletePaymentWithinWindow()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var stay = await admin.CreateStayAsync();
        var phone = $"09{Random.Shared.Next(10_000_000, 99_999_999)}";
        var booking = await TestCatalog.PostAsync<BookingDto>(staff, "/api/admin/bookings", new
        {
            roomId = stay.RoomId, checkIn = factory.Today.AddDays(60), checkOut = factory.Today.AddDays(61), guests = 1,
            guestName = "Trần Thị Tra Cứu", guestPhone = phone,
        });

        var byPhone = await staff.Client.GetJsonAsync<PagedResult<BookingSummaryDto>>($"/api/admin/bookings?q={phone[2..]}&status=pending");
        Assert.Equal(booking.Id, Assert.Single(byPhone!.Items).Id);
        var byHomestay = await staff.Client.GetJsonAsync<PagedResult<BookingSummaryDto>>(
            $"/api/admin/bookings?homestayId={stay.HomestayId}&status=confirmed");
        Assert.Empty(byHomestay!.Items);

        var withPayment = await PostOkAsync(staff, $"/api/admin/bookings/{booking.Id}/payments",
            new { kind = "deposit", method = "ewallet", amount = 400_000 });
        Assert.Equal(BookingStatus.Confirmed, withPayment.Status);
        var deleted = await staff.Client.DeleteAsync($"/api/admin/bookings/{booking.Id}/payments/{withPayment.Payments.Single().Id}");
        var afterDelete = (await deleted.ReadAsync<BookingDto>())!;
        Assert.Empty(afterDelete.Payments);
        Assert.Contains(afterDelete.Events, e => e.Type == "payment_deleted");
        Assert.Equal(BookingStatus.Confirmed, afterDelete.Status); // xoá khoản thu không tự lùi trạng thái – ghi nhật ký để lễ tân xử lý
    }

    [Fact]
    public async Task Guests_GroupedByPhone_CountsStaysAndPaid_SearchByNameOrPhone_StaffOnly()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var stay = await admin.CreateStayAsync();
        var phone = $"09{Random.Shared.Next(10_000_000, 99_999_999)}";
        var name = $"Khách Gộp {Guid.NewGuid():N}"[..20];
        object Walk(int day, string guestName) => new
        {
            roomId = stay.RoomId, checkIn = factory.Today.AddDays(day), checkOut = factory.Today.AddDays(day + 1), guests = 1,
            guestName, guestPhone = phone, guestEmail = "gop@example.com",
        };
        var first = await TestCatalog.PostAsync<BookingDto>(staff, "/api/admin/bookings", Walk(70, "Tên Cũ"));
        await PostOkAsync(staff, $"/api/admin/bookings/{first.Id}/payments", new { kind = "deposit", method = "cash", amount = 500_000 });
        await PostOkAsync(staff, $"/api/admin/bookings/{first.Id}/payments", new { kind = "refund", method = "cash", amount = 100_000 });
        var second = await TestCatalog.PostAsync<BookingDto>(staff, "/api/admin/bookings", Walk(80, name));
        await PostOkAsync(staff, $"/api/admin/bookings/{second.Id}/cancel", new { reason = "Đổi ý" });

        var byPhone = await staff.Client.GetJsonAsync<PagedResult<GuestDto>>($"/api/admin/guests?q={phone[..4]} {phone[4..]}");
        var guest = Assert.Single(byPhone!.Items, g => g.Phone == phone);
        // tên theo lần đặt gần nhất; 2 lần đặt: 1 sắp tới, 1 đã huỷ; đã trả 500k − hoàn 100k
        Assert.Equal((name, "gop@example.com", false, 2, 0, 1, 1, 400_000m, factory.Today.AddDays(80)),
            (guest.Name, guest.Email, guest.HasAccount, guest.Bookings, guest.Stays, guest.Upcoming, guest.Cancelled, guest.Paid, guest.LastCheckIn));

        var byName = await staff.Client.GetJsonAsync<PagedResult<GuestDto>>($"/api/admin/guests?q={Uri.EscapeDataString("Tên Cũ")}");
        Assert.Contains(byName!.Items, g => g.Phone == phone);

        var asGuest = await (await factory.CreateGuestAsync()).Client.GetAsync("/api/admin/guests");
        Assert.Equal(HttpStatusCode.Forbidden, asGuest.StatusCode);
    }

    private static async Task<BookingDto> PostOkAsync(TestUser user, string url, object? body = null)
    {
        var response = await user.Client.PostAsJsonAsync(url, body ?? new { });
        Assert.True(response.IsSuccessStatusCode, $"{url} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.ReadAsync<BookingDto>())!;
    }
}
