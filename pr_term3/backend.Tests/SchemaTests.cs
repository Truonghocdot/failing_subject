using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

/// <summary>Ràng buộc ở tầng DB (docs/06-database.md §4) – lưới an toàn cuối nếu service bỏ sót kiểm tra.</summary>
public class SchemaTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly Night = new(2026, 12, 24);

    private async Task<(long HomestayId, long RoomId)> SeedRoomAsync()
    {
        factory.CreateRawClient(); // bảo đảm DB test đã tạo
        return await factory.WithDbAsync(async db =>
        {
            var homestay = new Homestay
            {
                Slug = $"test-{Guid.NewGuid():N}",
                Name = "Homestay Test",
                Province = "Lâm Đồng",
                Address = "1 Đường Test",
                Rooms = [new Room { Name = "Phòng 1", BasePrice = 800_000, MaxGuests = 4 }],
            };
            db.Homestays.Add(homestay);
            await db.SaveChangesAsync();
            return (homestay.Id, homestay.Rooms[0].Id);
        });
    }

    private static Booking NewBooking(long homestayId, long roomId, DateOnly checkIn, DateOnly checkOut) => new()
    {
        Code = $"HS-{Random.Shared.Next(1000, 9999)}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
        HomestayId = homestayId,
        RoomId = roomId,
        GuestName = "Khách",
        GuestPhone = "0912345678",
        Guests = 2,
        Nights = (short)(checkOut.DayNumber - checkIn.DayNumber),
        CheckIn = checkIn,
        CheckOut = checkOut,
        RoomTotal = 800_000,
        Total = 800_000,
        DepositAmount = 240_000,
    };

    private async Task<long> SeedBookingAsync(long homestayId, long roomId) =>
        await factory.WithDbAsync(async db =>
        {
            var booking = NewBooking(homestayId, roomId, Night, Night.AddDays(1));
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();
            return booking.Id;
        });

    [Fact]
    public async Task RoomNights_SameRoomSameNight_SecondInsertRejected()
    {
        var (homestayId, roomId) = await SeedRoomAsync();
        var first = await SeedBookingAsync(homestayId, roomId);
        var second = await SeedBookingAsync(homestayId, roomId);

        await factory.WithDbAsync(async db =>
        {
            db.RoomNights.Add(new RoomNight { RoomId = roomId, Night = Night, BookingId = first });
            return await db.SaveChangesAsync();
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.RoomNights.Add(new RoomNight { RoomId = roomId, Night = Night, BookingId = second });
            return await db.SaveChangesAsync();
        }));

        // MySQL 1062 (Duplicate entry) – BookingService (Phase 3) đổi thành 409
        Assert.Contains("Duplicate entry", ex.InnerException?.Message);

        // Đêm kế tiếp vẫn đặt được
        await factory.WithDbAsync(async db =>
        {
            db.RoomNights.Add(new RoomNight { RoomId = roomId, Night = Night.AddDays(1), BookingId = second });
            return await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task RoomNights_MustBelongToExactlyOneOwner()
    {
        var (homestayId, roomId) = await SeedRoomAsync();
        var bookingId = await SeedBookingAsync(homestayId, roomId);
        var blockId = await factory.WithDbAsync(async db =>
        {
            var block = new RoomBlock { RoomId = roomId, FromDate = Night, ToDate = Night, Reason = "Sửa điều hoà" };
            db.RoomBlocks.Add(block);
            await db.SaveChangesAsync();
            return block.Id;
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.RoomNights.Add(new RoomNight { RoomId = roomId, Night = Night.AddDays(10) });
            return await db.SaveChangesAsync();
        }));
        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.RoomNights.Add(new RoomNight { RoomId = roomId, Night = Night.AddDays(11), BookingId = bookingId, BlockId = blockId });
            return await db.SaveChangesAsync();
        }));
    }

    public static TheoryData<string, Action<Room>> InvalidRooms => new()
    {
        { "base_guests > max_guests", r => { r.BaseGuests = 4; r.MaxGuests = 2; } },
        { "max_guests > 20", r => r.MaxGuests = 21 },
        { "base_guests = 0", r => r.BaseGuests = 0 },
        { "base_price = 0", r => r.BasePrice = 0 },
        { "weekend_price = 0", r => r.WeekendPrice = 0 },
        { "cleaning_fee âm", r => r.CleaningFee = -1 },
        { "min_nights = 0", r => r.MinNights = 0 },
    };

    [Theory]
    [MemberData(nameof(InvalidRooms))]
    public async Task InvalidRoom_IsRejectedByCheckConstraint(string _, Action<Room> breakRoom)
    {
        var (homestayId, _) = await SeedRoomAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            var room = new Room { HomestayId = homestayId, Name = $"P-{Guid.NewGuid():N}", BasePrice = 500_000 };
            breakRoom(room);
            db.Rooms.Add(room);
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task RoomName_IsUniqueWithinHomestay()
    {
        var (homestayId, _) = await SeedRoomAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.Rooms.Add(new Room { HomestayId = homestayId, Name = "Phòng 1", BasePrice = 500_000 });
            return await db.SaveChangesAsync();
        }));
    }

    public static TheoryData<string, Action<Booking>> InvalidBookings => new()
    {
        { "check_out = check_in", b => b.CheckOut = b.CheckIn },
        { "guests = 0", b => b.Guests = 0 },
        { "cọc > tổng", b => b.DepositAmount = b.Total + 1 },
        { "tổng âm", b => b.Total = -1 },
    };

    [Theory]
    [MemberData(nameof(InvalidBookings))]
    public async Task InvalidBooking_IsRejectedByCheckConstraint(string _, Action<Booking> breakBooking)
    {
        var (homestayId, roomId) = await SeedRoomAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            var booking = NewBooking(homestayId, roomId, Night, Night.AddDays(2));
            breakBooking(booking);
            db.Bookings.Add(booking);
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Homestay_InvalidPolicy_IsRejected()
    {
        factory.CreateRawClient();
        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.Homestays.Add(new Homestay
            {
                Slug = $"x-{Guid.NewGuid():N}", Name = "X", Province = "Huế", Address = "X",
                FreeCancelDays = 2, PartialRefundDays = 5, // hoàn một phần phải ít ngày hơn hoàn toàn bộ
            });
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Payment_AmountMustBePositive_ReviewRatingOneToFive()
    {
        var (homestayId, roomId) = await SeedRoomAsync();
        var bookingId = await SeedBookingAsync(homestayId, roomId);
        var adminId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == ApiFactory.AdminEmail).Select(u => u.Id).SingleAsync());

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.Payments.Add(new Payment
            {
                BookingId = bookingId, Kind = PaymentKind.Deposit, Method = PaymentMethod.Cash, Amount = 0,
                PaidAt = DateTime.UtcNow, RecordedById = adminId,
            });
            return await db.SaveChangesAsync();
        }));
        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.Reviews.Add(new Review { BookingId = bookingId, HomestayId = homestayId, UserId = adminId, Rating = 6 });
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Types_RoundTrip_EnumsAsSnakeCaseText()
    {
        var (homestayId, roomId) = await SeedRoomAsync();
        var adminId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == ApiFactory.AdminEmail).Select(u => u.Id).SingleAsync());

        var bookingId = await factory.WithDbAsync(async db =>
        {
            var booking = NewBooking(homestayId, roomId, Night, Night.AddDays(3));
            booking.Status = BookingStatus.CheckedIn;
            booking.Source = BookingSource.WalkIn;
            booking.RoomTotal = 2_450_000.50m;
            booking.Total = 2_450_000.50m;
            booking.Payments.Add(new Payment
            {
                Kind = PaymentKind.Deposit, Method = PaymentMethod.BankTransfer, Amount = 735_000,
                PaidAt = DateTime.UtcNow, RecordedById = adminId,
            });
            booking.Events.Add(new BookingEvent { Type = "status", FromStatus = BookingStatus.Confirmed, ToStatus = BookingStatus.CheckedIn });
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();
            return booking.Id;
        });

        var raw = await factory.WithDbAsync(async db => (
            Status: await db.Database.SqlQuery<string>($"SELECT status AS Value FROM bookings WHERE id = {bookingId}").SingleAsync(),
            Source: await db.Database.SqlQuery<string>($"SELECT source AS Value FROM bookings WHERE id = {bookingId}").SingleAsync(),
            Method: await db.Database.SqlQuery<string>($"SELECT method AS Value FROM payments WHERE booking_id = {bookingId}").SingleAsync(),
            Event: await db.Database.SqlQuery<string>($"SELECT to_status AS Value FROM booking_events WHERE booking_id = {bookingId}").SingleAsync()));
        Assert.Equal(("checked_in", "walk_in", "bank_transfer", "checked_in"), raw);

        var (booking, homestay) = await factory.WithDbAsync(async db => (
            await db.Bookings.AsNoTracking().Include(b => b.Events).SingleAsync(b => b.Id == bookingId),
            await db.Homestays.AsNoTracking().SingleAsync(h => h.Id == homestayId)));
        Assert.Equal(BookingStatus.CheckedIn, booking.Status);
        Assert.Equal(BookingStatus.Confirmed, booking.Events[0].FromStatus);
        Assert.Equal(2_450_000.50m, booking.Total);
        Assert.Equal(Night, booking.CheckIn);
        Assert.Equal(DateTimeKind.Utc, booking.CreatedAt.Kind);
        Assert.Equal(new TimeOnly(14, 0), homestay.CheckInTime);
        Assert.Equal(new TimeOnly(12, 0), homestay.CheckOutTime);
        Assert.Equal(30, homestay.DepositPercent);
    }

    [Fact]
    public async Task UserEmail_IsUnique_IgnoringCase()
    {
        factory.CreateRawClient();
        var email = ApiFactory.NewEmail();
        await factory.WithDbAsync(async db =>
        {
            db.Users.Add(new User { Email = email, PasswordHash = "x", FullName = "A" });
            return await db.SaveChangesAsync();
        });

        // collation utf8mb4_unicode_ci → "A@B" trùng "a@b" ngay ở DB
        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.Users.Add(new User { Email = email.ToUpperInvariant(), PasswordHash = "x", FullName = "B" });
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public void EnumText_UsesSnakeCase()
    {
        Assert.Equal("no_show", EnumText<BookingStatus>.ToText(BookingStatus.NoShow));
        Assert.Equal(PaymentMethod.Ewallet, EnumText<PaymentMethod>.Parse("ewallet"));
        Assert.Equal("'web','phone','walk_in','ota'", EnumText<BookingSource>.SqlList);
    }
}
