using System.ComponentModel.DataAnnotations;

namespace backend.Vsmov;

public sealed class VsmovOptions
{
    public const string Section = "Vsmov";

    [Required, Url]
    public string BaseUrl { get; init; } = "https://vsmov.com/api/";

    [Range(1, 60)]
    public int TimeoutSeconds { get; init; } = 10;
}
