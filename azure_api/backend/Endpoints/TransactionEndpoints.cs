using System.Security.Claims;
using backend.Auth;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

public static class TransactionEndpoints
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/transactions").WithTags("Giao dịch").RequireAuthorization();

        group.MapGet("", List).WithName("ListTransactions").WithSummary("Lọc + tìm + phân trang, kèm tổng thu/chi của bộ lọc");
        group.MapGet("/export", Export).WithName("ExportTransactions").WithSummary("Xuất CSV theo bộ lọc (tối đa 10 000 dòng)");
        group.MapGet("/{id:long}", Get).WithName("GetTransaction");
        group.MapPost("", Create).WithName("CreateTransaction").WithSummary("Thêm thu / chi / chuyển tiền");
        group.MapPut("/{id:long}", Update).WithName("UpdateTransaction");
        group.MapDelete("/{id:long}", Delete).WithName("DeleteTransaction");

        return app;
    }

    private static async Task<Results<Ok<PagedTransactions>, ValidationProblem>> List(
        [AsParameters] TransactionQuery query, ClaimsPrincipal user, TransactionService transactions, AppClock clock, CancellationToken ct)
    {
        var v = ReadFilter(query, clock, out var filter)
            .Range("page", query.Page, 1, 10_000, 1, out var page)
            .Range("pageSize", query.PageSize, 1, 100, 20, out var pageSize);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await transactions.ListAsync(user.GetUserId(), filter, page, pageSize, ct));
    }

    private static async Task<Results<FileContentHttpResult, ValidationProblem>> Export(
        [AsParameters] TransactionQuery query, ClaimsPrincipal user, TransactionService transactions, AppClock clock, CancellationToken ct)
    {
        var v = ReadFilter(query, clock, out var filter);
        if (!v.IsValid) return v.Problem();
        var bytes = await transactions.ExportCsvAsync(user.GetUserId(), filter, ct);
        return TypedResults.File(bytes, "text/csv; charset=utf-8", $"giao-dich_{filter.From:yyyy-MM-dd}_{filter.To:yyyy-MM-dd}.csv");
    }

    private static async Task<Ok<TransactionDto>> Get(long id, ClaimsPrincipal user, TransactionService transactions, CancellationToken ct) =>
        TypedResults.Ok(await transactions.GetAsync(user.GetUserId(), id, ct));

    private static async Task<Results<Created<TransactionDto>, ValidationProblem>> Create(
        TransactionRequest body, ClaimsPrincipal user, TransactionService transactions, AppClock clock, CancellationToken ct)
    {
        var v = ReadInput(body, clock, out var input);
        if (!v.IsValid) return v.Problem();
        var dto = await transactions.CreateAsync(user.GetUserId(), input, ct);
        return TypedResults.Created($"/api/transactions/{dto.Id}", dto);
    }

    private static async Task<Results<Ok<TransactionDto>, ValidationProblem>> Update(
        long id, TransactionRequest body, ClaimsPrincipal user, TransactionService transactions, AppClock clock, CancellationToken ct)
    {
        var v = ReadInput(body, clock, out var input);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await transactions.UpdateAsync(user.GetUserId(), id, input, ct));
    }

    private static async Task<NoContent> Delete(long id, ClaimsPrincipal user, TransactionService transactions, CancellationToken ct)
    {
        await transactions.DeleteAsync(user.GetUserId(), id, ct);
        return TypedResults.NoContent();
    }

    /// <summary>Quy tắc hình thức theo loại giao dịch (docs/02-nghiep-vu.md §4).</summary>
    private static RequestValidator ReadInput(TransactionRequest body, AppClock clock, out TransactionInput input)
    {
        var v = new RequestValidator()
            .Enum<TransactionKind>("kind", body.Kind, out var kind, "Loại giao dịch")
            .RequiredId("walletId", body.WalletId, out var walletId, "Ví")
            .Money("amount", body.Amount, out var amount, "Số tiền", positive: true)
            .Date("occurredOn", body.OccurredOn, out var occurredOn, RequestValidator.MinDate, clock.Today.AddYears(1), "Ngày")
            .OptionalText("note", body.Note, 500, out var note, "Ghi chú");

        if (kind == TransactionKind.Transfer)
        {
            if (body.ToWalletId is not > 0) v.Add("toWalletId", "Vui lòng chọn ví nhận");
            else if (body.ToWalletId == body.WalletId) v.Add("toWalletId", "Ví nhận phải khác ví nguồn");
        }
        else if (body.Kind is not null)
        {
            v.RequiredId("categoryId", body.CategoryId, out _, "Danh mục");
        }

        input = new TransactionInput(kind, walletId, body.ToWalletId, body.CategoryId, amount, occurredOn, note);
        return v;
    }

    private static RequestValidator ReadFilter(TransactionQuery query, AppClock clock, out TransactionFilter filter)
    {
        var v = new RequestValidator().DateRange(query.From, query.To, clock.CurrentMonth, out var from, out var to);

        TransactionKind? kind = null;
        if (!string.IsNullOrWhiteSpace(query.Kind))
        {
            v.Enum<TransactionKind>("kind", query.Kind, out var k, "Loại giao dịch");
            kind = k;
        }
        var q = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
        if (q?.Length > 100) v.Add("q", "Từ khoá tối đa 100 ký tự");

        filter = new TransactionFilter(from, to, query.WalletId, query.CategoryId, kind, q);
        return v;
    }
}
