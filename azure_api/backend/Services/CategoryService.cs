using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed record CategoryInput(string Name, string Icon, string Color, bool IsArchived);

/// <summary>Danh mục thu / chi (docs/02-nghiep-vu.md §3) – kind không đổi được sau khi tạo.</summary>
public sealed class CategoryService(AppDbContext db)
{
    private const string NotFound = "Không tìm thấy danh mục";

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(Guid userId, CategoryKind? kind, bool includeArchived, CancellationToken ct) =>
        await db.Categories.AsNoTracking()
            .Where(c => c.UserId == userId && (kind == null || c.Kind == kind) && (includeArchived || !c.IsArchived))
            .OrderBy(c => c.Kind).ThenBy(c => c.IsArchived).ThenBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Kind, c.Name, c.Icon, c.Color, c.IsArchived, c.SortOrder))
            .ToListAsync(ct);

    public async Task<CategoryDto> CreateAsync(Guid userId, CategoryKind kind, CategoryInput input, CancellationToken ct)
    {
        await EnsureUniqueNameAsync(userId, kind, input.Name, exceptId: null, ct);
        var maxOrder = await db.Categories.Where(c => c.UserId == userId && c.Kind == kind).MaxAsync(c => (int?)c.SortOrder, ct) ?? -1;

        var category = new Category
        {
            UserId = userId,
            Kind = kind,
            Name = input.Name,
            Icon = input.Icon,
            Color = input.Color,
            SortOrder = maxOrder + 1,
        };
        db.Categories.Add(category);
        await SaveAsync(ct);
        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(Guid userId, long id, CategoryInput input, CancellationToken ct)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct)
            ?? throw ApiProblemException.NotFound(NotFound);
        await EnsureUniqueNameAsync(userId, category.Kind, input.Name, exceptId: id, ct);

        category.Name = input.Name;
        category.Icon = input.Icon;
        category.Color = input.Color;
        category.IsArchived = input.IsArchived;
        await SaveAsync(ct);
        return ToDto(category);
    }

    public async Task DeleteAsync(Guid userId, long id, CancellationToken ct)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct)
            ?? throw ApiProblemException.NotFound(NotFound);
        if (await db.Transactions.AnyAsync(t => t.CategoryId == id, ct) || await db.Budgets.AnyAsync(b => b.CategoryId == id, ct))
            throw ApiProblemException.Conflict("Danh mục đang được dùng – hãy lưu trữ thay vì xoá");

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    private static CategoryDto ToDto(Category c) => new(c.Id, c.Kind, c.Name, c.Icon, c.Color, c.IsArchived, c.SortOrder);

    private async Task EnsureUniqueNameAsync(Guid userId, CategoryKind kind, string name, long? exceptId, CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(c => c.UserId == userId && c.Kind == kind && c.Name == name && c.Id != exceptId, ct))
            throw ApiProblemException.Conflict("Tên danh mục đã tồn tại", "name");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw ApiProblemException.Conflict("Tên danh mục đã tồn tại", "name");
        }
    }
}
