namespace backend.Data.Entities;

/// <summary>Quy tắc trường: docs/02-nghiep-vu.md §2.</summary>
public class Homestay : ITimestamped
{
    public long Id { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public required string Province { get; set; }
    public required string Address { get; set; }
    public string Description { get; set; } = "";

    public TimeOnly CheckInTime { get; set; } = new(14, 0);
    public TimeOnly CheckOutTime { get; set; } = new(12, 0);

    /// <summary>0 = không cần cọc → đặt online được xác nhận ngay.</summary>
    public byte DepositPercent { get; set; } = 30;
    public byte HoldHours { get; set; } = 24;
    public byte FreeCancelDays { get; set; } = 7;
    public byte PartialRefundDays { get; set; } = 3;
    public byte PartialRefundPercent { get; set; } = 50;

    public HomestayStatus Status { get; set; } = HomestayStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<HomestayAmenity> Amenities { get; set; } = [];
    public List<Photo> Photos { get; set; } = [];
    public List<Room> Rooms { get; set; } = [];
}

public class HomestayAmenity
{
    public long HomestayId { get; set; }

    /// <summary>Mã trong <see cref="Amenity.Codes"/> (wifi, parking...).</summary>
    public required string Code { get; set; }
}

public static class Amenity
{
    public static readonly string[] Codes =
    [
        "wifi", "parking", "kitchen", "aircon", "hot_water", "washer", "tv", "pool",
        "bbq", "garden", "balcony", "mountain_view", "sea_view", "breakfast", "pet_friendly", "workspace",
    ];
}

public class Photo : ICreatedAt
{
    public long Id { get; set; }
    public long HomestayId { get; set; }

    /// <summary>Tên ngẫu nhiên (guid + đuôi) trong storage/uploads/{homestayId}/.</summary>
    public required string FileName { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>0 = ảnh bìa.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Quy tắc trường: docs/02-nghiep-vu.md §3.</summary>
public class Room : ITimestamped
{
    public long Id { get; set; }
    public long HomestayId { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public string? BedInfo { get; set; }
    public short? AreaM2 { get; set; }

    public byte BaseGuests { get; set; } = 2;
    public byte MaxGuests { get; set; } = 2;
    public byte MinNights { get; set; } = 1;

    public decimal BasePrice { get; set; }

    /// <summary>Áp cho đêm thứ Sáu và thứ Bảy; null = như giá cơ bản.</summary>
    public decimal? WeekendPrice { get; set; }

    /// <summary>Mỗi khách vượt <see cref="BaseGuests"/> mỗi đêm.</summary>
    public decimal ExtraGuestFee { get; set; }

    /// <summary>Mỗi lượt đặt.</summary>
    public decimal CleaningFee { get; set; }

    public RoomStatus Status { get; set; } = RoomStatus.Active;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Homestay Homestay { get; set; } = null!;
}

/// <summary>Giá riêng 1 đêm (lễ, Tết, cao điểm) – ưu tiên cao nhất (docs/02-nghiep-vu.md §4).</summary>
public class RateOverride : ICreatedAt
{
    public long RoomId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Price { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
