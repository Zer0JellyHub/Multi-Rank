using System.Text.Json;
using Jellyfin.Plugin.MultiRank.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MultiRank.Services;

public sealed class GenreService
{
    private readonly ILogger<GenreService> _log;
    private readonly string _genresFile;   // stores custom genres
    private readonly string _iconsDir;     // stores waifu icon images
    private List<GenreDefinition> _genres  = new();

    public GenreService(ILogger<GenreService> log, string dataPath)
    {
        _log       = log;
        _genresFile= Path.Combine(dataPath, "custom_genres.json");
        _iconsDir  = Path.Combine(dataPath, "waifu_icons");
        Directory.CreateDirectory(_iconsDir);
        Load();
    }

    // ── Public API ───────────────────────────────────────────────────────────

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

    // ── Custom genre CRUD ────────────────────────────────────────────────────

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

    // ── Waifu icon storage ───────────────────────────────────────────────────

    public string SaveWaifuIcon(string genreId, int rankIndex, byte[] data, string ext)
    {
        var path = IconPath(genreId, rankIndex, ext);
        File.WriteAllBytes(path, data);

        // Update the rank's icon URL so it's persisted
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

    // ── Built-in genres ──────────────────────────────────────────────────────

    private static List<GenreDefinition> BuiltIns() => new()
    {
        // ════════════════════════════════════════════════════════════════════
        //  ISEKAI  –  Bauer → Held  (15 Ränge)
        // ════════════════════════════════════════════════════════════════════
        new()
        {
            Id = "isekai", Name = "Isekai", Type = GenreType.Isekai, BuiltIn = true,
            BackgroundGradient = "linear-gradient(135deg,#0f0c29,#302b63,#24243e)",
            Ranks = new()
            {
                new(){ Name="Bauer",         XpRequired=0,          Icon="🌾", Color="#8B4513", Description="Ein einfacher Bauer – der Anfang jeder Isekai-Reise." },
                new(){ Name="Dorfbewohner",  XpRequired=10_000,     Icon="🏘️", Color="#A0522D", Description="Du kennst dein Dorf in- und auswendig." },
                new(){ Name="Lehrling",      XpRequired=30_000,     Icon="📜", Color="#CD853F", Description="Du lernst die Grundlagen der Magie und des Kampfes." },
                new(){ Name="Abenteurer F",  XpRequired=60_000,     Icon="⚔️", Color="#708090", Description="Offiziell registrierter Abenteurer – F-Rang." },
                new(){ Name="Abenteurer E",  XpRequired=120_000,    Icon="🗡️", Color="#778899", Description="Du hast deinen ersten echten Dungeon überlebt." },
                new(){ Name="Abenteurer D",  XpRequired=250_000,    Icon="🛡️", Color="#4682B4", Description="Respektiert in kleinen Städten." },
                new(){ Name="Krieger",       XpRequired=450_000,    Icon="⚡", Color="#4169E1", Description="Ein wahrer Krieger – gefürchtet von Gegnern." },
                new(){ Name="Ritter",        XpRequired=750_000,    Icon="🏇", Color="#6A0DAD", Description="Im Dienste eines Adeligen oder Königreichs." },
                new(){ Name="Edelmann",      XpRequired=1_200_000,  Icon="💜", Color="#8B008B", Description="Adelsblut – oder zumindest so angesehen." },
                new(){ Name="Baron",         XpRequired=2_000_000,  Icon="🔮", Color="#FF8C00", Description="Du herrschst über Land und Leute." },
                new(){ Name="Graf",          XpRequired=3_000_000,  Icon="👑", Color="#FFD700", Description="Ein mächtiger Adeliger mit großem Einfluss." },
                new(){ Name="Herzog",        XpRequired=4_000_000,  Icon="🌟", Color="#FF4500", Description="Nur noch der König steht über dir." },
                new(){ Name="König",         XpRequired=5_000_000,  Icon="🏰", Color="#DC143C", Description="Du regierst ein ganzes Königreich!" },
                new(){ Name="Legendär",      XpRequired=5_500_000,  Icon="🐉", Color="#00CED1", Description="Dein Name wird in der Geschichte verewigt." },
                new(){ Name="Held",          XpRequired=6_000_000,  Icon="✨", Color="#FFFFFF", Description="Der Auserwählte – Retter der Welt!" },
            }
        },

        // ════════════════════════════════════════════════════════════════════
        //  ABENTEURER-GILDE  –  Overlord-Style  (Kupfer → Diamant, 9 Ränge)
        // ════════════════════════════════════════════════════════════════════
        new()
        {
            Id = "abenteuer", Name = "Abenteurer-Gilde", Type = GenreType.Adventurer, BuiltIn = true,
            BackgroundGradient = "linear-gradient(135deg,#1c0a00,#3d1c02,#5a3000)",
            Ranks = new()
            {
                new(){ Name="Kupfer",     XpRequired=0,          Icon="🪙", Color="#b87333",
                       Description="Anfänger. Du hast deine kupferne Plakette gerade erst erhalten. Niemand in der Gilde kennt deinen Namen." },
                new(){ Name="Eisen",      XpRequired=40_000,     Icon="⚙️",  Color="#8a9ba8",
                       Description="Einsteiger. Du hast die ersten Quests überlebt und weißt, wie man ein Schwert hält." },
                new(){ Name="Silber",     XpRequired=120_000,    Icon="🥈", Color="#C0C0C0",
                       Description="Erfahrene Abenteurer. Die Rezeptionistin kennt dich beim Namen. Du bist kein Frischling mehr." },
                new(){ Name="Gold",       XpRequired=300_000,    Icon="🥇", Color="#FFD700",
                       Description="Hoher Rang – meist das absolute Limit für talentlose Menschen. Wie Climb, der persönliche Wächter von Renner." },
                new(){ Name="Platin",     XpRequired=700_000,    Icon="💠", Color="#00BFFF",
                       Description="Mittlere Elitestufe. Du kannst Gruppen schwacher Monster anführen und besiegen. Andere Abenteurer respektieren dich." },
                new(){ Name="Mithril",    XpRequired=1_500_000,  Icon="🔵", Color="#4fc3f7",
                       Description="Hohe Stufe. Du bist eine lokale Berühmtheit – Städte feiern deine Ankunft, Gasthäuser bieten dir das beste Zimmer." },
                new(){ Name="Orichalcum", XpRequired=3_000_000,  Icon="🟠", Color="#ff8c42",
                       Description="Sehr hohe Stufe. Du wirst selten gesehen – dein bloßer Name lässt Monster zittern und Könige aufhorchen." },
                new(){ Name="Adamantit",  XpRequired=5_000_000,  Icon="🔱", Color="#e8d5ff",
                       Description="Der höchste offizielle Rang der Gilde. Höchste Anerkennung. Du gehörst zu den stärksten Abenteurern der Welt – wie Momon." },
                new(){ Name="Diamant",    XpRequired=6_500_000,  Icon="💎", Color="#B9F2FF",
                       Description="Bonus-Rang jenseits aller offiziellen Klassifikation. Nur Wesen, die die Grenze zum Übernatürlichen überschreiten, erreichen ihn." },
            }
        },

        // ════════════════════════════════════════════════════════════════════
        //  FORTNITE RANKED  –  Bronze → Unreal  (8 Ränge, keine Unterteilungen)
        // ════════════════════════════════════════════════════════════════════
        new()
        {
            Id = "fortnite", Name = "Fortnite Ranked", Type = GenreType.Fortnite, BuiltIn = true,
            BackgroundGradient = "linear-gradient(135deg,#0d0d0d,#1a1a2e,#7209b7)",
            Ranks = new()
            {
                new(){ Name="Bronze",   XpRequired=0,          Icon="🟫", Color="#CD7F32", Description="Gerade angefangen. Bleib dran!" },
                new(){ Name="Silber",   XpRequired=50_000,     Icon="⬜", Color="#C0C0C0", Description="Du weißt was du tust." },
                new(){ Name="Gold",     XpRequired=150_000,    Icon="🟨", Color="#FFD700", Description="Solid. Konstant gut." },
                new(){ Name="Platin",   XpRequired=350_000,    Icon="🔷", Color="#00BFFF", Description="Über dem Durchschnitt." },
                new(){ Name="Diamant",  XpRequired=800_000,    Icon="💎", Color="#A8D8FF", Description="Top-Spieler. Beeindruckend." },
                new(){ Name="Elite",    XpRequired=1_800_000,  Icon="🟣", Color="#9B59B6", Description="Die Elite. Die Besten der Besten." },
                new(){ Name="Champion", XpRequired=3_500_000,  Icon="🏆", Color="#F39C12", Description="Champion. Fast unmöglich zu erreichen." },
                new(){ Name="Unreal",   XpRequired=6_000_000,  Icon="👾", Color="#E74C3C", Description="UNREAL. Das absolute Maximum." },
            }
        },

        // ════════════════════════════════════════════════════════════════════
        //  WAIFU TIER  –  6 Slots mit anpassbaren Bildern
        // ════════════════════════════════════════════════════════════════════
        new()
        {
            Id = "waifu", Name = "Waifu Tier", Type = GenreType.Waifu, BuiltIn = true,
            BackgroundGradient = "linear-gradient(135deg,#1a0533,#6b003e,#ff006e22)",
            Ranks = new()
            {
                new(){ Name="Waifu 1", XpRequired=0,          Icon="💔", Color="#FF69B4", IsImageIcon=true, Description="Deine erste Waifu – noch nicht vergeben." },
                new(){ Name="Waifu 2", XpRequired=100_000,    Icon="💗", Color="#FF1493", IsImageIcon=true, Description="Sie lächelt dir zu..." },
                new(){ Name="Waifu 3", XpRequired=300_000,    Icon="💖", Color="#FF007F", IsImageIcon=true, Description="Du hast ihr Herz gewonnen." },
                new(){ Name="Waifu 4", XpRequired=700_000,    Icon="💕", Color="#FF00AA", IsImageIcon=true, Description="Unzertrennlich." },
                new(){ Name="Waifu 5", XpRequired=1_500_000,  Icon="💞", Color="#E91E8C", IsImageIcon=true, Description="Deine Traumwaifu." },
                new(){ Name="Waifu 6", XpRequired=3_000_000,  Icon="💝", Color="#FF3399", IsImageIcon=true, Description="Die ultimative Waifu – forever yours." },
            }
        },

        // ════════════════════════════════════════════════════════════════════
        //  MARINE  –  One Piece Marineränge  (Matrose → Großadmiral, 14 Ränge)
        // ════════════════════════════════════════════════════════════════════
        new()
        {
            Id = "marine", Name = "Marine", Type = GenreType.Custom, BuiltIn = true,
            BackgroundGradient = "linear-gradient(135deg,#001f3f,#003580,#00509e)",
            Ranks = new()
            {
                // ── Mannschaft ──────────────────────────────────────────────
                new(){ Name="Matrose",              XpRequired=0,          Icon="⚓", Color="#7fbbe5",
                       Description="Seaman – einfacher Matrose der Marine. Du schrubbst noch das Deck." },
                new(){ Name="Maat",                 XpRequired=20_000,     Icon="🔱", Color="#6aace0",
                       Description="Petty Officer – du kennst deinen Platz an Bord und erfüllst Befehle zuverlässig." },
                new(){ Name="Obermaat",             XpRequired=60_000,     Icon="⭐", Color="#5599d4",
                       Description="Chief Petty Officer – erfahrener Unteroffizier, Respektsperson unter den Matrosen." },

                // ── Offiziere & Unteroffiziere ──────────────────────────────
                new(){ Name="Fähnrich (Shōi)",      XpRequired=130_000,    Icon="🎌", Color="#4a85c8",
                       Description="Leutnant zur See – dein erstes Offizierspatent. Die Grundausbildung liegt hinter dir." },
                new(){ Name="Oberleutnant (Chūi)",  XpRequired=250_000,    Icon="🔫", Color="#3e72bc",
                       Description="Du führst kleine Einheiten an und hast erste Kämpfe hinter dir." },
                new(){ Name="Kapitänleutnant (Taii)",XpRequired=420_000,   Icon="⚔️", Color="#3260a8",
                       Description="Stabsoffizier mit eigenem Kommando über Mannschaftsteile." },
                new(){ Name="Korvettenkapitän (Shōsa)",XpRequired=650_000, Icon="🛡️", Color="#2750a0",
                       Description="Du hast Kämpfe gegen echte Piraten bestanden. Haki-Kenntnisse beginnen sich zu zeigen." },
                new(){ Name="Fregattenkapitän (Chūsa)",XpRequired=950_000, Icon="📋", Color="#1d4090",
                       Description="Höherer Stabsoffizier – du verwaltest Missionen und führst größere Einheiten." },

                // ── Höhere Offiziere ────────────────────────────────────────
                new(){ Name="Kapitän zur See (Taisa)", XpRequired=1_400_000, Icon="🚢", Color="#153080",
                       Description="Käpt'n – du führst dein eigenes Schiff. Wie Tashigi oder Coby nach dem Timeskip." },
                new(){ Name="Kommodore (Junshō)",      XpRequired=2_000_000, Icon="🌊", Color="#0e2370",
                       Description="Flottillenadmiral – Kommandeur einer kleinen Flotte. Dein Name ist in der Neuen Welt bekannt." },
                new(){ Name="Konteradmiral (Shōshō)",  XpRequired=2_800_000, Icon="💫", Color="#0a1a60",
                       Description="Angehöriger der Admiralität. Du beherrschst Haki auf hohem Niveau." },

                // ── Spitze ──────────────────────────────────────────────────
                new(){ Name="Vizeadmiral (Chūjō)",     XpRequired=3_800_000, Icon="💪", Color="#e8c84a",
                       Description="Du führst ganze Flotten an und bist ein Anwärter auf den Admiralsposten. Wie Garp oder Smoker." },
                new(){ Name="Admiral (Taishō)",         XpRequired=5_200_000, Icon="🦁", Color="#f0a500",
                       Description="Einer der drei stärksten Kämpfer der Marine – „Höchste militärische Macht". Wie Kizaru, Aokiji oder Akainu." },
                new(){ Name="Großadmiral (Gensui)",     XpRequired=6_500_000, Icon="👑", Color="#FFD700",
                       Description="Oberbefehlshaber der gesamten Marine. Wie Sakazuki/Akainu oder früher Sengoku. Du kommandierst die ganze Welt-Regierungs-Marine." },
                new(){ Name="Flottenadmiral",            XpRequired=8_000_000, Icon="🌊⚓", Color="#FFFFFF",
                       Description="Das absolute Maximum – über dem Großadmiral. Du bist die Marine. Niemand in dieser Welt steht noch über dir. Eine Legende, die selbst die Yonkou respektieren." },
            }
        },
    };

    // ── Load / Persist ───────────────────────────────────────────────────────

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
