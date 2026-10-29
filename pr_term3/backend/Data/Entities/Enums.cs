namespace backend.Data.Entities;

// Lưu xuống DB và trả JSON dạng snake_case chữ thường: "guest", "checked_in", "bank_transfer"... (docs/06-database.md §1)

public enum UserRole
{
    Guest,
    Staff,
    Admin,
}

public enum HomestayStatus
{
    Active,
    Hidden,
}

public enum RoomStatus
{
    Active,
    Inactive,
}

public enum BookingStatus
{
    Pending,
    Confirmed,
    CheckedIn,
    CheckedOut,
    Cancelled,
    Expired,
    NoShow,
}

public enum BookingSource
{
    Web,
    Phone,
    WalkIn,
    Ota,
}

public enum PaymentKind
{
    Deposit,
    Balance,
    Extra,
    Refund,
}

public enum PaymentMethod
{
    Cash,
    BankTransfer,
    Card,
    Ewallet,
}

public enum EmailStatus
{
    Pending,
    Sent,
    Failed,
}
