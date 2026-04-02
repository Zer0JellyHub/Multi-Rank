using Jellyfin.Plugin.MultiRank.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MultiRank.Services;

public sealed class DatabaseService : IDisposable
{
    private readonly string _connStr;
    private readonly ILogger<DatabaseService> _log;

    public DatabaseService(ILogger<DatabaseService> log, string dataPath)
    {
        _log     = log;
        _connStr = $"Data Source={Path.Combine(dataPath, "multirank.db")}";
        InitDb();
    }

    // ── Schema ──────────────────────────────────────────────────────────────

    private void InitDb()
    {
        using var con = Open();
        Exec(con, """
            CREATE TABLE IF NOT EXISTS Users (
                UserId               TEXT PRIMARY KEY,
                Username             TEXT NOT NULL,
                ActiveGenreId        TEXT NOT NULL DEFAULT 'isekai',
                TotalXp              INTEGER NOT NULL DEFAULT 0,
                SeasonXp             INTEGER NOT NULL DEFAULT 0,
                PrestigeCount        INTEGER NOT NULL DEFAULT 0,
                LastUpdated          TEXT NOT NULL,
                CurrentSeason        TEXT NOT NULL DEFAULT '',
                SeasonStartRankIndex INTEGER NOT NULL DEFAULT 0,
                SeasonStartXp        INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS SeasonHistory (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId        TEXT NOT NULL,
                SeasonId      TEXT NOT NULL,
                XpEarned      INTEGER NOT NULL DEFAULT 0,
                TopRankName   TEXT NOT NULL DEFAULT '',
                FinalPosition INTEGER NOT NULL DEFAULT 0,
                RecordedAt    TEXT NOT NULL,
                Result        TEXT NOT NULL DEFAULT 'Reset',
                RanksClimbed  INTEGER NOT NULL DEFAULT 0,
                UNIQUE(UserId, SeasonId)
            );

            CREATE TABLE IF NOT EXISTS ProcessedSessions (
                SessionKey  TEXT PRIMARY KEY,
                UserId      TEXT NOT NULL,
                ProcessedAt TEXT NOT NULL
            );

            -- Migration: add new columns if upgrading from older DB
            ALTER TABLE Users ADD COLUMN SeasonStartRankIndex INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE Users ADD COLUMN SeasonStartXp        INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE SeasonHistory ADD COLUMN Result       TEXT    NOT NULL DEFAULT 'Reset';
            ALTER TABLE SeasonHistory ADD COLUMN RanksClimbed INTEGER NOT NULL DEFAULT 0;

            CREATE INDEX IF NOT EXISTS idx_sh_user   ON SeasonHistory(UserId);
            CREATE INDEX IF NOT EXISTS idx_sh_season ON SeasonHistory(SeasonId);
        """);
    }

    // ── Users ────────────────────────────────────────────────────────────────

    public UserRankData? GetUser(string userId)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users WHERE UserId=@id";
        cmd.Parameters.AddWithValue("@id", userId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? MapUser(r) : null;
    }

