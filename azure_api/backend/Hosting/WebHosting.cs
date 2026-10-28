using Microsoft.Net.Http.Headers;

namespace backend.Hosting;

/// <summary>Phục vụ giao diện (fe/dist được publish vào wwwroot) và gắn security header.</summary>
public static class WebHosting
{
    // style 'unsafe-inline': React / Recharts dùng thuộc tính style; script chỉ từ chính origin (Vite build không có script inline)
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; " +
        "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        // OnStarting: UseExceptionHandler xoá header khi có lỗi → gắn ngay trước khi gửi để trang lỗi cũng có
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers[HeaderNames.XContentTypeOptions] = "nosniff";
            headers[HeaderNames.XFrameOptions] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers[HeaderNames.ContentSecurityPolicy] = ContentSecurityPolicy;
            return Task.CompletedTask;
        });
        return next();
    });

    /// <summary>
    /// File tĩnh: <c>assets/*</c> có hash trong tên → cache 1 năm; index.html luôn hỏi lại để nhận bản mới sau khi cập nhật.
    /// Mọi đường dẫn không phải file và không thuộc /api → index.html (React Router xử lý, F5 ở /reports vẫn chạy).
    /// </summary>
    public static WebApplication MapSpa(this WebApplication app)
    {
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx => ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                ctx.Context.Request.Path.StartsWithSegments("/assets") ? "public, max-age=31536000, immutable" : "no-cache",
        });

        // /api/... không khớp route nào → 404 JSON chứ không trả index.html
        app.MapFallback("/api/{**path}", () => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound,
            title: "Không tìm thấy", detail: "API không tồn tại"));
        app.MapFallbackToFile("index.html", new StaticFileOptions
        {
            OnPrepareResponse = ctx => ctx.Context.Response.Headers[HeaderNames.CacheControl] = "no-cache",
        });
        return app;
    }
}
