using System.Security.Claims;
using backend.Auth;
using backend.Contracts;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

public static class BudgetEndpoints
{
    public static IEndpointRouteBuilder MapBudgetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/budgets").WithTags("Ngân sách").RequireAuthorization();

        group.MapGet("", List).WithName("ListBudgets").WithSummary("Ngân sách 1 tháng kèm đã chi / % / trạng thái (mặc định tháng này)");
        group.MapPut("", Upsert).WithName("UpsertBudget").WithSummary("Đặt ngân sách (tháng, danh mục chi) – có rồi thì sửa");
        group.MapDelete("/{id:long}", Delete).WithName("DeleteBudget");
        group.MapPost("/copy", Copy).WithName("CopyBudgets").WithSummary("Sao chép ngân sách sang tháng khác (bỏ qua danh mục đã có)");

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<BudgetDto>>, ValidationProblem>> List(
        string? month, ClaimsPrincipal user, BudgetService budgets, AppClock clock, CancellationToken ct)
    {
        var value = clock.CurrentMonth.From;
        if (!string.IsNullOrWhiteSpace(month))
        {
            var v = new RequestValidator().Month("month", month, out value, MaxMonth(clock));
            if (!v.IsValid) return v.Problem();
        }
        return TypedResults.Ok(await budgets.ListAsync(user.GetUserId(), value, ct));
    }

    private static async Task<Results<Ok<BudgetDto>, ValidationProblem>> Upsert(
        BudgetRequest body, ClaimsPrincipal user, BudgetService budgets, AppClock clock, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Month("month", body.Month, out var month, MaxMonth(clock))
            .RequiredId("categoryId", body.CategoryId, out var categoryId, "Danh mục")
            .Money("amount", body.Amount, out var amount, "Số tiền ngân sách", positive: true);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await budgets.UpsertAsync(user.GetUserId(), month, categoryId, amount, ct));
    }

    private static async Task<NoContent> Delete(long id, ClaimsPrincipal user, BudgetService budgets, CancellationToken ct)
    {
        await budgets.DeleteAsync(user.GetUserId(), id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<CopyBudgetsResult>, ValidationProblem>> Copy(
        CopyBudgetsRequest body, ClaimsPrincipal user, BudgetService budgets, AppClock clock, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Month("from", body.From, out var from, MaxMonth(clock), "Tháng nguồn")
            .Month("to", body.To, out var to, MaxMonth(clock), "Tháng đích");
        if (v.IsValid && from == to) v.Add("to", "Tháng đích phải khác tháng nguồn");
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await budgets.CopyAsync(user.GetUserId(), from, to, ct));
    }

    // Lập ngân sách trước tối đa 1 năm
    private static DateOnly MaxMonth(AppClock clock) => clock.CurrentMonth.From.AddYears(1);
}
