using System.Security.Claims;
using backend.Auth;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

/// <summary>/api/reports/* – khoảng ngày giống bộ lọc giao dịch (mặc định tháng này, tối đa 366 ngày).</summary>
public static class ReportEndpoints
{
    public const int MaxDailyTrendDays = 92;

    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Báo cáo").RequireAuthorization();

        group.MapGet("/summary", Summary).WithName("ReportSummary").WithSummary("Tổng thu / chi / chênh lệch / chi trung bình ngày");
        group.MapGet("/by-category", ByCategory).WithName("ReportByCategory").WithSummary("Tổng theo danh mục (kind=expense|income)");
        group.MapGet("/trend", Trend).WithName("ReportTrend").WithSummary("Thu / chi theo ngày hoặc tháng, đủ mọi kỳ");

        return app;
    }

    private static async Task<Results<Ok<ReportSummaryDto>, ValidationProblem>> Summary(
        [AsParameters] ReportQuery query, ClaimsPrincipal user, ReportService reports, AppClock clock, CancellationToken ct)
    {
        var v = new RequestValidator().DateRange(query.From, query.To, clock.CurrentMonth, out var from, out var to);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await reports.SummaryAsync(user.GetUserId(), from, to, ct));
    }

    private static async Task<Results<Ok<IReadOnlyList<CategoryReportItem>>, ValidationProblem>> ByCategory(
        [AsParameters] ReportQuery query, ClaimsPrincipal user, ReportService reports, AppClock clock, CancellationToken ct)
    {
        var v = new RequestValidator().DateRange(query.From, query.To, clock.CurrentMonth, out var from, out var to);
        var kind = CategoryKind.Expense;
        if (!string.IsNullOrWhiteSpace(query.Kind)) v.Enum("kind", query.Kind, out kind, "Loại danh mục");
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await reports.ByCategoryAsync(user.GetUserId(), from, to, kind, ct));
    }

    private static async Task<Results<Ok<IReadOnlyList<TrendPoint>>, ValidationProblem>> Trend(
        [AsParameters] ReportQuery query, ClaimsPrincipal user, ReportService reports, AppClock clock, CancellationToken ct)
    {
        var groupBy = TrendGroup.Month;
        var v = new RequestValidator();
        if (!string.IsNullOrWhiteSpace(query.GroupBy)) v.Enum("groupBy", query.GroupBy, out groupBy, "Kiểu nhóm");
        // Theo ngày quá dài thì biểu đồ không đọc được (và trả quá nhiều điểm)
        v.DateRange(query.From, query.To, clock.CurrentMonth, out var from, out var to,
            groupBy == TrendGroup.Day ? MaxDailyTrendDays : RequestValidator.MaxRangeDays);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await reports.TrendAsync(user.GetUserId(), from, to, groupBy, ct));
    }
}
