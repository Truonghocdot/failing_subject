using Microsoft.Extensions.Options;

namespace backend.Services;

public sealed class UploadOptions
{
    public const string Section = "Uploads";

    /// <summary>Thư mục ảnh, tương đối theo thư mục backend (hoặc đường dẫn tuyệt đối).</summary>
    public string Path { get; init; } = "storage/uploads";
}

/// <summary>Ảnh homestay trên đĩa: {Root}/{homestayId}/{guid}.jpg|png|webp – phục vụ tĩnh ở /uploads/... (Program.cs).</summary>
public sealed class UploadStorage
{
    public const string RequestPath = "/uploads";
    public const long MaxBytes = 5 * 1024 * 1024;
    public const int MaxPhotosPerHomestay = 20;

    public UploadStorage(IOptions<UploadOptions> options, IWebHostEnvironment env)
    {
        Root = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.Path));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public static string Url(long homestayId, string fileName) => $"{RequestPath}/{homestayId}/{fileName}";

    public async Task SaveAsync(long homestayId, string fileName, byte[] content, CancellationToken ct)
    {
        var dir = Directory.CreateDirectory(Path.Combine(Root, homestayId.ToString()));
        await File.WriteAllBytesAsync(Path.Combine(dir.FullName, fileName), content, ct);
    }

    public void Delete(long homestayId, string fileName)
    {
        var path = Path.Combine(Root, homestayId.ToString(), Path.GetFileName(fileName));
        if (File.Exists(path)) File.Delete(path);
    }

    public void DeleteHomestay(long homestayId)
    {
        var dir = Path.Combine(Root, homestayId.ToString());
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }
}
