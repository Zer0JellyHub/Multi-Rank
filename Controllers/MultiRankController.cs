using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

// Jellyfin / MediaBrowser Namespaces
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Users;
using MediaBrowser.Model.Library; 

using Jellyfin.Plugin.MultiRank.Models;
using Jellyfin.Plugin.MultiRank.Services;

namespace Jellyfin.Plugin.MultiRank.Controllers;

[ApiController]
[Route("MultiRank")]
[Authorize]
public sealed class MultiRankController : ControllerBase
{
    private readonly ILogger<MultiRankController> _log;
    private readonly IUserManager _users;

    private DatabaseService    Db      => Plugin.Instance!.DbService!;
    private GenreService       Genres  => Plugin.Instance!.GenreService!;
    private SeasonService       Seasons => Plugin.Instance!.SeasonService!;
    private XpService           Xp      => Plugin.Instance!.XpService!;
    private PluginConfiguration Cfg     => Plugin.Instance!.Configuration;

    public MultiRankController(ILogger<MultiRankController> log, IUserManager users)
    {
        _log   = log;
        _users = users;
    }

    [HttpGet("Me")]
    public async Task<IActionResult> GetMe()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var jfUser   = _users.GetUserById(Guid.Parse(userId));
        var username = jfUser?.Username ?? "Unknown";

        await Xp.SyncAsync(userId, username, PlaybackDbPath());

        var u = Db.GetUser(userId);
        if (u is null) return NotFound("No rank profile found.");

        var genre    = Genres.GetGenre(u.ActiveGenreId);
        var curRank  = Genres.GetRankForXp(u.ActiveGenreId, u.TotalXp);
        var nextRank = Genres.GetNextRank(u.ActiveGenreId, u.TotalXp);
        var progress = Genres.GetProgressPercent(u.ActiveGenreId, u.TotalXp);
        var season   = Seasons.GetCurrentSeason();

