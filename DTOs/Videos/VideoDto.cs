namespace InnovaTube.Api.DTOs.Videos;

public class VideoDto
{
    public string VideoId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? ChannelTitle { get; set; }
    public string? ChannelId { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime? PublishedAt { get; set; }
}
