using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using backend.Auth;
using backend.Contracts;
using backend.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

/// <summary>Vai trò guest / staff / admin, policy khu quản trị, quản lý nhân viên (docs/02-nghiep-vu.md §1).</summary>
public class RoleTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task SeedAdmin_CanLogIn_WithAdminRole()
    {
        var admin = await factory.LoginSeedAdminAsync();

        Assert.Equal(UserRole.Admin, admin.Auth.User.Role);
        Assert.Equal(ApiFactory.AdminEmail, admin.Auth.User.Email);
    }

    [Fact]
    public async Task Seeder_RunTwice_DoesNotCreateSecondAdmin()
    {
        factory.CreateRawClient(); // bảo đảm DB test đã tạo + seed lần đầu
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<backend.Data.Seeder>().RunAsync();

        var seeded = await factory.WithDbAsync(db => db.Users.CountAsync(u => u.Email == ApiFactory.AdminEmail));
        Assert.Equal(1, seeded);
    }

    [Fact]
    public async Task AdminUsersApi_AnonymousIs401_GuestAndStaffAre403_AdminIs200()
    {
        var guest = await factory.CreateGuestAsync();
        var staff = await factory.CreateStaffAsync();
        var admin = await factory.LoginSeedAdminAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateRawClient().GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.Client.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.Client.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.Client.GetAsync("/api/admin/users")).StatusCode);
    }

    [Theory]
    [InlineData("guest", false, false)]
    [InlineData("staff", true, false)]
    [InlineData("admin", true, true)]
    public async Task Policies_MatchRoleTable(string role, bool staffAllowed, bool adminAllowed)
    {
        using var scope = factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(Roles.ClaimType, role)], "test", "sub", Roles.ClaimType));

        Assert.Equal(staffAllowed, (await authorization.AuthorizeAsync(principal, Roles.StaffPolicy)).Succeeded);
        Assert.Equal(adminAllowed, (await authorization.AuthorizeAsync(principal, Roles.AdminPolicy)).Succeeded);
    }

    [Fact]
    public async Task Admin_CreatesStaff_WhoLogsInWithStaffRole()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var email = ApiFactory.NewEmail();

        var response = await admin.Client.PostAsJsonAsync("/api/admin/users",
            new { email = email.ToUpperInvariant(), password = ApiFactory.Password, fullName = " Hà Lễ Tân ", phone = "0901 234 567", role = "staff" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.ReadAsync<StaffDto>())!;
        Assert.Equal(email, created.Email);
        Assert.Equal("Hà Lễ Tân", created.FullName);
        Assert.Equal("0901234567", created.Phone);
        Assert.Equal(UserRole.Staff, created.Role);
        Assert.True(created.IsActive);

        var staff = await factory.LoginAsync(email, ApiFactory.Password);
        Assert.Equal(UserRole.Staff, staff.Auth.User.Role);

        var list = await admin.Client.GetJsonAsync<List<StaffDto>>($"/api/admin/users?q={Uri.EscapeDataString(email)}");
        Assert.Equal(created.Id, Assert.Single(list!).Id);
    }

    [Fact]
    public async Task StaffList_DoesNotIncludeGuests()
    {
        var guest = await factory.CreateGuestAsync();
        var admin = await factory.LoginSeedAdminAsync();

        var list = await admin.Client.GetJsonAsync<List<StaffDto>>($"/api/admin/users?q={Uri.EscapeDataString(guest.Auth.User.Email)}");
        var update = await admin.Client.PutAsJsonAsync($"/api/admin/users/{guest.Auth.User.Id}",
            new { fullName = "X", role = "staff", isActive = true });

        Assert.Empty(list!);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode); // không biến khách thành nhân viên qua route này
    }

    [Theory]
    [InlineData("guest")]
    [InlineData("owner")]
    [InlineData("")]
    public async Task CreateStaff_InvalidRole_Returns400(string role)
    {
        var admin = await factory.LoginSeedAdminAsync();

        var response = await admin.Client.PostAsJsonAsync("/api/admin/users",
            new { email = ApiFactory.NewEmail(), password = ApiFactory.Password, fullName = "A", role });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var expected = role.Length == 0 ? "Vui lòng chọn vai trò" : "Vai trò không hợp lệ (chọn: staff, admin)";
        Assert.Equal(expected, await response.FieldErrorAsync("role"));
    }

    [Fact]
    public async Task CreateStaff_ExistingEmail_Returns409()
    {
        var guest = await factory.CreateGuestAsync();
        var admin = await factory.LoginSeedAdminAsync();

        var response = await admin.Client.PostAsJsonAsync("/api/admin/users",
            new { email = guest.Auth.User.Email, password = ApiFactory.Password, fullName = "A", role = "staff" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Email đã được sử dụng", await response.FieldErrorAsync("email"));
    }

    [Fact]
    public async Task Admin_CannotDemoteOrLockThemselves()
    {
        var admin = await factory.CreateStaffAsync("admin");
        var id = admin.Auth.User.Id;

        var demote = await admin.Client.PutAsJsonAsync($"/api/admin/users/{id}", new { fullName = "Tôi", role = "staff", isActive = true });
        var lockSelf = await admin.Client.PutAsJsonAsync($"/api/admin/users/{id}", new { fullName = "Tôi", role = "admin", isActive = false });
        var rename = await admin.Client.PutAsJsonAsync($"/api/admin/users/{id}", new { fullName = "Tên mới", role = "admin", isActive = true });

        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
        Assert.Equal("Không thể tự hạ quyền hoặc khoá tài khoản của chính mình", await demote.DetailAsync());
        Assert.Equal(HttpStatusCode.Conflict, lockSelf.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
    }

    [Fact]
    public async Task LockingStaff_RejectsTokenImmediately_BlocksLoginAndRefresh()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        Assert.Equal(HttpStatusCode.OK, (await staff.Client.GetAsync("/api/me")).StatusCode);

        var locked = await admin.Client.PutAsJsonAsync($"/api/admin/users/{staff.Auth.User.Id}",
            new { fullName = "Bị khoá", role = "staff", isActive = false });
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        Assert.False((await locked.ReadAsync<StaffDto>())!.IsActive);

        // access token còn hạn nhưng bị từ chối ngay, không chờ 15 phút
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.Client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateRawClient().PostWithRefreshCookie("/api/auth/refresh", staff.RefreshToken)).StatusCode);

        var login = await factory.CreateRawClient().PostAsJsonAsync("/api/auth/login",
            new { email = staff.Auth.User.Email, password = ApiFactory.Password });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal("Tài khoản đã bị khoá – liên hệ quản trị viên", await login.DetailAsync());

        // mở khoá → đăng nhập lại được
        await admin.Client.PutAsJsonAsync($"/api/admin/users/{staff.Auth.User.Id}", new { fullName = "Mở", role = "staff", isActive = true });
        Assert.Equal(UserRole.Staff, (await factory.LoginAsync(staff.Auth.User.Email, ApiFactory.Password)).Auth.User.Role);
    }

    [Fact]
    public async Task LockedAccount_WrongPassword_StillSaysWrongCredentials()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();
        await admin.Client.PutAsJsonAsync($"/api/admin/users/{staff.Auth.User.Id}", new { fullName = "K", role = "staff", isActive = false });

        var login = await factory.CreateRawClient().PostAsJsonAsync("/api/auth/login",
            new { email = staff.Auth.User.Email, password = "sai-mat-khau" });

        // không lộ "bị khoá" cho người không biết mật khẩu
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal("Email hoặc mật khẩu không đúng", await login.DetailAsync());
    }

    [Fact]
    public async Task PromotingStaffToAdmin_OldTokenRejected_NewLoginHasAdminRole()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();

        var promote = await admin.Client.PutAsJsonAsync($"/api/admin/users/{staff.Auth.User.Id}",
            new { fullName = "Lên admin", role = "admin", isActive = true });

        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);
        // token mang role cũ → 401, FE sẽ đăng nhập / refresh lại
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.Client.GetAsync("/api/me")).StatusCode);
        var again = await factory.LoginAsync(staff.Auth.User.Email, ApiFactory.Password);
        Assert.Equal(UserRole.Admin, again.Auth.User.Role);
        Assert.Equal(HttpStatusCode.OK, (await again.Client.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task ResetPassword_LogsOutStaff_NewPasswordWorks()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();

        var reset = await admin.Client.PostAsJsonAsync($"/api/admin/users/{staff.Auth.User.Id}/reset-password", new { password = "matkhaumoi789" });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateRawClient().PostWithRefreshCookie("/api/auth/refresh", staff.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateRawClient().PostAsJsonAsync("/api/auth/login",
            new { email = staff.Auth.User.Email, password = "matkhaumoi789" })).StatusCode);
    }

    [Fact]
    public async Task UpdateUnknownStaff_Returns404()
    {
        var admin = await factory.LoginSeedAdminAsync();

        var response = await admin.Client.PutAsJsonAsync($"/api/admin/users/{Guid.NewGuid()}", new { fullName = "X", role = "staff", isActive = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Không tìm thấy nhân viên", await response.DetailAsync());
    }

    [Fact]
    public async Task UpdateStaff_MissingIsActive_Returns400()
    {
        var admin = await factory.LoginSeedAdminAsync();
        var staff = await factory.CreateStaffAsync();

        var response = await admin.Client.PutAsJsonAsync($"/api/admin/users/{staff.Auth.User.Id}", new { fullName = "X", role = "staff" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Vui lòng chọn trạng thái tài khoản", await response.FieldErrorAsync("isActive"));
    }
}
