using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

internal static class CacheControlExtensions
{
    /// <summary>Thêm header Cache-Control cho response thành công để client/proxy tận dụng.</summary>
    public static TBuilder WithCacheControl<TBuilder>(this TBuilder builder, int maxAgeSeconds)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var result = await next(context);
            var inner = result is INestedHttpResult nested ? nested.Result : result;
            if (inner is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 })
                context.HttpContext.Response.Headers.CacheControl = $"public, max-age={maxAgeSeconds}";
            return result;
        });
}
