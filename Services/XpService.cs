using Jellyfin.Plugin.MultiRank.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MultiRank.Services;

public sealed class XpService
{
    private readonly ILogger<XpService>  _log;
    private readonly DatabaseService     _db;
    private readonly SeasonService       _seasons;
    private readonly GenreService        _genres;
    private readonly PluginConfiguration _cfg;

    public XpService(
        ILogger<XpService>  log,
        DatabaseService     db,
        SeasonService       seasons,
        GenreService        genres,
        PluginConfiguration cfg)
    {
        _log = log; _db = db; _seasons = seasons; _genres = genres; _cfg = cfg;
    }

    // ── Main entry point ─────────────────────────────────────────────────────

    /// <summary>
    /// Reads new sessions from the Playback Reporting DB and awards XP.
    /// Call this on page load (or via a scheduled task).
    /// </summary>
    public async Task SyncAsync(string userId, string username, string pbDbPath)
    {
        if (!File.Exists(pbDbPath))
        {
            _log.LogWarning("Playback Reporting DB not found at {Path}", pbDbPath);
            return;
        }

        // Ensure user row exists
        var user = _db.GetUser(userId) ?? CreateUser(userId, username);

        // Season rollover check
        if (_seasons.IsNewSeason(user.CurrentSeason))
        {
            await RolloverSeasonAsync(user);
            user = _db.GetUser(userId)!;
        }

        long gained = await ReadNewXpAsync(userId, pbDbPath);
        if (gained > 0)
        {
            _db.AddXp(userId, gained);
            _log.LogInformation("{User} earned {Xp} XP from new sessions", username, gained);
        }
    }

    // ── XP reading ───────────────────────────────────────────────────────────

    private async Task<long> ReadNewXpAsync(string userId, string pbDb)
    {
        long total = 0;

        await Task.Run(() =>
        {
            using var con = new SqliteConnection($"Data Source={pbDb};Mode=ReadOnly");
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = """
                SELECT rowid, ItemType, PlayDuration, ItemDuration, DateCreated
                FROM PlaybackActivity
                WHERE UserId = @uid
                ORDER BY DateCreated ASC
            """;
            cmd.Parameters.AddWithValue("@uid", userId);
            using var r = cmd.ExecuteReader();

            // Accumulate per-date watch minutes for session bonus
            var dateMinutes = new Dictionary<string, long>();

            while (r.Read())
            {
                var key      = $"{userId}_{r.GetInt64(0)}";
                if (_db.IsProcessed(key)) continue;

                var itemType = r.IsDBNull(1) ? ""  : r.GetString(1);
                var played   = r.IsDBNull(2) ? 0L  : r.GetInt64(2);
                var duration = r.IsDBNull(3) ? 0L  : r.GetInt64(3);
                var dateStr  = r.IsDBNull(4) ? "?" : r.GetString(4)[..10];

                // Anti-cheat: played time cannot exceed item length
                if (duration > 0) played = Math.Min(played, duration);

                long xp = CalcXp(played, duration, itemType);
                if (xp <= 0) continue;

                total += xp;
                _db.MarkProcessed(key, userId);

                dateMinutes.TryGetValue(dateStr, out long prev);
                dateMinutes[dateStr] = prev + played / 60;
            }

            // Session bonus – awarded per day where threshold is met
            if (_cfg.EnableSessionBonus)
            {
                foreach (var (_, mins) in dateMinutes)
                {
                    if (mins >= _cfg.SessionBonusThresholdMinutes)
                    {
                        // Calculate base XP for that day's sessions and add bonus on top
                        long bonus = (long)(total * (_cfg.SessionBonusPercent / 100.0));
                        total += bonus;
                        _log.LogInformation("Session bonus applied: +{B} XP ({Pct}%)", bonus, _cfg.SessionBonusPercent);
                    }
                }
            }
        });

        return total;
    }

    private long CalcXp(long playedSec, long durationSec, string itemType)
    {
        long xp = (playedSec / 60) * _cfg.XpPerMinute;

        double pct = durationSec > 0 ? (double)playedSec / durationSec * 100 : 0;
        bool isEpisode = string.Equals(itemType, "Episode", StringComparison.OrdinalIgnoreCase);
        bool isMovie   = string.Equals(itemType, "Movie",   StringComparison.OrdinalIgnoreCase);

        if (isEpisode && pct >= _cfg.EpisodeCompletionThresholdPercent
                      && playedSec >= _cfg.EpisodeMinWatchSeconds)
            xp += _cfg.XpPerEpisode;

        if (isMovie && pct >= _cfg.MovieCompletionThresholdPercent
                    && playedSec >= _cfg.MovieMinWatchSeconds)
            xp += _cfg.XpPerMovie;

        return xp;
    }

