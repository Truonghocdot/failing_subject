using System.Security.Claims;
using backend.Auth;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

public static class WalletEndpoints
{
    public static IEndpointRouteBuilder MapWalletEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/wallets").WithTags("Ví").RequireAuthorization();

        group.MapGet("", List).WithName("ListWallets").WithSummary("Danh sách ví + số dư + tổng tài sản");
        group.MapPost("", Create).WithName("CreateWallet").WithSummary("Thêm ví");
        group.MapPut("/{id:long}", Update).WithName("UpdateWallet").WithSummary("Sửa / lưu trữ ví");
        group.MapDelete("/{id:long}", Delete).WithName("DeleteWallet").WithSummary("Xoá ví chưa có giao dịch");

        return app;
    }

    private static async Task<Ok<WalletListDto>> List(bool? includeArchived, ClaimsPrincipal user, WalletService wallets, CancellationToken ct) =>
        TypedResults.Ok(await wallets.ListAsync(user.GetUserId(), includeArchived ?? false, ct));

    private static async Task<Results<Created<WalletDto>, ValidationProblem>> Create(
        WalletRequest body, ClaimsPrincipal user, WalletService wallets, CancellationToken ct)
    {
        if (!TryRead(body, out var input, out var problem)) return problem;
        var dto = await wallets.CreateAsync(user.GetUserId(), input, ct);
        return TypedResults.Created($"/api/wallets/{dto.Id}", dto);
    }

    private static async Task<Results<Ok<WalletDto>, ValidationProblem>> Update(
        long id, WalletRequest body, ClaimsPrincipal user, WalletService wallets, CancellationToken ct)
    {
        if (!TryRead(body, out var input, out var problem)) return problem;
        return TypedResults.Ok(await wallets.UpdateAsync(user.GetUserId(), id, input, ct));
    }

    private static async Task<NoContent> Delete(long id, ClaimsPrincipal user, WalletService wallets, CancellationToken ct)
    {
        await wallets.DeleteAsync(user.GetUserId(), id, ct);
        return TypedResults.NoContent();
    }

    private static bool TryRead(WalletRequest body, out WalletInput input, out ValidationProblem problem)
    {
        var v = new RequestValidator()
            .Text("name", body.Name, 50, out var name, "Tên ví")
            .Enum<WalletType>("type", body.Type, out var type, "Loại ví")
            .Money("openingBalance", body.OpeningBalance, out var opening, "Số dư đầu", positive: false);
        // Màu bỏ trống → màu mặc định theo loại ví
        var color = string.IsNullOrWhiteSpace(body.Color) ? Data.DefaultData.WalletColors.GetValueOrDefault(type, "#64748b") : body.Color;
        v.Color(color, out color);

        input = new WalletInput(name, type, opening, color, body.IsArchived ?? false);
        problem = v.IsValid ? null! : v.Problem();
        return v.IsValid;
    }
}
