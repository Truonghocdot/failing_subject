using backend.Data.Entities;
using backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Data;

public sealed class SeedOptions
{
    public const string Section = "Seed";

    /// <summary>Admin đầu tiên – chỉ tạo khi DB chưa có admin nào (Seed__AdminEmail trong .env).</summary>
    public string? AdminEmail { get; init; }

    public string? AdminPassword { get; init; }

    public string AdminName { get; init; } = "Quản trị viên";

    /// <summary>Thêm 2 homestay + 5 phòng mẫu khi chưa có homestay nào (bật ở appsettings.Development.json).</summary>
    public bool SampleData { get; init; }
}

/// <summary>Chạy lúc khởi động, sau migration; chạy lại nhiều lần không tạo trùng.</summary>
public sealed class Seeder(AppDbContext db, IOptions<SeedOptions> options, ILogger<Seeder> logger)
{
    private readonly SeedOptions _seed = options.Value;

    public async Task RunAsync(CancellationToken ct = default)
    {
        await EnsureAdminAsync(ct);
        if (_seed.SampleData) await EnsureSampleDataAsync(ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin, ct)) return;

        if (string.IsNullOrWhiteSpace(_seed.AdminEmail) || string.IsNullOrEmpty(_seed.AdminPassword))
        {
            logger.LogWarning("Chưa có tài khoản admin – đặt Seed__AdminEmail và Seed__AdminPassword trong backend/.env rồi khởi động lại");
            return;
        }
        if (_seed.AdminPassword.Length < 8)
        {
            logger.LogError("Seed__AdminPassword phải từ 8 ký tự – chưa tạo admin");
            return;
        }

        var email = AuthService.NormalizeEmail(_seed.AdminEmail);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            // Không tự nâng quyền 1 tài khoản khách đã tồn tại – tránh ai đó đăng ký trước bằng email admin
            logger.LogError("Email {Email} đã là tài khoản khác – đổi Seed__AdminEmail để tạo admin", email);
            return;
        }

        db.Users.Add(new User
        {
            Email = email,
            PasswordHash = AuthService.HashPassword(_seed.AdminPassword),
            FullName = _seed.AdminName,
            Role = UserRole.Admin,
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã tạo tài khoản admin đầu tiên {Email}", email);
    }

    private async Task EnsureSampleDataAsync(CancellationToken ct)
    {
        if (await db.Homestays.AnyAsync(ct)) return;

        db.Homestays.AddRange(
            new Homestay
            {
                Slug = "da-lat-pine-house",
                Name = "Đà Lạt Pine House",
                Province = "Lâm Đồng",
                Address = "12 Đường Khe Sanh, Phường 10, Đà Lạt",
                Description = "Nhà gỗ giữa đồi thông, cách chợ Đà Lạt 10 phút lái xe.\nSáng có sương, tối đốt lửa trại ngoài vườn.",
                Amenities = Codes("wifi", "parking", "kitchen", "hot_water", "bbq", "garden", "mountain_view"),
                Rooms =
                [
                    new Room
                    {
                        Name = "Phòng Đồi Thông", BedInfo = "1 giường đôi 1m8", AreaM2 = 25, BaseGuests = 2, MaxGuests = 4,
                        BasePrice = 800_000, WeekendPrice = 1_000_000, ExtraGuestFee = 150_000, CleaningFee = 100_000, SortOrder = 1,
                        Description = "Cửa sổ lớn nhìn ra rừng thông, ban công riêng.",
                    },
                    new Room
                    {
                        Name = "Phòng Sương Mai", BedInfo = "1 giường đôi 1m6", AreaM2 = 20, BaseGuests = 2, MaxGuests = 2,
                        BasePrice = 650_000, WeekendPrice = 800_000, CleaningFee = 100_000, SortOrder = 2,
                    },
                    new Room
                    {
                        Name = "Phòng Gia Đình", BedInfo = "2 giường đôi 1m6", AreaM2 = 35, BaseGuests = 4, MaxGuests = 6,
                        BasePrice = 1_200_000, WeekendPrice = 1_500_000, ExtraGuestFee = 150_000, CleaningFee = 150_000,
                        MinNights = 2, SortOrder = 3,
                    },
                ],
            },
            new Homestay
            {
                Slug = "hoi-an-riverside-villa",
                Name = "Hội An Riverside Villa",
                Province = "Đà Nẵng",
                Address = "45 Nguyễn Phúc Chu, Minh An, Hội An",
                Description = "Biệt thự ven sông Hoài, đi bộ 5 phút tới phố cổ. Có hồ bơi và xe đạp miễn phí.",
                DepositPercent = 50,
                HoldHours = 12,
                Amenities = Codes("wifi", "aircon", "pool", "breakfast", "washer", "balcony", "workspace"),
                Rooms =
                [
                    new Room
                    {
                        Name = "Phòng Hướng Sông", BedInfo = "1 giường King", AreaM2 = 30, BaseGuests = 2, MaxGuests = 3,
                        BasePrice = 1_100_000, WeekendPrice = 1_300_000, ExtraGuestFee = 200_000, CleaningFee = 0, SortOrder = 1,
                    },
                    new Room
                    {
                        Name = "Nguyên căn", BedInfo = "3 phòng ngủ, 5 giường", AreaM2 = 180, BaseGuests = 6, MaxGuests = 10,
                        BasePrice = 4_500_000, WeekendPrice = 5_500_000, ExtraGuestFee = 250_000, CleaningFee = 300_000,
                        MinNights = 2, SortOrder = 2, Description = "Thuê trọn biệt thự, có bếp và hồ bơi riêng.",
                    },
                ],
            });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã thêm dữ liệu mẫu: 2 homestay, 5 phòng");
    }

    private static List<HomestayAmenity> Codes(params string[] codes) => [.. codes.Select(c => new HomestayAmenity { Code = c })];
}