    public List<UserRankData> GetAllUsers()
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users ORDER BY TotalXp DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<UserRankData>();
        while (r.Read()) list.Add(MapUser(r));
        return list;
    }

    public void UpsertUser(UserRankData u)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Users
                (UserId,Username,ActiveGenreId,TotalXp,SeasonXp,PrestigeCount,LastUpdated,CurrentSeason,SeasonStartRankIndex,SeasonStartXp)
            VALUES
                (@uid,@un,@genre,@txp,@sxp,@pres,@upd,@sea,@sri,@ssx)
            ON CONFLICT(UserId) DO UPDATE SET
                Username=excluded.Username,
                ActiveGenreId=excluded.ActiveGenreId,
                TotalXp=excluded.TotalXp,
                SeasonXp=excluded.SeasonXp,
                PrestigeCount=excluded.PrestigeCount,
                LastUpdated=excluded.LastUpdated,
                CurrentSeason=excluded.CurrentSeason,
                SeasonStartRankIndex=excluded.SeasonStartRankIndex,
                SeasonStartXp=excluded.SeasonStartXp
        """;
        cmd.Parameters.AddWithValue("@uid",  u.UserId);
        cmd.Parameters.AddWithValue("@un",   u.Username);
        cmd.Parameters.AddWithValue("@genre",u.ActiveGenreId);
        cmd.Parameters.AddWithValue("@txp",  u.TotalXp);
        cmd.Parameters.AddWithValue("@sxp",  u.CurrentSeasonXp);
        cmd.Parameters.AddWithValue("@pres", u.PrestigeCount);
        cmd.Parameters.AddWithValue("@upd",  u.LastUpdated.ToString("O"));
        cmd.Parameters.AddWithValue("@sea",  u.CurrentSeason);
        cmd.Parameters.AddWithValue("@sri",  u.SeasonStartRankIndex);
        cmd.Parameters.AddWithValue("@ssx",  u.SeasonStartXp);
        cmd.ExecuteNonQuery();
    }

    public void AddXp(string userId, long xp)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = """
            UPDATE Users
            SET TotalXp=TotalXp+@xp, SeasonXp=SeasonXp+@xp, LastUpdated=@now
            WHERE UserId=@id
        """;
        cmd.Parameters.AddWithValue("@xp",  xp);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id",  userId);
        cmd.ExecuteNonQuery();
    }

    public void ResetSeasonXp(string userId, string newSeasonId, int startRankIndex, long startXp)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = """
            UPDATE Users SET
                SeasonXp=0,
                CurrentSeason=@sea,
                SeasonStartRankIndex=@sri,
                SeasonStartXp=@ssx
            WHERE UserId=@id
        """;
        cmd.Parameters.AddWithValue("@sea", newSeasonId);
        cmd.Parameters.AddWithValue("@sri", startRankIndex);
        cmd.Parameters.AddWithValue("@ssx", startXp);
        cmd.Parameters.AddWithValue("@id",  userId);
        cmd.ExecuteNonQuery();
    }

    // ── Season History ───────────────────────────────────────────────────────

    public void SaveSeasonHistory(SeasonHistoryEntry e)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO SeasonHistory
                (UserId,SeasonId,XpEarned,TopRankName,FinalPosition,RecordedAt,Result,RanksClimbed)
            VALUES(@uid,@sid,@xp,@rank,@pos,@at,@res,@rc)
        """;
        cmd.Parameters.AddWithValue("@uid",  e.UserId);
        cmd.Parameters.AddWithValue("@sid",  e.SeasonId);
        cmd.Parameters.AddWithValue("@xp",   e.XpEarned);
        cmd.Parameters.AddWithValue("@rank", e.TopRankName);
        cmd.Parameters.AddWithValue("@pos",  e.FinalPosition);
        cmd.Parameters.AddWithValue("@at",   e.RecordedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@res",  e.Result.ToString());
        cmd.Parameters.AddWithValue("@rc",   e.RanksClimbed);
        cmd.ExecuteNonQuery();
    }

    public List<SeasonHistoryEntry> GetUserHistory(string userId)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT * FROM SeasonHistory WHERE UserId=@id ORDER BY RecordedAt DESC";
        cmd.Parameters.AddWithValue("@id", userId);
        using var r = cmd.ExecuteReader();
        var list = new List<SeasonHistoryEntry>();
        while (r.Read()) list.Add(MapHistory(r));
        return list;
    }

    public List<SeasonHistoryEntry> GetAllHistoryForYear(int year)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT * FROM SeasonHistory WHERE SeasonId LIKE @y ORDER BY XpEarned DESC";
        cmd.Parameters.AddWithValue("@y", $"{year}-%");
        using var r = cmd.ExecuteReader();
        var list = new List<SeasonHistoryEntry>();
        while (r.Read()) list.Add(MapHistory(r));
        return list;
    }

    // ── Session dedup ────────────────────────────────────────────────────────

    public bool IsProcessed(string key)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM ProcessedSessions WHERE SessionKey=@k";
        cmd.Parameters.AddWithValue("@k", key);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public void MarkProcessed(string key, string userId)
    {
        using var con = Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO ProcessedSessions(SessionKey,UserId,ProcessedAt) VALUES(@k,@u,@t)";
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@u", userId);
        cmd.Parameters.AddWithValue("@t", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private SqliteConnection Open()
    {
        var con = new SqliteConnection(_connStr);
        con.Open();
        return con;
    }

    private static void Exec(SqliteConnection con, string sql)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static UserRankData MapUser(SqliteDataReader r) => new()
    {
        UserId               = r.GetString(r.GetOrdinal("UserId")),
        Username             = r.GetString(r.GetOrdinal("Username")),
        ActiveGenreId        = r.GetString(r.GetOrdinal("ActiveGenreId")),
        TotalXp              = r.GetInt64 (r.GetOrdinal("TotalXp")),
        CurrentSeasonXp      = r.GetInt64 (r.GetOrdinal("SeasonXp")),
        PrestigeCount        = r.GetInt32 (r.GetOrdinal("PrestigeCount")),
        LastUpdated          = DateTime.Parse(r.GetString(r.GetOrdinal("LastUpdated"))),
        CurrentSeason        = r.GetString(r.GetOrdinal("CurrentSeason")),
        SeasonStartRankIndex = HasColumn(r,"SeasonStartRankIndex") ? r.GetInt32(r.GetOrdinal("SeasonStartRankIndex")) : 0,
        SeasonStartXp        = HasColumn(r,"SeasonStartXp")        ? r.GetInt64(r.GetOrdinal("SeasonStartXp"))        : 0,
    };

    private static SeasonHistoryEntry MapHistory(SqliteDataReader r) => new()
    {
        UserId        = r.GetString(r.GetOrdinal("UserId")),
        SeasonId      = r.GetString(r.GetOrdinal("SeasonId")),
        XpEarned      = r.GetInt64 (r.GetOrdinal("XpEarned")),
        TopRankName   = r.GetString(r.GetOrdinal("TopRankName")),
        FinalPosition = r.GetInt32 (r.GetOrdinal("FinalPosition")),
        RecordedAt    = DateTime.Parse(r.GetString(r.GetOrdinal("RecordedAt"))),
        Result        = HasColumn(r,"Result")
                            ? Enum.TryParse<SeasonResult>(r.GetString(r.GetOrdinal("Result")), out var res)
                                ? res : SeasonResult.Reset
                            : SeasonResult.Reset,
        RanksClimbed  = HasColumn(r,"RanksClimbed") ? r.GetInt32(r.GetOrdinal("RanksClimbed")) : 0,
    };

    /// <summary>Safe column check for DB migrations where new columns may not exist yet.</summary>
    private static bool HasColumn(SqliteDataReader r, string name)
    {
        for (int i = 0; i < r.FieldCount; i++)
            if (string.Equals(r.GetName(i), name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public void Dispose() { }
}
