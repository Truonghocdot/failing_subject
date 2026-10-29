using System.Security.Claims;
using backend.Auth;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

/// <summary>
/// /api/admin/* – cả nhóm yêu cầu policy Staff (lễ tân / quản trị); khách đăng nhập gọi vào → 403.
/// Nhóm con chỉ dành cho admin thêm RequireAuthorization(Roles.AdminPolicy).
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(Roles.StaffPolicy);
        admin.MapAdminCatalog();
        admin.MapAdminBookings();

        admin.MapGet("/guests", ListGuests).WithTags("Quản trị – khách").WithName("ListGuests")
            .WithSummary("Khách gộp theo SĐT (q: tên / SĐT / email, page) – lịch sử: /api/admin/bookings?q=<SĐT>");

        var reports = admin.MapGroup("").WithTags("Quản trị – báo cáo & đánh giá").RequireAuthorization(Roles.AdminPolicy);
        reports.MapGet("/reports", Report).WithName("Report")
            .WithSummary($"Công suất, ADR, RevPAR, doanh thu theo tháng / homestay / nguồn, tỉ lệ huỷ (from, to ≤ {ReportService.MaxDays} ngày, homestayId)");
        reports.MapGet("/reviews", ListReviews).WithName("AdminListReviews").WithSummary("Mọi đánh giá (homestayId, hidden=true|false, page)");
        reports.MapPut("/reviews/{id:long}", SetReviewVisibility).WithName("SetReviewVisibility").WithSummary("Ẩn / hiện đánh giá {isHidden}");

        var users = admin.MapGroup("/users").WithTags("Quản trị – nhân viên").RequireAuthorization(Roles.AdminPolicy);
        users.MapGet("", ListStaff).WithName("ListStaff").WithSummary("Danh sách lễ tân / quản trị (q: email, tên, SĐT)");
        users.MapPost("", CreateStaff).WithName("CreateStaff").WithSummary("Tạo tài khoản nhân viên");
        users.MapPut("/{id:guid}", UpdateStaff).WithName("UpdateStaff").WithSummary("Sửa thông tin, đổi vai trò, khoá / mở khoá");
        users.MapPost("/{id:guid}/reset-password", ResetPassword).WithName("ResetStaffPassword")
            .WithSummary("Đặt mật khẩu mới – đăng xuất mọi thiết bị của nhân viên đó");

        return app;
    }

    private static readonly UserRole[] StaffRoles = [UserRole.Staff, UserRole.Admin];

    private static async Task<Results<Ok<PagedResult<GuestDto>>, ValidationProblem>> ListGuests(
        string? q, int? page, GuestService guests, CancellationToken ct)
    {
        var v = new RequestValidator()
            .OptionalText("q", q, 100, out var term, "Từ khoá")
            .Int("page", page, 1, 10_000, 1, out var pageNumber, "Trang");
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await guests.ListAsync(term, pageNumber, ct));
    }

    private static async Task<Results<Ok<ReportDto>, ValidationProblem>> Report(
        DateOnly? from, DateOnly? to, long? homestayId, AppClock clock, ReportService reports, CancellationToken ct)
    {
        var today = clock.Today;
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var v = new RequestValidator().DateRange(start, to ?? start.AddMonths(1).AddDays(-1), ReportService.MaxDays, out var f, out var t);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await reports.BuildAsync(f, t, homestayId, ct));
    }

    private static async Task<Results<Ok<PagedResult<AdminReviewDto>>, ValidationProblem>> ListReviews(
        long? homestayId, bool? hidden, int? page, ReviewService reviews, CancellationToken ct)
    {
        var v = new RequestValidator().Int("page", page, 1, 10_000, 1, out var pageNumber, "Trang");
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await reviews.ListAdminAsync(homestayId, hidden, pageNumber, ct));
    }

    private static async Task<Results<NoContent, ValidationProblem>> SetReviewVisibility(
        long id, ReviewVisibilityRequest body, ReviewService reviews, CancellationToken ct)
    {
        if (body.IsHidden is null) return new RequestValidator().Add("isHidden", "Vui lòng chọn ẩn hay hiện").Problem();
        await reviews.SetHiddenAsync(id, body.IsHidden.Value, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<List<StaffDto>>> ListStaff(string? q, StaffService staff, CancellationToken ct) =>
        TypedResults.Ok(await staff.ListAsync(q, ct));

    private static async Task<Results<Created<StaffDto>, ValidationProblem>> CreateStaff(
        CreateStaffRequest body, StaffService staff, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Email(body.Email, out var email)
            .Password(body.Password, out var password)
            .Text("fullName", body.FullName, 100, out var fullName, "Họ tên")
            .Phone("phone", body.Phone, out var phone, required: false)
            .Enum("role", body.Role, out UserRole role, "Vai trò", StaffRoles);
        if (!v.IsValid) return v.Problem();

        var created = await staff.CreateAsync(email, password, fullName, phone, role, ct);
        return TypedResults.Created($"/api/admin/users/{created.Id}", created);
    }

    private static async Task<Results<Ok<StaffDto>, ValidationProblem>> UpdateStaff(
        Guid id, UpdateStaffRequest body, ClaimsPrincipal user, StaffService staff, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Text("fullName", body.FullName, 100, out var fullName, "Họ tên")
            .Phone("phone", body.Phone, out var phone, required: false)
            .Enum("role", body.Role, out UserRole role, "Vai trò", StaffRoles);
        if (body.IsActive is null) v.Add("isActive", "Vui lòng chọn trạng thái tài khoản");
        if (!v.IsValid) return v.Problem();

        return TypedResults.Ok(await staff.UpdateAsync(user.GetUserId(), id, fullName, phone, role, body.IsActive!.Value, ct));
    }

    private static async Task<Results<NoContent, ValidationProblem>> ResetPassword(
        Guid id, ResetPasswordRequest body, StaffService staff, CancellationToken ct)
    {
        var v = new RequestValidator().Password(body.Password, out var password);
        if (!v.IsValid) return v.Problem();

        await staff.ResetPasswordAsync(id, password, ct);
        return TypedResults.NoContent();
    }
}