        return Ok(new
        {
            userId,
            username,
            totalXp         = u.TotalXp,
            seasonXp        = u.CurrentSeasonXp,
            prestige        = u.PrestigeCount,
            activeGenreId   = u.ActiveGenreId,
            genreName       = genre?.Name ?? u.ActiveGenreId,
            currentRank     = curRank,
            nextRank,
            progressPercent = progress,
            season = new
            {
                id            = season.Id,
                name          = season.Name,
                type          = season.Type.ToString(),
                daysRemaining = Seasons.DaysRemaining(),
                isYearEnd     = season.IsYearEndSeason,
            },
        });
    }

    [HttpGet("Leaderboard")]
    public IActionResult GetLeaderboard([FromQuery] bool season = false, [FromQuery] string? genreId = null)
    {
        var all     = Db.GetAllUsers();
        var sorted  = season ? all.OrderByDescending(u => u.CurrentSeasonXp) : all.OrderByDescending(u => u.TotalXp);

        int pos     = 1;
        var entries = new List<LeaderboardEntry>();

        foreach (var u in sorted)
        {
            if (genreId is not null && !string.Equals(u.ActiveGenreId, genreId, StringComparison.OrdinalIgnoreCase))
                continue;

            long xp     = season ? u.CurrentSeasonXp : u.TotalXp;
            var  genre  = Genres.GetGenre(u.ActiveGenreId);
            var  rank   = Genres.GetRankForXp(u.ActiveGenreId, xp);

            entries.Add(new LeaderboardEntry
            {
                UserId        = u.UserId,
                Username      = u.Username,
                TotalXp       = u.TotalXp,
                SeasonXp      = u.CurrentSeasonXp,
                GenreId       = u.ActiveGenreId,
                GenreName     = genre?.Name ?? u.ActiveGenreId,
                CurrentRank   = rank,
                PrestigeCount = u.PrestigeCount,
                Position      = pos++,
            });
        }
        return Ok(entries);
    }

    [HttpGet("Genres")]
    public IActionResult GetGenres() => Ok(Genres.GetAllGenres());

    [HttpGet("Genre/{id}")]
    public IActionResult GetGenre(string id)
    {
        var g = Genres.GetGenre(id);
        return g is null ? NotFound() : Ok(g);
    }

    [HttpPost("Genre/SetActive")]
    public IActionResult SetActiveGenre([FromBody] SetGenreRequest req)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        if (Genres.GetGenre(req.GenreId) is null) return NotFound("Genre not found.");

        var u = Db.GetUser(userId);
        if (u is null) return NotFound("User not found.");

        u.ActiveGenreId = req.GenreId;
        Db.UpsertUser(u);
        return Ok(new { genreId = req.GenreId });
    }

    [HttpPost("Genre/Custom")]
    public IActionResult SaveCustomGenre([FromBody] GenreDefinition genre)
    {
        if (!IsAdmin()) return Forbid();
        if (genre.BuiltIn) return BadRequest("Cannot overwrite built-in.");
        return Ok(Genres.SaveCustomGenre(genre));
    }

    [HttpDelete("Genre/Custom/{id}")]
    public IActionResult DeleteCustomGenre(string id)
    {
        if (!IsAdmin()) return Forbid();
        return Genres.DeleteCustomGenre(id) ? Ok(new { message = "Deleted." }) : BadRequest("Not found.");
    }

    [HttpPost("Waifu/UploadIcon/{genreId}/{rankIndex:int}")]
    public async Task<IActionResult> UploadWaifuIcon(string genreId, int rankIndex, IFormFile file)
    {
        if (!IsAdmin()) return Forbid();
        if (file is null || file.Length == 0) return BadRequest("No file.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" }.Contains(ext)) return BadRequest("Format error.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var url = Genres.SaveWaifuIcon(genreId, rankIndex, ms.ToArray(), ext);
        return Ok(new { url });
    }

    [HttpGet("WaifuIcon/{genreId}/{rankIndex:int}")]
    [AllowAnonymous]
    public IActionResult GetWaifuIcon(string genreId, int rankIndex)
    {
        var data = Genres.GetWaifuIcon(genreId, rankIndex);
        if (data == null) return NotFound();

        // base.File verhindert Konflikte mit System.IO.File
        return base.File(data, "image/png");
    }

    [HttpPost("Prestige")]
    public IActionResult Prestige()
    {
        if (!Cfg.EnablePrestige) return BadRequest("Disabled.");
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var u = Db.GetUser(userId);
        if (u is null) return NotFound();

        var genre = Genres.GetGenre(u.ActiveGenreId);
        var topRank = genre?.Ranks.MaxBy(r => r.XpRequired);
        if (topRank == null || u.TotalXp < topRank.XpRequired) return BadRequest("Not enough XP.");

        u.TotalXp = 0;
        u.PrestigeCount++;
        Db.UpsertUser(u);
        return Ok(new { message = "✨ Prestige!", prestige = u.PrestigeCount });
    }

    [HttpGet("Season")]
    public IActionResult GetSeason()
    {
        var s = Seasons.GetCurrentSeason();
        return Ok(new { id = s.Id, name = s.Name, daysRemaining = Seasons.DaysRemaining() });
    }

    private string? CurrentUserId() => HttpContext.User.FindFirst("Emby.UserId")?.Value;

    private bool IsAdmin()
    {
        var id = CurrentUserId();
        if (id is null) return false;
        var u = _users.GetUserById(Guid.Parse(id));
        
        // SICHERE METHODE: Prüfe direkt die Administrator-Eigenschaft des Users
        return u != null && u.HasPermission(MediaBrowser.Model.Entities.PermissionKind.IsAdministrator);
    }

    private string PlaybackDbPath()
    {
        if (!string.IsNullOrEmpty(Cfg.PlaybackReportingDbPath)) return Cfg.PlaybackReportingDbPath;
        var paths = new[] { 
            "/config/data", 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "jellyfin", "data"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "jellyfin", "data")
        };
        foreach (var p in paths) {
            var c = Path.Combine(p, "playback_reporting.db");
            if (System.IO.File.Exists(c)) return c;
        }
        return string.Empty;
    }
}
