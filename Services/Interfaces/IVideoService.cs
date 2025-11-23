using InnovaTube.Api.DTOs.Videos;

namespace InnovaTube.Api.Services.Interfaces;

public interface IVideoService
{
    Task<FavoriteDto> AddFavoriteAsync(
        int userId,
        AddFavoriteRequest request,
        string ip,
        string userAgent);

    Task RemoveFavoriteAsync(
        int userId,
        string videoId,
        string ip,
        string userAgent);

    Task<IReadOnlyList<FavoriteDto>> ListFavoritesAsync(
        int userId,
        string? search);
}
