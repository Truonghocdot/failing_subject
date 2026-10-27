using backend.Vsmov;
using Microsoft.AspNetCore.Diagnostics;

namespace backend.Endpoints;

/// <summary>
/// Chuyển exception đã biết thành ProblemDetails thay vì 500:
/// lỗi bind tham số (VD page=abc) → 400, nguồn vsmov không có dữ liệu → 404, nguồn lỗi → 502.
/// </summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            BadHttpRequestException bad => (bad.StatusCode, "Tham số không hợp lệ", bad.Message),
            VsmovNotFoundException => (StatusCodes.Status404NotFound, "Không tìm thấy", exception.Message),
            VsmovUnavailableException => (StatusCodes.Status502BadGateway, "Lỗi nguồn dữ liệu", exception.Message),
            _ => (0, null, null),
        };
        if (status == 0) return false;

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title, Detail = detail },
        });
    }
}
