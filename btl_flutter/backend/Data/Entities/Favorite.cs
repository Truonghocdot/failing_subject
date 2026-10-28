namespace backend.Data.Entities;

public class Favorite
{
    public Guid UserId { get; set; }
    public long MovieId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Movie Movie { get; set; } = null!;
}
