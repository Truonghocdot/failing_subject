namespace backend.Contracts;

public sealed record RegisterRequest(string? Email, string? Password, string? DisplayName, string? DeviceName = null);

public sealed record LoginRequest(string? Email, string? Password, string? DeviceName = null);

public sealed record RefreshRequest(string? RefreshToken);

public sealed record UserDto(Guid Id, string Email, string DisplayName, string? AvatarUrl, DateTime CreatedAt);

public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserDto User);
