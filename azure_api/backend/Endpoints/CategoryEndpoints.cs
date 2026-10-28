using System.Security.Claims;
using backend.Auth;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories").WithTags("Danh mục").RequireAuthorization();

        group.MapGet("", List).WithName("ListCategories").WithSummary("Danh mục (lọc theo kind)");
        group.MapPost("", Create).WithName("CreateCategory").WithSummary("Thêm danh mục");
        group.MapPut("/{id:long}", Update).WithName("UpdateCategory").WithSummary("Sửa / lưu trữ danh mục (không đổi kind)");
        group.MapDelete("/{id:long}", Delete).WithName("DeleteCategory").WithSummary("Xoá danh mục chưa dùng");

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<CategoryDto>>, ValidationProblem>> List(
        string? kind, bool? includeArchived, ClaimsPrincipal user, CategoryService categories, CancellationToken ct)
    {
        CategoryKind? parsed = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            var v = new RequestValidator().Enum<CategoryKind>("kind", kind, out var k, "Loại danh mục");
            if (!v.IsValid) return v.Problem();
            parsed = k;
        }
        return TypedResults.Ok(await categories.ListAsync(user.GetUserId(), parsed, includeArchived ?? false, ct));
    }

    private static async Task<Results<Created<CategoryDto>, ValidationProblem>> Create(
        CategoryRequest body, ClaimsPrincipal user, CategoryService categories, CancellationToken ct)
    {
        var v = Validate(body, out var input).Enum<CategoryKind>("kind", body.Kind, out var kind, "Loại danh mục");
        if (!v.IsValid) return v.Problem();
        var dto = await categories.CreateAsync(user.GetUserId(), kind, input, ct);
        return TypedResults.Created($"/api/categories/{dto.Id}", dto);
    }

    private static async Task<Results<Ok<CategoryDto>, ValidationProblem>> Update(
        long id, CategoryRequest body, ClaimsPrincipal user, CategoryService categories, CancellationToken ct)
    {
        var v = Validate(body, out var input);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await categories.UpdateAsync(user.GetUserId(), id, input, ct));
    }

    private static async Task<NoContent> Delete(long id, ClaimsPrincipal user, CategoryService categories, CancellationToken ct)
    {
        await categories.DeleteAsync(user.GetUserId(), id, ct);
        return TypedResults.NoContent();
    }

    private static RequestValidator Validate(CategoryRequest body, out CategoryInput input)
    {
        var v = new RequestValidator()
            .Text("name", body.Name, 50, out var name, "Tên danh mục")
            .Icon(body.Icon, out var icon)
            .Color(body.Color, out var color);
        input = new CategoryInput(name, icon, color, body.IsArchived ?? false);
        return v;
    }
}
