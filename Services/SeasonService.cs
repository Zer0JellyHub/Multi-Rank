using Jellyfin.Plugin.MultiRank.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MultiRank.Services;

public sealed class SeasonService
{
    private readonly ILogger<SeasonService> _log;

    public SeasonService(ILogger<SeasonService> log) => _log = log;

    // ── Current season ───────────────────────────────────────────────────────

    public SeasonInfo GetCurrentSeason(DateTime? at = null)
    {
        var now  = at ?? DateTime.UtcNow;
        var type = TypeFromMonth(now.Month);
        int year = now.Year;

        var (start, end) = Bounds(year, type);

        return new SeasonInfo
        {
            Id              = $"{year}-{type}",
            Name            = $"{LocalName(type)} {year}",
            Type            = type,
            Year            = year,
            StartDate       = start,
            EndDate         = end,
            // Show year-end banner in the last month of Winter (= March)
            IsYearEndSeason = type == SeasonType.Winter && now.Month == 3,
        };
    }

    public string GetCurrentSeasonId() => GetCurrentSeason().Id;

    public bool IsNewSeason(string storedId) => storedId != GetCurrentSeasonId();

    public int DaysRemaining()
    {
        var s = GetCurrentSeason();
        return Math.Max(0, (s.EndDate.Date - DateTime.UtcNow.Date).Days);
    }

    // ── Year-end helper ──────────────────────────────────────────────────────

    /// <summary>All season IDs that belong to <paramref name="year"/>.</summary>
    public IEnumerable<string> SeasonIdsForYear(int year) =>
        Enum.GetValues<SeasonType>().Select(t => $"{year}-{t}");

    // ── Private helpers ──────────────────────────────────────────────────────

    private static SeasonType TypeFromMonth(int m) => m switch
    {
        1 or 2 or 3   => SeasonType.Winter,
        4 or 5 or 6   => SeasonType.Spring,
        7 or 8 or 9   => SeasonType.Summer,
        _              => SeasonType.Autumn,
    };

    private static (DateTime start, DateTime end) Bounds(int year, SeasonType t) => t switch
    {
        SeasonType.Winter => (new(year, 1,  1), new(year, 3,  31, 23, 59, 59)),
        SeasonType.Spring => (new(year, 4,  1), new(year, 6,  30, 23, 59, 59)),
        SeasonType.Summer => (new(year, 7,  1), new(year, 9,  30, 23, 59, 59)),
        _                  => (new(year, 10, 1), new(year, 12, 31, 23, 59, 59)),
    };

    private static string LocalName(SeasonType t) => t switch
    {
        SeasonType.Winter => "Winter-Season",
        SeasonType.Spring => "Frühlings-Season",
        SeasonType.Summer => "Sommer-Season",
        _                  => "Herbst-Season",
    };
}
