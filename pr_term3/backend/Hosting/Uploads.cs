using backend.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace backend.Hosting;

public static class Uploads
{
    /// <summary>
    /// Phục vụ ảnh ở /uploads/{homestayId}/{guid}.jpg|png|webp – chỉ 3 loại ảnh (file khác trả 404),
    /// cache dài hạn vì tên file không bao giờ đổi nội dung, nosniff để trình duyệt không đoán kiểu khác.
    /// </summary>
    public static IApplicationBuilder UseUploads(this WebApplication app)
    {
        var storage = app.Services.GetRequiredService<UploadStorage>();
        var types = new FileExtensionContentTypeProvider();
        types.Mappings.Clear();
        types.Mappings[".jpg"] = "image/jpeg";
        types.Mappings[".png"] = "image/png";
        types.Mappings[".webp"] = "image/webp";

        return app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = UploadStorage.RequestPath,
            FileProvider = new PhysicalFileProvider(storage.Root),
            ContentTypeProvider = types,
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                ctx.Context.Response.Headers.XContentTypeOptions = "nosniff";
            },
        });
    }
}
