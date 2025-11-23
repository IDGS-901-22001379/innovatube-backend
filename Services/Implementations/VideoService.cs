using System.Data;
using System.Text;
using InnovaTube.Api.DTOs.Videos;
using InnovaTube.Api.Infrastructure;
using InnovaTube.Api.Services.Interfaces;
using MySqlConnector;

namespace InnovaTube.Api.Services.Implementations;

public class VideoService : IVideoService
{
    private readonly MySqlConnectionFactory _factory;

    public VideoService(MySqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<FavoriteDto> AddFavoriteAsync(
        int userId,
        AddFavoriteRequest request,
        string ip,
        string userAgent)
    {
        using var conn = (MySqlConnection)_factory.CreateConnection();
        await conn.OpenAsync();

        FavoriteDto favorite;

        // ============================
        // 1) sp_add_favorite
        // ============================
        using (var cmd = new MySqlCommand("sp_add_favorite", conn))
        {
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("p_user_id", userId);
            cmd.Parameters.AddWithValue("p_video_id", request.VideoId);
            cmd.Parameters.AddWithValue("p_title", request.Title);
            cmd.Parameters.AddWithValue("p_description", (object?)request.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_channel_title", (object?)request.ChannelTitle ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_channel_id", (object?)request.ChannelId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_thumbnail_url", (object?)request.ThumbnailUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_published_at", (object?)request.PublishedAt ?? DBNull.Value);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                throw new Exception("No se pudo agregar el video a favoritos.");

            favorite = new FavoriteDto
            {
                FavoriteId = reader.GetInt64("favorite_id"),
                CreatedAt = reader.GetDateTime("created_at"),
                VideoId = reader.GetString("video_id"),
                Title = reader.GetString("title"),
                ThumbnailUrl = reader.IsDBNull(reader.GetOrdinal("thumbnail_url"))
                    ? null
                    : reader.GetString("thumbnail_url")
            };
        }

        // ============================
        // 2) sp_log_action
        // ============================
        using (var logCmd = new MySqlCommand("sp_log_action", conn))
        {
            logCmd.CommandType = CommandType.StoredProcedure;
            logCmd.Parameters.AddWithValue("p_user_id", userId);
            logCmd.Parameters.AddWithValue("p_action", "ADD_FAVORITE");
            logCmd.Parameters.AddWithValue("p_entity_type", "VIDEO");
            logCmd.Parameters.AddWithValue("p_entity_id", request.VideoId);
            logCmd.Parameters.AddWithValue("p_description", $"Agregó video {request.Title} a favoritos");
            logCmd.Parameters.AddWithValue("p_ip_address", ip);
            logCmd.Parameters.AddWithValue("p_user_agent", userAgent);

            await logCmd.ExecuteNonQueryAsync();
        }

        return favorite;
    }

    public async Task RemoveFavoriteAsync(
        int userId,
        string videoId,
        string ip,
        string userAgent)
    {
        using var conn = (MySqlConnection)_factory.CreateConnection();
        await conn.OpenAsync();

        // 1) sp_remove_favorite
        using (var cmd = new MySqlCommand("sp_remove_favorite", conn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("p_user_id", userId);
            cmd.Parameters.AddWithValue("p_video_id", videoId);

            await cmd.ExecuteNonQueryAsync();
        }

        // 2) sp_log_action
        using (var logCmd = new MySqlCommand("sp_log_action", conn))
        {
            logCmd.CommandType = CommandType.StoredProcedure;
            logCmd.Parameters.AddWithValue("p_user_id", userId);
            logCmd.Parameters.AddWithValue("p_action", "REMOVE_FAVORITE");
            logCmd.Parameters.AddWithValue("p_entity_type", "VIDEO");
            logCmd.Parameters.AddWithValue("p_entity_id", videoId);
            logCmd.Parameters.AddWithValue("p_description", $"Quitó el video {videoId} de favoritos");
            logCmd.Parameters.AddWithValue("p_ip_address", ip);
            logCmd.Parameters.AddWithValue("p_user_agent", userAgent);

            await logCmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<IReadOnlyList<FavoriteDto>> ListFavoritesAsync(
        int userId,
        string? search)
    {
        using var conn = (MySqlConnection)_factory.CreateConnection();
        await conn.OpenAsync();

        var result = new List<FavoriteDto>();

        using var cmd = new MySqlCommand("sp_list_favorites", conn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("p_user_id", userId);
        cmd.Parameters.AddWithValue("p_search", (object?)search ?? DBNull.Value);

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var dto = new FavoriteDto
            {
                FavoriteId = reader.GetInt64("favorite_id"),
                CreatedAt = reader.GetDateTime("created_at"),
                VideoId = reader.GetString("video_id"),
                Title = reader.GetString("title"),
                ThumbnailUrl = reader.IsDBNull(reader.GetOrdinal("thumbnail_url"))
                    ? null
                    : reader.GetString("thumbnail_url"),
                ChannelTitle = reader.IsDBNull(reader.GetOrdinal("channel_title"))
                    ? null
                    : reader.GetString("channel_title"),
                PublishedAt = reader.IsDBNull(reader.GetOrdinal("published_at"))
                    ? null
                    : reader.GetDateTime("published_at")
            };

            result.Add(dto);
        }

        return result;
    }
}
