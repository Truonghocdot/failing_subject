using backend.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace backend.Endpoints;

/// <summary>
/// Chuyển exception đã biết thành ProblemDetails thay vì 500:
/// lỗi bind tham số / JSON hỏng (VD page=abc) → 400, lỗi nghiệp vụ → mã của ApiProblemException (kèm errors nếu có).
/// </summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        ProblemDetails? problem = exception switch
        {
            BadHttpRequestException bad => new ProblemDetails { Status = bad.StatusCode, Title = "Tham số không hợp lệ", Detail = bad.Message },
            ApiProblemException { Errors: { } errors } p => new HttpValidationProblemDetails(errors) { Status = p.StatusCode, Title = p.Title, Detail = p.Message },
            ApiProblemException p => new ProblemDetails { Status = p.StatusCode, Title = p.Title, Detail = p.Message },
            _ => null,
        };
        if (problem is null) return false;

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }
}
