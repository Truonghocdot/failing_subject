using backend.Data.Entities;

namespace backend.Contracts;

public sealed record RegisterRequest(string? Email, string? Password, string? FullName, string? Phone);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record UpdateProfileRequest(string? FullName, string? Phone);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record UserDto(Guid Id, string Email, string FullName, string? Phone, UserRole Role, DateTime CreatedAt);

/// <summary>Refresh token KHÔNG nằm ở đây – nó đi trong cookie HttpOnly "rt".</summary>
public sealed record AuthResponse(string AccessToken, int ExpiresIn, UserDto User);

// --- Quản lý nhân viên (/api/admin/users – chỉ admin) ---

public sealed record StaffDto(
    Guid Id, string Email, string FullName, string? Phone, UserRole Role, bool IsActive, DateTime? LastLoginAt, DateTime CreatedAt);

/// <summary>role: "staff" | "admin".</summary>
public sealed record CreateStaffRequest(string? Email, string? Password, string? FullName, string? Phone, string? Role);

public sealed record UpdateStaffRequest(string? FullName, string? Phone, string? Role, bool? IsActive);

public sealed record ResetPasswordRequest(string? Password);
