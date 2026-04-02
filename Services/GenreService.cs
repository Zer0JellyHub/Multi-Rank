using System.Text.Json;
using Jellyfin.Plugin.MultiRank.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MultiRank.Services;

public sealed class GenreService
{
    private readonly ILogger<GenreService> _log;
    private readonly string _genresFile;
    private readonly string _iconsDir;
    private List<GenreDefinition> _genres  = new();

    public GenreService(ILogger<GenreService> log, string dataPath)
    {
        _log       = log;
        _genresFile= Path.Combine(dataPath, "custom_genres.json");
        _iconsDir  = Path.Combine(dataPath, "waifu_icons");
        Directory.CreateDirectory(_iconsDir);
        Load();
    }

    public List<GenreDefinition> GetAllGenres()    => _genres;
    public GenreDefinition?      GetGenre(string id)
        => _genres.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));

    public RankDefinition GetRankForXp(string genreId, long xp)
    {
        var genre = GetGenre(genreId);
        if (genre is null || genre.Ranks.Count == 0)
            return new RankDefinition { Name = "Unranked", Icon = "❓" };
        return genre.Ranks
            .OrderByDescending(r => r.XpRequired)
            .FirstOrDefault(r => xp >= r.XpRequired)
            ?? genre.Ranks.MinBy(r => r.XpRequired)!;
    }

    public RankDefinition? GetNextRank(string genreId, long xp)
    {
        var genre = GetGenre(genreId);
        return genre?.Ranks.OrderBy(r => r.XpRequired).FirstOrDefault(r => r.XpRequired > xp);
    }

    public double GetProgressPercent(string genreId, long xp)
    {
        var cur  = GetRankForXp(genreId, xp);
        var next = GetNextRank(genreId, xp);
        if (next is null) return 100.0;
        long range = next.XpRequired - cur.XpRequired;
        if (range == 0) return 100.0;
        return Math.Clamp((xp - cur.XpRequired) * 100.0 / range, 0, 100);
    }

    public GenreDefinition SaveCustomGenre(GenreDefinition g)
    {
        g.Type    = GenreType.Custom;
        g.BuiltIn = false;
        if (string.IsNullOrEmpty(g.Id)) g.Id = Guid.NewGuid().ToString();

        int idx = _genres.FindIndex(x => x.Id == g.Id);
        if (idx >= 0) _genres[idx] = g;
        else          _genres.Add(g);

        PersistCustom();
        return g;
    }

    public bool DeleteCustomGenre(string id)
    {
        var g = GetGenre(id);
        if (g is null || g.BuiltIn) return false;
        _genres.RemoveAll(x => x.Id == id);
        PersistCustom();
        return true;
    }

    public string SaveWaifuIcon(string genreId, int rankIndex, byte[] data, string ext)
    {
        var path = IconPath(genreId, rankIndex, ext);
        File.WriteAllBytes(path, data);

        var genre = GetGenre(genreId);
        if (genre != null && rankIndex < genre.Ranks.Count)
        {
            genre.Ranks[rankIndex].Icon        = $"/MultiRank/WaifuIcon/{genreId}/{rankIndex}";
            genre.Ranks[rankIndex].IsImageIcon = true;
            if (!genre.BuiltIn) PersistCustom();
        }
        return $"/MultiRank/WaifuIcon/{genreId}/{rankIndex}";
    }

    public byte[]? GetWaifuIcon(string genreId, int rankIndex)
    {
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" })
        {
            var p = IconPath(genreId, rankIndex, ext);
            if (File.Exists(p)) return File.ReadAllBytes(p);
        }
        return null;
    }

    private static List<GenreDefinition> BuiltIns() => new()
    {
        new()
        {
            Id = "isekai", Name = "Isekai", Type = GenreType.Isekai, BuiltIn = true,
            BackgroundGradient = "linear-gradient(135deg,#0f0c29,#302b63,#24243e)",
            Ranks = new()
            {
                new(){ Name="Bauer",        XpRequired=0,         Icon="🌾", Color="#8B4513", Description="Ein einfacher Bauer." },
                new(){ Name="Dorfbewohner", XpRequired=10000,     Icon="🏘️", Color="#A0522D", Description="Du kennst dein Dorf in- und auswendig." },
                new(){ Name="Lehrling",     XpRequired=30000,     Icon="📜", Color="#CD853F", Description="Du lernst die Grundlagen." },
                new(){ Name="Abenteurer F", XpRequired=60000,     Icon="⚔️", Color="#708090", Description="Offiziell registrierter Abenteurer F-Rang." },
                new(){ Name="Abenteurer E", XpRequired=120000,    Icon="🗡️", Color="#778899", Description="Du hast deinen ersten Dungeon überlebt." },
                new(){ Name="Abenteurer D", XpRequired=250000,    Icon="🛡️", Color="#4682B4", Description="Respektiert in kleinen Städten." },
                new(){ Name="Krieger",      XpRequired=450000,    Icon="⚡", Color="#4169E1", Description="Ein wahrer Krieger." },
                new(){ Name="Ritter",       XpRequired=750000,    Icon="🏇", Color="#6A0DAD", Description="Im Dienste eines Königreichs." },
                new(){ Name="Edelmann",     XpRequired=1200000,   Icon="💜", Color="#8B008B", Description="Adelsblut." },
                new(){ Name="Baron",        XpRequired=2000000,   Icon="🔮", Color="#FF8C00", Description="Du herrschst über Land und Leute." },
                new(){ Name="Graf",         XpRequired=3000000,   Icon="👑", Color="#FFD700", Description="Ein mächtiger Adeliger." },
                new(){ Name="Herzog",       XpRequired=4000000,   Icon="🌟", Color="#FF4500", Description="Nur noch der König steht über dir." },
                new(){ Name="König",        XpRequired=5000000,   Icon="🏰", Color="#DC143C", Description="Du regierst ein ganzes Königreich!" },
                new(){ Name="Legendär",     XpRequired=5500000,   Icon="🐉", Color="#00CED1", Description="Dein Name wird in der Geschichte verewigt." },
                new(){ Name="Held",         XpRequired=6000000,   Icon="✨", Color="#FFFFFF", Description="Der Auserwählte - Retter der Welt!" },
            }
        },

        new()
        {
            Id = "abenteuer", Name = "Abenteurer-Gilde", Type = GenreType.Adventurer, BuiltIn = true,
            BackgroundGradient = "linear-gradient(135deg,#1c0a00,#3d1c02,#5a3000)",
            Ranks = new()
            {
                new(){ Name="Kupfer",     XpRequired=0,       Icon="🪙", Color="#b87333", Description="Anfänger. Niemand kennt deinen Namen." },
                new(){ Name="Eisen",      XpRequired=40000,   Icon="⚙️",  Color="#8a9ba8", Description="Einsteiger. Du hast die ersten Quests überlebt." },
                new(){ Name="Silber",     XpRequired=120000,  Icon="🥈", Color="#C0C0C0", Description="Erfahren. Die Rezeptionistin kennt dich." },
                new(){ Name="Gold",       XpRequired=300000,  Icon="🥇", Color="#FFD700", Description="Hoher Rang. Das Limit für talentlose Menschen." },
                new(){ Name="Platin",     XpRequired=700000,  Icon="💠", Color="#00BFFF", Description="Elitestufe. Andere Abenteurer respektieren dich." },
                new(){ Name="Mithril",    XpRequired=1500000, Icon="🔵", Color="#4fc3f7", Description="Hohe Stufe. Du bist eine lokale Berühmtheit." },
                new(){ Name="Orichalcum", XpRequired=3000000, Icon="🟠", Color="#ff8c42", Description="Sehr hohe Stufe. Dein Name lässt Monster zittern." },
                new(){ Name="Adamantit",  XpRequired=5000000, Icon="🔱", Color="#e8d5ff", Description="Der höchste offizielle Rang der Gilde. Wie Momon." },
                new(){ Name="Diamant",    XpRequired=6500000, Icon="💎", Color="#B9F2FF", Description="Bonus-Rang jenseits aller Klassifikation." },
            }
        },

        new()
        {
            Id = "fortnite", Name = "Fortnite Ranked", Type = GenreType.Fortnite, BuiltIn = true,
            BackgroundGradient = "linear-gradient(135deg,#0d0d0d,#1a1a2e,#7209b7)",
            Ranks = new()
            {
                new(){ Name="Bronze",   XpRequired=0,       Icon="🟫", Color="#CD7F32", Description="Gerade angefangen." },
                new(){ Name="Silber",   XpRequired=50000,   Icon="⬜", Color="#C0C0C0", Description="Du weißt was du tust." },
                new(){ Name="Gold",     XpRequired=150000,  Icon="🟨", Color="#FFD700", Description="Solid. Konstant gut." },
                new(){ Name="Platin",   XpRequired=350000,  Icon="🔷", Color="#00BFFF", Description="Über dem Durchschnitt." },
                new(){ Name="Diamant",  XpRequired=800000,  Icon="💎", Color="#A8D8FF", Description="Top-Spieler." },
                new(){ Name="Elite",    XpRequired=1800000, Icon="🟣", Color="#9B59B6", Description="Die Elite." },
                new(){ Name="Champion", XpRequired=3500000, Icon="🏆", Color="#F39C12", Description="Champion." },
                new(){ Name="Unreal",   XpRequired=6000000, Icon="👾", Color="#E74C3C", Description="UNREAL. Das absolute Maximum." },
            }
        },

        new()
        {
            Id = "waifu", Name = "Waifu Tier", Type = GenreType.Waifu, BuiltIn = true,
            BackgroundGradient = "linear-gradient(135deg,#1a0533,#6b003e,#ff006e22)",
            Ranks = new()
            {
                new(){ Name="Waifu 1", XpRequired=0,       Icon="💔", Color="#FF69B4", IsImageIcon=true, Description="Deine erste Waifu." },
                new(){ Name="Waifu 2", XpRequired=100000,  Icon="💗", Color="#FF1493", IsImageIcon=true, Description="Sie lächelt dir zu..." },
                new(){ Name="Waifu 3", XpRequired=300000,  Icon="💖", Color="#FF007F", IsImageIcon=true, Description="Du hast ihr Herz gewonnen." },
                new(){ Name="Waifu 4", XpRequired=700000,  Icon="💕", Color="#FF00AA", IsImageIcon=true, Description="Unzertrennlich." },
                new(){ Name="Waifu 5", XpRequired=1500000, Icon="💞", Color="#E91E8C", IsImageIcon=true, Description="Deine Traumwaifu." },
                new(){ Name="Waifu 6", XpRequired=3000000, Icon="💝", Color="#FF3399", IsImageIcon=true, Description="Die ultimative Waifu." },
            }
        },

        new()
        {
            Id = "marine", Name = "Marine", Type = GenreType.Custom, BuiltIn = true,
            BackgroundGradient = "linear-gradient(135deg,#001f3f,#003580,#00509e)",
            Ranks = new()
            {
                new(){ Name="Matrose",          XpRequired=0,       Icon="⚓", Color="#7fbbe5", Description="Einfacher Matrose. Du schrubbst noch das Deck." },
                new(){ Name="Maat",             XpRequired=20000,   Icon="🔱", Color="#6aace0", Description="Du erfüllst Befehle zuverlässig." },
                new(){ Name="Obermaat",         XpRequired=60000,   Icon="⭐", Color="#5599d4", Description="Erfahrener Unteroffizier." },
                new(){ Name="Fähnrich",         XpRequired=130000,  Icon="🎌", Color="#4a85c8", Description="Dein erstes Offizierspatent." },
                new(){ Name="Oberleutnant",     XpRequired=250000,  Icon="🔫", Color="#3e72bc", Description="Du führst kleine Einheiten an." },
                new(){ Name="Kapitänleutnant",  XpRequired=420000,  Icon="⚔️", Color="#3260a8", Description="Stabsoffizier mit eigenem Kommando." },
                new(){ Name="Korvettenkapitän", XpRequired=650000,  Icon="🛡️", Color="#2750a0", Description="Du hast Kämpfe gegen echte Piraten bestanden." },
                new(){ Name="Fregattenkapitän", XpRequired=950000,  Icon="📋", Color="#1d4090", Description="Du verwaltest Missionen und führst Einheiten." },
                new(){ Name="Kapitän",          XpRequired=1400000, Icon="🚢", Color="#153080", Description="Du führst dein eigenes Schiff." },
                new(){ Name="Kommodore",        XpRequired=2000000, Icon="🌊", Color="#0e2370", Description="Kommandeur einer kleinen Flotte." },
                new(){ Name="Konteradmiral",    XpRequired=2800000, Icon="💫", Color="#0a1a60", Description="Angehöriger der Admiralität." },
                new(){ Name="Vizeadmiral",      XpRequired=3800000, Icon="💪", Color="#e8c84a", Description="Du führst ganze Flotten an." },
                new(){ Name="Admiral",          XpRequired=5200000, Icon="🦁", Color="#f0a500", Description="Einer der drei stärksten Kämpfer der Marine." },
                new(){ Name="Großadmiral",      XpRequired=6500000, Icon="👑", Color="#FFD700", Description="Oberbefehlshaber der gesamten Marine." },
                new(){ Name="Flottenadmiral",   XpRequired=8000000, Icon="🌊", Color="#FFFFFF", Description="Das absolute Maximum. Eine Legende." },
            }
        },
    };

    private void Load()
    {
        var builtIn = BuiltIns();

        if (!File.Exists(_genresFile))
        {
            _genres = builtIn;
            return;
        }

        try
        {
            var custom = JsonSerializer.Deserialize<List<GenreDefinition>>(
                File.ReadAllText(_genresFile)) ?? new();
            _genres = builtIn.Concat(custom.Where(g => !g.BuiltIn)).ToList();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not load custom genres – using built-ins only");
            _genres = builtIn;
        }
    }

    private void PersistCustom()
    {
        var custom = _genres.Where(g => !g.BuiltIn).ToList();
        File.WriteAllText(_genresFile,
            JsonSerializer.Serialize(custom, new JsonSerializerOptions { WriteIndented = true }));
    }

    private string IconPath(string genreId, int rankIndex, string ext)
        => Path.Combine(_iconsDir, $"{genreId}_rank{rankIndex}{ext}");
}
