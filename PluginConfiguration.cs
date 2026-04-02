using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MultiRank;

public class PluginConfiguration : BasePluginConfiguration
{
    public int XpPerMinute                      { get; set; } = 2;
    public int XpPerEpisode                     { get; set; } = 20;
    public int XpPerMovie                       { get; set; } = 50;

    // Anti-cheat thresholds
    public int EpisodeCompletionThresholdPercent{ get; set; } = 80;
    public int MovieCompletionThresholdPercent  { get; set; } = 80;
    public int EpisodeMinWatchSeconds           { get; set; } = 900;
    public int MovieMinWatchSeconds             { get; set; } = 2700;

    // Watch-session bonus
    public bool EnableSessionBonus              { get; set; } = true;
    public int  SessionBonusThresholdMinutes    { get; set; } = 90;
    public int  SessionBonusPercent             { get; set; } = 25;

    // Features
    public bool EnableSeasons                   { get; set; } = true;
    public bool ShowYearEndSummary              { get; set; } = true;
    public bool EnablePrestige                  { get; set; } = true;
    public bool ShowLeaderboard                 { get; set; } = true;

    // ── Season carry-over rules ──────────────────────────────────────────────
    /// <summary>Minimum rank steps climbed within a season to carry rank into the next season.</summary>
    public int  SeasonCarryOverMinRankClimb     { get; set; } = 2;
    /// <summary>How many ranks a player is demoted if they did not climb enough.</summary>
    public int  SeasonDemotionSteps             { get; set; } = 2;

    // Defaults
    public string DefaultGenreId               { get; set; } = "isekai";

    // Optional: explicit path to playback_reporting.db (auto-detected when empty)
    public string PlaybackReportingDbPath      { get; set; } = string.Empty;
}
