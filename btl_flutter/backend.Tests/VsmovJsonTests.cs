using System.Text.Json;
using backend.Vsmov;

namespace backend.Tests;

public class VsmovJsonTests
{
    [Fact]
    public void ReadsNumbersStoredAsStrings()
    {
        var tmdb = JsonSerializer.Deserialize<VsmovTmdb>("""{"vote_average":"7.3","vote_count":"12"}""", VsmovJson.Options)!;

        Assert.Equal(7.3, tmdb.VoteAverage);
        Assert.Equal(12, tmdb.VoteCount);
    }

    [Fact]
    public void ReadsStringsStoredAsNumbers()
    {
        var movie = JsonSerializer.Deserialize<VsmovMovie>("""{"episode_total":10,"episode_current":"Tập 1"}""", VsmovJson.Options)!;

        Assert.Equal("10", movie.EpisodeTotal);
        Assert.Equal("Tập 1", movie.EpisodeCurrent);
    }

    [Fact]
    public void InvalidValuesBecomeNullInsteadOfThrowing()
    {
        var movie = JsonSerializer.Deserialize<VsmovMovie>(
            """{"year":"","name":{"vi":"x"},"actor":["A",null,""],"tmdb":{"vote_average":"N/A"},"_id":null}""",
            VsmovJson.Options)!;

        Assert.Null(movie.Year);
        Assert.Null(movie.Name);
        Assert.Null(movie.Tmdb!.VoteAverage);
        Assert.Null(movie.Id);
        Assert.Equal(["A", null, ""], movie.Actor!);
    }
}