    // ── Season rollover ──────────────────────────────────────────────────────

    private async Task RolloverSeasonAsync(UserRankData user)
    {
        var newSeasonId = _seasons.GetCurrentSeasonId();
        _log.LogInformation("Season rollover for {U}: {Old} → {New}", user.Username, user.CurrentSeason, newSeasonId);

        // ── 1. Determine rank indices ──────────────────────────────────────
        var genre  = _genres.GetGenre(user.ActiveGenreId);
        var ranks  = genre?.Ranks.OrderBy(r => r.XpRequired).ToList() ?? new();

        int startIndex = user.SeasonStartRankIndex;
        int endIndex   = RankIndex(ranks, user.TotalXp);
        int climbed    = endIndex - startIndex;

        // ── 2. Apply carry-over rule ──────────────────────────────────────
        SeasonResult result;
        long newTotalXp = user.TotalXp;

        if (climbed >= _cfg.SeasonCarryOverMinRankClimb)
        {
            // ✅ Carried over – keep XP / rank
            result = SeasonResult.CarriedOver;
            _log.LogInformation("{U} climbed {N} ranks → rank CARRIED OVER", user.Username, climbed);
        }
        else
        {
            // ❌ Demoted – drop N rank steps
            result = SeasonResult.Demoted;
            int demotedIndex = Math.Max(0, endIndex - _cfg.SeasonDemotionSteps);
            newTotalXp = ranks.Count > 0 ? ranks[demotedIndex].XpRequired : 0;
            _log.LogInformation(
                "{U} only climbed {N} ranks (need {Min}) → DEMOTED {Steps} ranks to '{Rank}' ({Xp} XP)",
                user.Username, climbed, _cfg.SeasonCarryOverMinRankClimb,
                _cfg.SeasonDemotionSteps, ranks[demotedIndex].Name, newTotalXp);
        }

        // ── 3. Save season snapshot ───────────────────────────────────────
        var allUsers  = _db.GetAllUsers().OrderByDescending(u => u.CurrentSeasonXp).ToList();
        int position  = allUsers.FindIndex(u => u.UserId == user.UserId) + 1;
        var topRank   = ranks.Count > 0 ? ranks[endIndex].Name : "?";

        _db.SaveSeasonHistory(new SeasonHistoryEntry
        {
            UserId        = user.UserId,
            SeasonId      = user.CurrentSeason,
            XpEarned      = user.CurrentSeasonXp,
            TopRankName   = topRank,
            FinalPosition = position,
            RecordedAt    = DateTime.UtcNow,
            Result        = result,
            RanksClimbed  = climbed,
        });

        // ── 4. Apply XP change and reset season ───────────────────────────
        // Write new TotalXp if demoted
        if (result == SeasonResult.Demoted)
        {
            user.TotalXp = newTotalXp;
            _db.UpsertUser(user);
        }

        // Reset season counters; new start rank = wherever they land after demotion
        int newStartIndex = RankIndex(ranks, newTotalXp);
        _db.ResetSeasonXp(user.UserId, newSeasonId, newStartIndex, newTotalXp);

        await Task.CompletedTask;
    }

    // ── Helper: find rank index for a given XP ─────────────────────────────
    private static int RankIndex(List<RankDefinition> ranks, long xp)
    {
        if (ranks.Count == 0) return 0;
        for (int i = ranks.Count - 1; i >= 0; i--)
            if (xp >= ranks[i].XpRequired) return i;
        return 0;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private UserRankData CreateUser(string userId, string username)
    {
        var u = new UserRankData
        {
            UserId               = userId,
            Username             = username,
            ActiveGenreId        = _cfg.DefaultGenreId,
            CurrentSeason        = _seasons.GetCurrentSeasonId(),
            LastUpdated          = DateTime.UtcNow,
            SeasonStartRankIndex = 0,
            SeasonStartXp        = 0,
        };
        _db.UpsertUser(u);
        return u;
    }
}
