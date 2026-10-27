namespace backend.Vsmov;

public abstract class VsmovException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Nguồn trả 404 hoặc không có dữ liệu cho slug → HTTP 404.</summary>
public sealed class VsmovNotFoundException(string message = "Không tìm thấy dữ liệu")
    : VsmovException(message);

/// <summary>Nguồn lỗi 5xx, timeout, JSON hỏng → HTTP 502.</summary>
public sealed class VsmovUnavailableException(string message = "Nguồn phim tạm thời không phản hồi", Exception? inner = null)
    : VsmovException(message, inner);
