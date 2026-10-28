namespace backend.Services;

/// <summary>Lỗi nghiệp vụ có mã HTTP rõ ràng (401, 409...) – ApiExceptionHandler chuyển thành ProblemDetails.</summary>
public sealed class ApiProblemException(int statusCode, string title, string detail) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;

    public static ApiProblemException Unauthorized(string detail) => new(StatusCodes.Status401Unauthorized, "Chưa xác thực", detail);

    public static ApiProblemException Conflict(string detail) => new(StatusCodes.Status409Conflict, "Xung đột dữ liệu", detail);
}
