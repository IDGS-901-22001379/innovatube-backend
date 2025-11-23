namespace InnovaTube.Api.DTOs.Videos;

public class FavoriteDto
{
    public long FavoriteId { get; set; }
    public DateTime CreatedAt { get; set; }

    public string VideoId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? ThumbnailUrl { get; set; }
    public string? ChannelTitle { get; set; }
    public DateTime? PublishedAt { get; set; }
}
