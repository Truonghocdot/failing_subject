namespace backend.Services;

/// <summary>
/// Lỗi nghiệp vụ có mã HTTP rõ ràng (400, 401, 404, 409...) – ApiExceptionHandler chuyển thành ProblemDetails.
/// Có <see cref="Errors"/> → trả kèm <c>errors: { field: [msg] }</c> để FE hiện lỗi ngay dưới ô nhập.
/// </summary>
public sealed class ApiProblemException(int statusCode, string title, string detail, IDictionary<string, string[]>? errors = null)
    : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public IDictionary<string, string[]>? Errors { get; } = errors;

    public static ApiProblemException Unauthorized(string detail) => new(StatusCodes.Status401Unauthorized, "Chưa xác thực", detail);

    public static ApiProblemException NotFound(string detail) => new(StatusCodes.Status404NotFound, "Không tìm thấy", detail);

    /// <summary>field != null → lỗi gắn vào ô nhập đó (VD trùng tên).</summary>
    public static ApiProblemException Conflict(string detail, string? field = null) =>
        new(StatusCodes.Status409Conflict, "Xung đột dữ liệu", detail, field is null ? null : new Dictionary<string, string[]> { [field] = [detail] });

    public static ApiProblemException Validation(IDictionary<string, string[]> errors) =>
        new(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ", errors.Values.First()[0], errors);
}
