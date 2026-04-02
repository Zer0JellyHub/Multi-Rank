namespace Jellyfin.Plugin.MultiRank.Models;

// ── Genre type tag ──────────────────────────────────────────────────────────
public enum GenreType { Isekai, Adventurer, Fortnite, Waifu, Custom }

// ── One rank inside a genre ─────────────────────────────────────────────────
public class RankDefinition
{
    public string Name        { get; set; } = string.Empty;
    public long   XpRequired  { get; set; }
    public string Icon        { get; set; } = string.Empty;
    public string Color       { get; set; } = "#ffffff";
    public string Description { get; set; } = string.Empty;
    public bool   IsImageIcon { get; set; } = false;
}

// ── Genre with its rank list ────────────────────────────────────────────────
public class GenreDefinition
{
    public string            Id                 { get; set; } = Guid.NewGuid().ToString();
    public string            Name               { get; set; } = string.Empty;
    public GenreType         Type               { get; set; } = GenreType.Custom;
    public List<RankDefinition> Ranks           { get; set; } = new();
    public bool              BuiltIn            { get; set; } = false;
    public string            BackgroundGradient { get; set; } = "linear-gradient(135deg,#1a1a2e,#16213e)";
}

// ── Per-user persistent data ────────────────────────────────────────────────
public class UserRankData
{
    public string   UserId              { get; set; } = string.Empty;
    public string   Username            { get; set; } = string.Empty;
    public string   ActiveGenreId       { get; set; } = "isekai";
    public long     TotalXp             { get; set; }
    public long     CurrentSeasonXp     { get; set; }
    public int      PrestigeCount       { get; set; }
    public DateTime LastUpdated         { get; set; } = DateTime.UtcNow;
    public string   CurrentSeason       { get; set; } = string.Empty;

    // Tracks where the player stood at the START of this season (for carry-over check)
    public int      SeasonStartRankIndex{ get; set; } = 0;
    public long     SeasonStartXp       { get; set; } = 0;
}

// ── Season types & info ─────────────────────────────────────────────────────
public enum SeasonType { Winter, Spring, Summer, Autumn }

public class SeasonInfo
{
    public string     Id             { get; set; } = string.Empty;
    public string     Name           { get; set; } = string.Empty;
    public SeasonType Type           { get; set; }
    public int        Year           { get; set; }
    public DateTime   StartDate      { get; set; }
    public DateTime   EndDate        { get; set; }
    public bool       IsYearEndSeason{ get; set; } = false;
}

// ── Leaderboard entry ───────────────────────────────────────────────────────
public class LeaderboardEntry
{
    public string          UserId       { get; set; } = string.Empty;
    public string          Username     { get; set; } = string.Empty;
    public long            TotalXp      { get; set; }
    public long            SeasonXp     { get; set; }
    public string          GenreId      { get; set; } = string.Empty;
    public string          GenreName    { get; set; } = string.Empty;
    public RankDefinition? CurrentRank  { get; set; }
    public int             PrestigeCount{ get; set; }
    public int             Position     { get; set; }
}

// ── Season carry-over result ────────────────────────────────────────────────
public enum SeasonResult { CarriedOver, Demoted, Reset }

// ── Season history row ──────────────────────────────────────────────────────
public class SeasonHistoryEntry
{
    public string       UserId        { get; set; } = string.Empty;
    public string       SeasonId      { get; set; } = string.Empty;
    public long         XpEarned      { get; set; }
    public string       TopRankName   { get; set; } = string.Empty;
    public int          FinalPosition { get; set; }
    public DateTime     RecordedAt    { get; set; } = DateTime.UtcNow;
    public SeasonResult Result        { get; set; } = SeasonResult.Reset;
    public int          RanksClimbed  { get; set; } = 0;
}

// ── API request DTOs ────────────────────────────────────────────────────────
public class SetGenreRequest  { public string GenreId  { get; set; } = string.Empty; }
