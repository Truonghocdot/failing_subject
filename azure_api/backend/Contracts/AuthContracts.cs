namespace backend.Contracts;

public sealed record RegisterRequest(string? Email, string? Password, string? DisplayName);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record UpdateProfileRequest(string? DisplayName, string? Currency);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record UserDto(Guid Id, string Email, string DisplayName, string Currency, DateTime CreatedAt);

/// <summary>Refresh token KHÔNG nằm ở đây – nó đi trong cookie HttpOnly "rt".</summary>
public sealed record AuthResponse(string AccessToken, int ExpiresIn, UserDto User);
