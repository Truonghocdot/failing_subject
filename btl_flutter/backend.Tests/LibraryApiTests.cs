using System.Net;
using System.Net.Http.Json;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;

namespace backend.Tests;

public class LibraryApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Series = ApiFactory.SeriesSlug;
    private const string Single = ApiFactory.SingleSlug;

    private static object Episode(string slug, string name, string server = "Vietsub #1") =>
        new { serverName = server, episodeSlug = slug, episodeName = name };

    [Fact]
    public async Task Favorites_AddListStatusRemove()
    {
        var (client, _) = await factory.CreateUserAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/me/favorites/{Series}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/me/favorites/{Series}", null)).StatusCode); // idempotent
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/me/favorites/{Single}", null)).StatusCode);

        var list = await client.GetFromJsonAsync<PagedResult<MovieSummaryDto>>("/api/me/favorites");
        Assert.Equal(2, list!.TotalItems);
        Assert.Equal(Single, list.Items[0].Slug); // mới nhất trước
        Assert.Equal("Mùa Hè Đáng Nhớ", list.Items[1].Name);
        Assert.StartsWith("https://", list.Items[1].PosterUrl);

        Assert.True((await client.GetFromJsonAsync<FavoriteStatusDto>($"/api/me/favorites/{Series}"))!.IsFavorite);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/me/favorites/{Series}")).StatusCode);
        Assert.False((await client.GetFromJsonAsync<FavoriteStatusDto>($"/api/me/favorites/{Series}"))!.IsFavorite);
        Assert.Equal(1, (await client.GetFromJsonAsync<PagedResult<MovieSummaryDto>>("/api/me/favorites"))!.TotalItems);
    }

    [Fact]
    public async Task Favorites_UnknownMovie_Returns404_InvalidSlug400()
    {
        var (client, _) = await factory.CreateUserAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsync("/api/me/favorites/khong-ton-tai", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync("/api/me/favorites/Khong%20Hop%20Le", null)).StatusCode);
    }

    [Fact]
    public async Task Favorites_AreIsolatedPerUser_MovieStoredOnce()
    {
        var (alice, _) = await factory.CreateUserAsync();
        var (bob, _) = await factory.CreateUserAsync();

        await alice.PutAsync($"/api/me/favorites/{Series}", null);
        await bob.PutAsync($"/api/me/favorites/{Series}", null);
        await bob.DeleteAsync($"/api/me/favorites/{Series}");

        Assert.True((await alice.GetFromJsonAsync<FavoriteStatusDto>($"/api/me/favorites/{Series}"))!.IsFavorite);
        Assert.False((await bob.GetFromJsonAsync<FavoriteStatusDto>($"/api/me/favorites/{Series}"))!.IsFavorite);
        Assert.Equal(1, factory.WithDb(db => db.Movies.Count(m => m.Slug == Series)));
    }

    [Fact]
    public async Task History_UpsertKeepsOneEntryPerMovie_NewestFirst()
    {
        var (client, _) = await factory.CreateUserAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/me/history/{Series}")).StatusCode);

        await client.PutAsJsonAsync($"/api/me/history/{Series}", Episode("tap-1", "1"));
        await client.PutAsJsonAsync($"/api/me/history/{Single}", Episode("tap-full", "Full"));
        await client.PutAsJsonAsync($"/api/me/history/{Series}", Episode("tap-3", "3", "Thuyết minh #1"));

        var history = await client.GetFromJsonAsync<PagedResult<WatchHistoryDto>>("/api/me/history");
        Assert.Equal(2, history!.TotalItems);
        Assert.Equal(Series, history.Items[0].Movie.Slug); // vừa xem lại → lên đầu
        Assert.Equal(("tap-3", "3", "Thuyết minh #1"), (history.Items[0].EpisodeSlug, history.Items[0].EpisodeName, history.Items[0].ServerName));

        var entry = await client.GetFromJsonAsync<WatchHistoryDto>($"/api/me/history/{Series}");
        Assert.Equal("tap-3", entry!.EpisodeSlug);
        Assert.Equal(DateTimeKind.Utc, entry.WatchedAt.Kind);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/me/history/{Single}")).StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<PagedResult<WatchHistoryDto>>("/api/me/history"))!.TotalItems);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/me/history")).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<PagedResult<WatchHistoryDto>>("/api/me/history"))!.TotalItems);
    }

    [Fact]
    public async Task History_InvalidBody_Returns400()
    {
        var (client, _) = await factory.CreateUserAsync();

        var response = await client.PutAsJsonAsync($"/api/me/history/{Series}", new { serverName = "", episodeSlug = "tap-1", positionSeconds = -5 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task History_IsTrimmedToMaxEntries()
    {
        var (client, auth) = await factory.CreateUserAsync();
        var userId = auth.User.Id;
        var start = DateTime.UtcNow.AddDays(-1);

        // Gieo sẵn đúng MaxHistoryPerUser bản ghi (phim giả đã đồng bộ → không gọi nguồn)
        factory.WithDb(db =>
        {
            for (var i = 0; i < LibraryService.MaxHistoryPerUser; i++)
            {
                var movie = new Movie
                {
                    Slug = $"seed-{userId:N}-{i}", Name = $"Seed {i}", PosterUrl = "", ThumbUrl = "", SyncedAt = DateTime.UtcNow,
                };
                db.Movies.Add(movie);
                db.WatchHistory.Add(new WatchHistory
                {
                    UserId = userId, Movie = movie, ServerName = "S", EpisodeSlug = "tap-1", EpisodeName = "1", WatchedAt = start.AddMinutes(i),
                });
            }
            return db.SaveChanges();
        });

        await client.PutAsJsonAsync($"/api/me/history/{Series}", Episode("tap-1", "1"));

        var history = await client.GetFromJsonAsync<PagedResult<WatchHistoryDto>>("/api/me/history");
        Assert.Equal(LibraryService.MaxHistoryPerUser, history!.TotalItems);
        Assert.Equal(Series, history.Items[0].Movie.Slug);
        Assert.False(factory.WithDb(db => db.WatchHistory.Any(h => h.UserId == userId && h.Movie.Slug == $"seed-{userId:N}-0")));
    }
}
