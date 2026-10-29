using System.Net;
using System.Net.Http.Json;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace backend.Tests;

/// <summary>Phase 6: email outbox (.eml), đánh giá, báo cáo khớp SQL viết tay.</summary>
public class Phase6Tests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // --- Email ---

    [Fact]
    public async Task OnlineBooking_QueuesTransferMail_SenderWritesEml_WithCodeDatesAmounts()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var stay = await admin.CreateStayAsync(room: TestCatalog.FlatRoom());
        var guest = await factory.CreateGuestAsync();
        var checkIn = factory.Today.AddDays(40);
        var booking = await TestCatalog.PostAsync<BookingDto>(guest, "/api/bookings", new
        {
            roomId = stay.RoomId, checkIn, checkOut = checkIn.AddDays(3), guests = 3, guestName = "Nguyễn Văn Thư", guestPhone = "0912345678",
        });
        Assert.Equal(guest.Auth.User.Email, booking.GuestEmail); // mặc định email tài khoản

        var mail = await factory.WithDbAsync(db => db.EmailOutbox.SingleAsync(m => m.Subject.Contains(booking.Code)));
        Assert.Equal((EmailStatus.Pending, guest.Auth.User.Email), (mail.Status, mail.ToEmail));
        Assert.Contains("chuyển khoản cọc", mail.Subject);

        var eml = await SendAndLoadAsync(mail.Id);
        Assert.Equal(guest.Auth.User.Email, eml.To.Mailboxes.Single().Address);
        var html = eml.HtmlBody;
        Assert.Contains(booking.Code, html);
        Assert.Contains(checkIn.ToString("dd/MM/yyyy"), html);
        Assert.Contains(checkIn.AddDays(3).ToString("dd/MM/yyyy"), html);
        Assert.Contains(Vnd.Format(3_850_000), html);  // tổng (DoD Phase 3)
        Assert.Contains(Vnd.Format(1_155_000), html);  // cọc = số cần chuyển khoản
        Assert.Contains("Ngân hàng Test", html);
        Assert.Contains($"/bookings/{booking.Code}", html);
    }

    [Fact]
    public async Task DepositConfirms_CancelMailHasRefund_ReminderQueuedOnceForTomorrow_NoEmailNoMail()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var stay = await admin.CreateStayAsync(room: TestCatalog.FlatRoom());
        var email = ApiFactory.NewEmail();
        object Walk(int day, string? guestEmail, object? payment = null) => new
        {
            roomId = stay.RoomId, checkIn = factory.Today.AddDays(day), checkOut = factory.Today.AddDays(day + 1), guests = 2,
            guestName = "Khách Gọi Điện", guestPhone = "0905123456", guestEmail, payment,
        };

        var pending = await TestCatalog.PostAsync<BookingDto>(staff, "/api/admin/bookings", Walk(30, email));
        await TestCatalog.PostAsync<BookingDto>(staff, $"/api/admin/bookings/{pending.Id}/payments", new { kind = "deposit", method = "bank_transfer", amount = 360_000 });
        await TestCatalog.PostAsync<BookingDto>(staff, $"/api/admin/bookings/{pending.Id}/cancel", new { reason = "Khách đổi kế hoạch", refundAmount = 200_000 });
        var subjects = await factory.WithDbAsync(db => db.EmailOutbox.Where(m => m.Subject.Contains(pending.Code)).OrderBy(m => m.Id).Select(m => m.Subject).ToListAsync());
        Assert.Collection(subjects,
            s => Assert.Contains("chuyển khoản cọc", s),
            s => Assert.Contains("đã được xác nhận", s),
            s => Assert.Contains("đã huỷ", s));
        var cancelMail = await factory.WithDbAsync(db => db.EmailOutbox.Where(m => m.Subject.Contains(pending.Code)).OrderBy(m => m.Id).LastAsync());
        Assert.Contains(Vnd.Format(200_000), cancelMail.HtmlBody);
        Assert.Contains("Khách đổi kế hoạch", cancelMail.HtmlBody);

        // Tạo hộ kèm cọc đủ → chỉ 1 thư xác nhận; nhận phòng ngày mai → nhắc đúng 1 lần
        var tomorrow = await TestCatalog.PostAsync<BookingDto>(staff, "/api/admin/bookings",
            Walk(1, email, new { kind = "deposit", method = "cash", amount = 360_000 }));
        Assert.Equal(BookingStatus.Confirmed, tomorrow.Status);
        await factory.WithServiceAsync<EmailService, int>(s => s.QueueRemindersAsync(CancellationToken.None));
        await factory.WithServiceAsync<EmailService, int>(s => s.QueueRemindersAsync(CancellationToken.None));
        var tomorrowMails = await factory.WithDbAsync(db => db.EmailOutbox.Where(m => m.Subject.Contains(tomorrow.Code)).Select(m => m.Subject).ToListAsync());
        Assert.Equal(2, tomorrowMails.Count);
        Assert.Contains(tomorrowMails, s => s.Contains("Nhắc lịch nhận phòng ngày mai"));

        var noEmail = await TestCatalog.PostAsync<BookingDto>(staff, "/api/admin/bookings", Walk(50, null));
        Assert.False(await factory.WithDbAsync(db => db.EmailOutbox.AnyAsync(m => m.Subject.Contains(noEmail.Code))));
    }

    [Fact]
    public async Task SendFailure_RetriesThenMarksFailed()
    {
        var id = await factory.WithDbAsync(async db =>
        {
            var mail = new EmailOutbox { ToEmail = "@@khong hop le@@", Subject = "Thư lỗi", HtmlBody = "<p>x</p>", Attempts = EmailOutboxSender.MaxAttempts - 1 };
            db.EmailOutbox.Add(mail);
            await db.SaveChangesAsync();
            return mail.Id;
        });
        await SendUntilProcessedAsync(id);
        var failed = await factory.WithDbAsync(db => db.EmailOutbox.SingleAsync(m => m.Id == id));
        Assert.Equal((EmailStatus.Failed, EmailOutboxSender.MaxAttempts), (failed.Status, failed.Attempts));
        Assert.NotNull(failed.LastError);
    }

    private async Task<MimeMessage> SendAndLoadAsync(long mailId)
    {
        await SendUntilProcessedAsync(mailId);
        var file = Directory.GetFiles(ApiFactory.MailRoot, $"*-outbox-{mailId}-*.eml").Single();
        return await MimeMessage.LoadAsync(file);
    }

    /// <summary>Outbox dùng chung DB test – gửi theo lô cho tới khi tới lượt thư cần kiểm tra.</summary>
    private async Task SendUntilProcessedAsync(long mailId)
    {
        for (var i = 0; i < 50; i++)
        {
            if (await factory.WithDbAsync(db => db.EmailOutbox.AnyAsync(m => m.Id == mailId && m.Status != EmailStatus.Pending))) return;
            await factory.WithServiceAsync<EmailOutboxSender, int>(s => s.SendPendingAsync(CancellationToken.None));
        }
        throw new InvalidOperationException($"Thư {mailId} không được xử lý");
    }

    // --- Đánh giá ---

    [Fact]
    public async Task Review_OnlyAfterCheckOut_Once_PublicShortName_AdminHides_RatingUpdates()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var stay = await admin.CreateStayAsync(room: TestCatalog.FlatRoom());
        var guest = await factory.CreateGuestAsync();
        var booking = await TestCatalog.PostAsync<BookingDto>(guest, "/api/bookings", new
        {
            roomId = stay.RoomId, checkIn = factory.Today, checkOut = factory.Today.AddDays(1), guests = 2, guestName = "Trần Thị Minh", guestPhone = "0912345678",
        });
        var early = await guest.Client.PostAsJsonAsync($"/api/bookings/{booking.Code}/review", new { rating = 5 });
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Contains("sau khi trả phòng", await early.DetailAsync());

        await TestCatalog.PostAsync<BookingDto>(staff, $"/api/admin/bookings/{booking.Id}/payments", new { kind = "balance", method = "cash", amount = booking.Total });
        await TestCatalog.PostAsync<BookingDto>(staff, $"/api/admin/bookings/{booking.Id}/check-in", new { });
        await TestCatalog.PostAsync<BookingDto>(staff, $"/api/admin/bookings/{booking.Id}/check-out", new { });
        Assert.True((await guest.Client.GetJsonAsync<BookingDto>($"/api/bookings/{booking.Code}"))!.CanReview);

        var invalid = await guest.Client.PostAsJsonAsync($"/api/bookings/{booking.Code}/review", new { rating = 6 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var other = await (await factory.CreateGuestAsync()).Client.PostAsJsonAsync($"/api/bookings/{booking.Code}/review", new { rating = 1 });
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        var created = await guest.Client.PostAsJsonAsync($"/api/bookings/{booking.Code}/review", new { rating = 4, comment = "  Sạch sẽ, chủ nhà thân thiện  " });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var again = await guest.Client.PostAsJsonAsync($"/api/bookings/{booking.Code}/review", new { rating = 5 });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var detail = (await guest.Client.GetJsonAsync<BookingDto>($"/api/bookings/{booking.Code}"))!;
        Assert.Equal((false, (byte)4, "Sạch sẽ, chủ nhà thân thiện"), (detail.CanReview, detail.Review!.Rating, detail.Review.Comment));

        var anonymous = factory.CreateRawClient();
        var list = (await anonymous.GetJsonAsync<PagedResult<PublicReviewDto>>($"/api/stays/{stay.Slug}/reviews"))!;
        var review = Assert.Single(list.Items);
        // tên người viết = tài khoản đã đặt, rút gọn "Tên H."
        Assert.Equal((ReviewService.ShortName(guest.Auth.User.FullName), (byte)4, factory.Today), (review.Author, review.Rating, review.StayedAt));
        Assert.DoesNotContain(guest.Auth.User.FullName, review.Author);
        var stayDto = (await anonymous.GetJsonAsync<StayDetailDto>($"/api/stays/{stay.Slug}"))!;
        Assert.Equal((4.0, 1), (stayDto.Rating, stayDto.ReviewCount));

        var asStaff = await staff.Client.PutAsJsonAsync($"/api/admin/reviews/{review.Id}", new { isHidden = true });
        Assert.Equal(HttpStatusCode.Forbidden, asStaff.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.Client.PutAsJsonAsync($"/api/admin/reviews/{review.Id}", new { isHidden = true })).StatusCode);
        Assert.Empty((await anonymous.GetJsonAsync<PagedResult<PublicReviewDto>>($"/api/stays/{stay.Slug}/reviews"))!.Items);
        Assert.Equal(((double?)null, 0), ((await anonymous.GetJsonAsync<StayDetailDto>($"/api/stays/{stay.Slug}"))!.Rating, (await anonymous.GetJsonAsync<StayDetailDto>($"/api/stays/{stay.Slug}"))!.ReviewCount));
        var hidden = (await admin.Client.GetJsonAsync<PagedResult<AdminReviewDto>>($"/api/admin/reviews?homestayId={stay.HomestayId}&hidden=true"))!;
        Assert.Equal((booking.Code, "Trần Thị Minh"), (Assert.Single(hidden.Items).BookingCode, hidden.Items[0].GuestName));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(29, null)]
    [InlineData(31, "Chỉ đánh giá trong 30 ngày sau khi trả phòng")]
    public void ReviewWindow_ThirtyDaysAfterCheckOut(int daysAfter, string? expected)
    {
        var checkedOut = new DateTime(2026, 11, 1, 5, 0, 0, DateTimeKind.Utc);
        var b = new Booking { Code = "HS-TEST-0001", GuestName = "A", GuestPhone = "0912345678", Status = BookingStatus.CheckedOut, CheckedOutAt = checkedOut };
        Assert.Equal(expected, ReviewService.CannotReview(b, alreadyReviewed: false, checkedOut.AddDays(daysAfter)));
    }

    [Theory]
    [InlineData("Trần Thị Minh", "Minh T.")]
    [InlineData("an", "an")]
    [InlineData("  nguyễn   văn  bình ", "bình N.")]
    public void ShortName_GivenNamePlusFamilyInitial(string full, string expected) => Assert.Equal(expected, ReviewService.ShortName(full));

    // --- Báo cáo ---

    [Fact]
    public async Task Report_MatchesHandWrittenSql_AndMonthsSumToTotal_AdminOnly()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        var stay = await admin.CreateStayAsync(room: TestCatalog.DefaultRoom());
        var room2 = await admin.AddRoomAsync(stay.HomestayId, TestCatalog.FlatRoom("Phòng 2"));
        // khoảng ngày cắt ngang 2 tháng
        var monthStart = new DateOnly(factory.Today.Year, factory.Today.Month, 1).AddMonths(3);
        var from = monthStart.AddDays(-5);
        var to = monthStart.AddDays(9);
        object Walk(long roomId, DateOnly ci, int nights, int guests, decimal? deposit) => new
        {
            roomId, checkIn = ci, checkOut = ci.AddDays(nights), guests, guestName = "Khách Báo Cáo", guestPhone = "0905123456",
            payment = deposit is { } d ? new { kind = "deposit", method = "cash", amount = d } : null,
        };

        // A: đã xác nhận, 3 khách (có phụ thu), 4 đêm cắt ngang tháng; B: chờ cọc (chưa bán); C: huỷ; D: đã xác nhận, 2 đêm ngoài khoảng một phần
        var a = await TestCatalog.PostAsync<BookingDto>(staff, "/api/admin/bookings", Walk(stay.RoomId, monthStart.AddDays(-2), 4, 3, 2_000_000));
        await TestCatalog.PostAsync<BookingDto>(staff, "/api/admin/bookings", Walk(room2.Id, monthStart, 2, 2, null));
        var c = await TestCatalog.PostAsync<BookingDto>(staff, "/api/admin/bookings", Walk(room2.Id, monthStart.AddDays(3), 2, 2, 1_000_000));
        await TestCatalog.PostAsync<BookingDto>(staff, $"/api/admin/bookings/{c.Id}/cancel", new { reason = "Huỷ test" });
        await TestCatalog.PostAsync<BookingDto>(staff, "/api/admin/bookings", Walk(stay.RoomId, to, 2, 2, 1_000_000));
        (await staff.Client.PostAsJsonAsync("/api/admin/blocks", new { roomId = room2.Id, from = monthStart.AddDays(6), to = monthStart.AddDays(7), reason = "Bảo trì" }))
            .EnsureSuccessStatusCode();
        Assert.Equal(BookingStatus.Confirmed, a.Status);

        var report = (await admin.Client.GetJsonAsync<ReportDto>($"/api/admin/reports?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&homestayId={stay.HomestayId}"))!;

        // SQL viết tay trên cùng dữ liệu (docs/02-nghiep-vu.md §11)
        var (sqlSold, sqlRevenue, sqlRooms, sqlBlocked) = await factory.WithDbAsync(async db =>
        {
            var p = new object[] { stay.HomestayId, from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd") };
            const string soldFrom = "FROM booking_nights bn JOIN bookings b ON b.id = bn.booking_id " +
                                    "WHERE b.homestay_id = {0} AND b.status IN ('confirmed','checked_in','checked_out') AND bn.night BETWEEN {1} AND {2}";
            var sold = await db.Database.SqlQueryRaw<long>($"SELECT COUNT(*) AS Value {soldFrom}", p).SingleAsync();
            var revenue = await db.Database.SqlQueryRaw<decimal>($"SELECT COALESCE(SUM(bn.price + b.extra_guest_total / b.nights), 0) AS Value {soldFrom}", p).SingleAsync();
            var rooms = await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM rooms WHERE homestay_id = {0} AND status = 'active'", stay.HomestayId).SingleAsync();
            var blocked = await db.Database.SqlQueryRaw<long>(
                "SELECT COUNT(*) AS Value FROM room_nights rn JOIN rooms r ON r.id = rn.room_id " +
                "WHERE r.homestay_id = {0} AND r.status = 'active' AND rn.block_id IS NOT NULL AND rn.night BETWEEN {1} AND {2}", p).SingleAsync();
            return (sold, revenue, rooms, blocked);
        });
        var days = to.DayNumber - from.DayNumber + 1;
        Assert.Equal((5L, 2L, 2L), (sqlSold, sqlRooms, sqlBlocked)); // A: 4 đêm + D: 1 đêm trong khoảng
        var t = report.Total;
        Assert.Equal((int)(sqlRooms * days - sqlBlocked), t.SellableNights);
        Assert.Equal((int)sqlSold, t.SoldNights);
        Assert.Equal(Vnd.Round(sqlRevenue), t.RoomRevenue);
        Assert.Equal(Vnd.Round(sqlRevenue / sqlSold), t.Adr);
        Assert.Equal(Vnd.Round(sqlRevenue / t.SellableNights), t.RevPar);
        Assert.Equal(Math.Round((double)sqlSold / t.SellableNights, 4), t.Occupancy);

        Assert.Equal(2, report.Months.Count);
        Assert.Equal(t.SoldNights, report.Months.Sum(m => m.Metrics.SoldNights));
        Assert.Equal(t.SellableNights, report.Months.Sum(m => m.Metrics.SellableNights));
        Assert.Equal((2, 3), (report.Months[0].Metrics.SoldNights, report.Months[1].Metrics.SoldNights));
        var row = Assert.Single(report.Homestays);
        Assert.Equal(t, row.Metrics);
        Assert.Equal(5, report.Sources.Single(s => s.Source == BookingSource.Phone).SoldNights);

        // Đặt phòng tạo hôm nay: 4 cái, 1 huỷ → tỉ lệ huỷ 25%; tiền thu = 3 khoản cọc (khoản huỷ chưa hoàn)
        var today = (await admin.Client.GetJsonAsync<ReportDto>($"/api/admin/reports?from={factory.Today:yyyy-MM-dd}&to={factory.Today:yyyy-MM-dd}&homestayId={stay.HomestayId}"))!;
        Assert.Equal((4, 1, 0.25, 4_000_000m), (today.Bookings, today.Cancelled, today.CancelRate, today.Collected));

        var asStaff = await staff.Client.GetAsync("/api/admin/reports");
        Assert.Equal(HttpStatusCode.Forbidden, asStaff.StatusCode);
        var tooLong = await admin.Client.GetAsync($"/api/admin/reports?from={from:yyyy-MM-dd}&to={from.AddDays(400):yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }
}
