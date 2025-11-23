using System.Security.Claims;
using InnovaTube.Api.DTOs.Videos;
using InnovaTube.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InnovaTube.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // todos requieren estar autenticados
public class VideosController : ControllerBase
{
    private readonly IVideoService _videoService;

    public VideosController(IVideoService videoService)
    {
        _videoService = videoService;
    }

    private int GetUserIdFromToken()
    {
        var subClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");

        if (subClaim == null || !int.TryParse(subClaim.Value, out var userId))
            throw new Exception("No se pudo obtener el usuario del token.");

        return userId;
    }

    [HttpGet("favorites")]
    public async Task<IActionResult> GetFavorites([FromQuery] string? search)
    {
        var userId = GetUserIdFromToken();
        var favorites = await _videoService.ListFavoritesAsync(userId, search);
        return Ok(favorites);
    }

    [HttpPost("favorites")]
    public async Task<IActionResult> AddFavorite([FromBody] AddFavoriteRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserIdFromToken();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();

        var favorite = await _videoService.AddFavoriteAsync(userId, request, ip, userAgent);
        return Ok(favorite);
    }

    [HttpDelete("favorites/{videoId}")]
    public async Task<IActionResult> RemoveFavorite([FromRoute] string videoId)
    {
        var userId = GetUserIdFromToken();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();

        await _videoService.RemoveFavoriteAsync(userId, videoId, ip, userAgent);
        return NoContent();
    }
}
