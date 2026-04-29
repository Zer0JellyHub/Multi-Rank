using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MultiRank.Models;
using Jellyfin.Plugin.MultiRank.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MultiRank.Controllers;

[ApiController]
[Route("MultiRank")]
[Authorize]
public sealed class MultiRankController : ControllerBase
{
    private readonly ILogger<MultiRankController> _log;
    private readonly IUserManager _users;

    private DatabaseService     Db      => Plugin.Instance!.DbService!;
    private GenreService        Genres  => Plugin.Instance!.GenreService!;
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

        var dbPath = PlaybackDbPath();
        _log.LogInformation("MultiRank: PlaybackDB path = {Path}", dbPath);

        await Xp.SyncAsync(userId, username, dbPath);

        var u = Db.GetUser(userId);

        // ── NEU: User wird automatisch angelegt wenn noch nicht vorhanden ──
        if (u is null)
        {
            u = new UserRankData
            {
                UserId          = userId,
                Username        = username,
                TotalXp         = 0,
                CurrentSeasonXp = 0,
                PrestigeCount   = 0,
                ActiveGenreId   = "isekai",
            };
            Db.UpsertUser(u);
        }

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
    public IActionResult GetLeaderboard(
        [FromQuery] bool    season  = false,
        [FromQuery] string? genreId = null)
    {
        var all    = Db.GetAllUsers();
        var sorted = season
            ? all.OrderByDescending(u => u.CurrentSeasonXp)
            : all.OrderByDescending(u => u.TotalXp);

        int pos     = 1;
        var entries = new List<LeaderboardEntry>();

        foreach (var u in sorted)
        {
            if (genreId is not null &&
                !string.Equals(u.ActiveGenreId, genreId, StringComparison.OrdinalIgnoreCase))
                continue;

            long xp    = season ? u.CurrentSeasonXp : u.TotalXp;
            var  genre = Genres.GetGenre(u.ActiveGenreId);
            var  rank  = Genres.GetRankForXp(u.ActiveGenreId, xp);

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

        if (Genres.GetGenre(req.GenreId) is null)
            return NotFound("Genre not found.");

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
        return Genres.DeleteCustomGenre(id)
            ? Ok(new { message = "Deleted." })
            : BadRequest("Not found.");
    }

    [HttpPost("Waifu/UploadIcon/{genreId}/{rankIndex:int}")]
    public async Task<IActionResult> UploadWaifuIcon(
        string genreId, int rankIndex, IFormFile file)
    {
        if (!IsAdmin()) return Forbid();
        if (file is null || file.Length == 0) return BadRequest("No file.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" }.Contains(ext))
            return BadRequest("Format not supported.");

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
        return data is null ? NotFound() : File(data, "image/png");
    }

    [HttpPost("Prestige")]
    public IActionResult Prestige()
    {
        if (!Cfg.EnablePrestige) return BadRequest("Prestige disabled.");

        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var u = Db.GetUser(userId);
        if (u is null) return NotFound();

        var genre   = Genres.GetGenre(u.ActiveGenreId);
        var topRank = genre?.Ranks.MaxBy(r => r.XpRequired);
        if (topRank is null || u.TotalXp < topRank.XpRequired)
            return BadRequest($"Reach '{topRank?.Name}' first!");

        u.TotalXp = 0;
        u.PrestigeCount++;
        Db.UpsertUser(u);
        return Ok(new { message = "✨ Prestige!", prestige = u.PrestigeCount });
    }

    [HttpGet("Season")]
    public IActionResult GetSeason()
    {
        var s = Seasons.GetCurrentSeason();
        return Ok(new
        {
            id            = s.Id,
            name          = s.Name,
            type          = s.Type.ToString(),
            daysRemaining = Seasons.DaysRemaining(),
            isYearEnd     = s.IsYearEndSeason,
        });
    }

    [HttpGet("Season/YearEnd/{year:int}")]
    public IActionResult GetYearEnd(int year)
    {
        var history  = Db.GetAllHistoryForYear(year);
        var allUsers = Db.GetAllUsers().ToDictionary(u => u.UserId);

        var leaderboard = history
            .GroupBy(h => h.UserId)
            .Select(g =>
            {
                allUsers.TryGetValue(g.Key, out var u);
                var genre = Genres.GetGenre(u?.ActiveGenreId ?? "isekai");
                return new
                {
                    userId    = g.Key,
                    username  = u?.Username ?? g.Key,
                    yearXp    = g.Sum(h => h.XpEarned),
                    genreName = genre?.Name ?? "?",
                    prestige  = u?.PrestigeCount ?? 0,
                    seasons   = g.Select(h => new { h.SeasonId, h.XpEarned }),
                };
            })
            .OrderByDescending(x => x.yearXp)
            .Select((x, i) => new
            {
                position = i + 1,
                x.userId, x.username, x.yearXp,
                x.genreName, x.prestige, x.seasons,
            })
            .ToList();

        return Ok(new
        {
            year,
            title       = $"🏆 Year {year} — Final Standings",
            leaderboard,
        });
    }

    [HttpGet("History")]
    public IActionResult GetMyHistory()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        return Ok(Db.GetUserHistory(userId));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string? CurrentUserId()
        => HttpContext.User.FindFirst("Emby.UserId")?.Value
        ?? HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private bool IsAdmin()
    {
        var id = CurrentUserId();
        if (id is null) return false;
        var u = _users.GetUserById(Guid.Parse(id));
        return u?.HasPermission(PermissionKind.IsAdministrator) ?? false;
    }

    private string PlaybackDbPath()
    {
        // 1. Manuell gesetzter Pfad in den Plugin-Settings
        if (!string.IsNullOrEmpty(Cfg.PlaybackReportingDbPath) &&
            System.IO.File.Exists(Cfg.PlaybackReportingDbPath))
            return Cfg.PlaybackReportingDbPath;

        // 2. Alle möglichen Pfade durchsuchen – Synology zuerst!
        var candidates = new[]
        {
            // ── Synology NAS (Paketcenter) ──
            "/volume1/@appdata/Jellyfin/data/playback_reporting.db",
            "/volume2/@appdata/Jellyfin/data/playback_reporting.db",
            "/volume3/@appdata/Jellyfin/data/playback_reporting.db",

            // ── Docker ──
            "/config/data/playback_reporting.db",
            "/data/playback_reporting.db",

            // ── Linux ──
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "jellyfin", "data", "playback_reporting.db"),

            // ── Windows ──
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "jellyfin", "data", "playback_reporting.db"),
        };

        foreach (var path in candidates)
        {
            if (System.IO.File.Exists(path))
            {
                _log.LogInformation("MultiRank: Found PlaybackDB at {Path}", path);
                return path;
            }
        }

        _log.LogWarning("MultiRank: playback_reporting.db nicht gefunden! " +
                        "Bitte Pfad in den Plugin-Settings manuell eintragen.");
        return string.Empty;
    }
}
