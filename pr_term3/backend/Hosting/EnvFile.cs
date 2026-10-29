namespace backend.Hosting;

/// <summary>
/// Tìm và nạp <c>.env</c>: đi ngược lên từ thư mục chứa backend.exe rồi từ thư mục hiện tại.
/// Windows Service chạy với thư mục hiện tại là C:\Windows\System32 → phải tìm theo thư mục của exe
/// (bản publish: đặt .env cạnh backend.exe). Biến môi trường thật luôn thắng giá trị trong file.
/// </summary>
public static class EnvFile
{
    public const string FileName = ".env";

    public static string? Load()
    {
        var path = Find(AppContext.BaseDirectory) ?? Find(Directory.GetCurrentDirectory());
        if (path is not null) DotNetEnv.Env.NoClobber().Load(path);
        return path;
    }

    internal static string? Find(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, FileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
