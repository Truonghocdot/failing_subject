using System.Security.Claims;
using backend.Data;
using backend.Data.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace backend.Auth;

/// <summary>Vai trò trong claim "role" của JWT + 2 policy cho khu quản trị (docs/02-nghiep-vu.md §1).</summary>
public static class Roles
{
    public const string ClaimType = "role";

    /// <summary>Lễ tân hoặc quản trị – mọi route <c>/api/admin/*</c>.</summary>
    public const string StaffPolicy = "Staff";

    /// <summary>Chỉ quản trị – homestay, phòng, giá, nhân viên, báo cáo.</summary>
    public const string AdminPolicy = "Admin";

    public static readonly string Guest = EnumText<UserRole>.ToText(UserRole.Guest);
    public static readonly string Staff = EnumText<UserRole>.ToText(UserRole.Staff);
    public static readonly string Admin = EnumText<UserRole>.ToText(UserRole.Admin);

    public static IServiceCollection AddRolePolicies(this IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            .AddPolicy(StaffPolicy, p => p.RequireAuthenticatedUser().RequireRole(Staff, Admin))
            .AddPolicy(AdminPolicy, p => p.RequireAuthenticatedUser().RequireRole(Admin))
            .Services;

    /// <summary>
    /// Mỗi request có token: kiểm tra tài khoản còn hoạt động và vai trò trong token còn đúng.
    /// Khoá / hạ quyền nhân viên có hiệu lực ngay, không phải chờ access token 15 phút hết hạn –
    /// FE nhận 401, tự refresh (refresh cũng bị chặn nếu đã khoá) và nhận token với vai trò mới.
    /// </summary>
    public static async Task ValidateUserStillAllowed(TokenValidatedContext context)
    {
        var principal = context.Principal!;
        if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            context.Fail("Token không có claim sub hợp lệ");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var current = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.Role })
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        if (current is null || !current.IsActive || EnumText<UserRole>.ToText(current.Role) != principal.FindFirstValue(ClaimType))
            context.Fail("Tài khoản đã bị khoá hoặc đổi vai trò");
    }
}
