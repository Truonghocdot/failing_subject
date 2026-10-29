using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

/// <summary>
/// Tài khoản nhân viên (lễ tân / quản trị) – chỉ admin dùng (docs/02-nghiep-vu.md §1):
/// admin không tự hạ quyền / khoá chính mình; hệ thống luôn còn ít nhất 1 admin hoạt động.
/// Khách (guest) không quản lý ở đây – xem /api/admin/guests (Phase 5).
/// </summary>
public sealed class StaffService(AppDbContext db, AuthService auth)
{
    private const string NotFound = "Không tìm thấy nhân viên";

    private static readonly UserRole[] StaffRoles = [UserRole.Staff, UserRole.Admin];

    public async Task<List<StaffDto>> ListAsync(string? q, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().Where(u => StaffRoles.Contains(u.Role));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.Email.Contains(term) || u.FullName.Contains(term) || (u.Phone != null && u.Phone.Contains(term)));
        }
        return await query
            .OrderByDescending(u => u.IsActive).ThenBy(u => u.FullName)
            .Select(u => new StaffDto(u.Id, u.Email, u.FullName, u.Phone, u.Role, u.IsActive, u.LastLoginAt, u.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<StaffDto> CreateAsync(string email, string password, string fullName, string? phone, UserRole role, CancellationToken ct)
    {
        var normalized = AuthService.NormalizeEmail(email);
        if (await db.Users.AnyAsync(u => u.Email == normalized, ct))
            throw ApiProblemException.Conflict("Email đã được sử dụng", "email");

        var user = new User
        {
            Email = normalized,
            PasswordHash = AuthService.HashPassword(password),
            FullName = fullName,
            Phone = phone,
            Role = role,
        };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw ApiProblemException.Conflict("Email đã được sử dụng", "email");
        }
        return ToDto(user);
    }

    public async Task<StaffDto> UpdateAsync(
        Guid actorId, Guid id, string fullName, string? phone, UserRole role, bool isActive, CancellationToken ct)
    {
        if (id == actorId && (role != UserRole.Admin || !isActive))
            throw ApiProblemException.Conflict("Không thể tự hạ quyền hoặc khoá tài khoản của chính mình");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var activeAdmins = await LockActiveAdminsAsync(ct);
        var user = await FindAsync(id, ct);

        var losesAdmin = user.Role == UserRole.Admin && user.IsActive && (role != UserRole.Admin || !isActive);
        if (losesAdmin && activeAdmins.All(a => a == id))
            throw ApiProblemException.Conflict("Hệ thống phải còn ít nhất 1 quản trị viên đang hoạt động");

        var accessChanged = user.Role != role || user.IsActive != isActive;
        user.FullName = fullName;
        user.Phone = phone;
        user.Role = role;
        user.IsActive = isActive;
        await db.SaveChangesAsync(ct);
        // Khoá / đổi vai trò → đăng xuất mọi thiết bị; token đang dùng bị Roles.ValidateUserStillAllowed từ chối ngay
        if (accessChanged) await auth.RevokeAllAsync(id, ct);
        await tx.CommitAsync(ct);
        return ToDto(user);
    }

    /// <summary>Admin đặt mật khẩu mới cho nhân viên quên mật khẩu → đăng xuất mọi thiết bị của nhân viên đó.</summary>
    public async Task ResetPasswordAsync(Guid id, string password, CancellationToken ct)
    {
        var user = await FindAsync(id, ct);
        user.PasswordHash = AuthService.HashPassword(password);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        await auth.RevokeAllAsync(id, ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Khoá các dòng admin đang hoạt động tới hết transaction: 2 admin hạ quyền nhau cùng lúc thì request sau phải chờ
    /// và thấy kết quả của request trước → không bao giờ còn 0 admin.
    /// </summary>
    private async Task<List<Guid>> LockActiveAdminsAsync(CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("SELECT id FROM users WHERE role = 'admin' AND is_active = TRUE FOR UPDATE", ct);
        // Đọc thường ngay sau khi đã giữ khoá: snapshot của transaction tạo từ đây nên thấy thay đổi đã commit trước đó
        return await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Admin && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);
    }

    private async Task<User> FindAsync(Guid id, CancellationToken ct) =>
        await db.Users.SingleOrDefaultAsync(u => u.Id == id && StaffRoles.Contains(u.Role), ct)
        ?? throw ApiProblemException.NotFound(NotFound);

    private static StaffDto ToDto(User u) => new(u.Id, u.Email, u.FullName, u.Phone, u.Role, u.IsActive, u.LastLoginAt, u.CreatedAt);
}
