using Jellyfin.Plugin.MultiRank.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MultiRank;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    // Singleton access used by the controller
    public static Plugin? Instance { get; private set; }

    public DatabaseService? DbService     { get; private set; }
    public GenreService?    GenreService  { get; private set; }
    public SeasonService?   SeasonService { get; private set; }
    public XpService?       XpService     { get; private set; }

    public Plugin(
        IApplicationPaths    applicationPaths,
        IXmlSerializer       xmlSerializer,
        ILoggerFactory       loggerFactory)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;

        var dataPath = Path.Combine(applicationPaths.DataPath, "MultiRank");
        Directory.CreateDirectory(dataPath);

        DbService     = new DatabaseService (loggerFactory.CreateLogger<DatabaseService>(),  dataPath);
        GenreService  = new GenreService    (loggerFactory.CreateLogger<GenreService>(),     dataPath);
        SeasonService = new SeasonService   (loggerFactory.CreateLogger<SeasonService>());
        XpService     = new XpService(
            loggerFactory.CreateLogger<XpService>(),
            DbService, SeasonService, GenreService, Configuration);

        loggerFactory.CreateLogger<Plugin>()
            .LogInformation("🎮 MultiRank loaded – {N} genres ready", GenreService.GetAllGenres().Count);
    }

    public override string Name        => "MultiRank";
    public override Guid   Id          => new Guid("b2f3d4e5-6789-4abc-def0-123456789abc");
    public override string Description =>
        "Multi-genre ranking system: Isekai, Adventurer Guild, Fortnite, Waifu, Custom – powered by Playback Reporting.";

    public IEnumerable<PluginPageInfo> GetPages() => new[]
    {
        new PluginPageInfo
        {
            Name                = "MultiRank",
            EmbeddedResourcePath= $"{GetType().Namespace}.Web.multirank.html",
            DisplayName         = "🎮 MultiRank",
            EnableInMainMenu    = true,
        },
        new PluginPageInfo
        {
            Name                = "MultiRankConfig",
            EmbeddedResourcePath= $"{GetType().Namespace}.Web.config.html",
            DisplayName         = "MultiRank – Settings",
            EnableInMainMenu    = false,
        },
    };
}
