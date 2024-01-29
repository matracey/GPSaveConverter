using System.Threading.Tasks;
using NSubstitute;
using Xunit;
using GPSaveConverter.Interfaces;
using GPSaveConverter.Library;

namespace GPSaveConverter.Tests
{
    public class PCGameWikiFetchTests
    {
        private readonly IHttpClient _httpClient;
        private readonly PCGameWiki _wiki;

        public PCGameWikiFetchTests()
        {
            _httpClient = Substitute.For<IHttpClient>();
            _wiki = new PCGameWiki(_httpClient);
        }

        [Fact]
        public async Task FetchSaveLocation_ValidPage_SetsBaseLocation()
        {
            var gameInfo = new GameInfo { Name = "Hades" };

            // Step 1: Cargo query returns page ID
            _httpClient.DownloadStringAsync(Arg.Is<string>(u => u.Contains("action=cargoquery")))
                .Returns(Task.FromResult("{\"cargoquery\":[{\"title\":{\"PageID\":\"12345\"}}]}"));

            // Step 2: sections returns "Save game data location" section
            _httpClient.DownloadStringAsync(Arg.Is<string>(u => u.Contains("action=parse") && u.Contains("prop=sections")))
                .Returns(Task.FromResult("{\"parse\":{\"sections\":[{\"line\":\"Save game data location\",\"index\":\"3\"}]}}"));

            // Step 3: wikitext with save data
            _httpClient.DownloadStringAsync(Arg.Is<string>(u => u.Contains("action=parse") && u.Contains("prop=wikitext")))
                .Returns(Task.FromResult("{\"parse\":{\"wikitext\":\"{{Game data/saves|Windows|%APPDATA%\\\\Hades\\\\save.dat}}\"}}"));

            await _wiki.FetchSaveLocation(gameInfo);

            Assert.Equal("%APPDATA%\\Hades\\save.dat", gameInfo.BaseNonXboxSaveLocation);
        }

        [Fact]
        public async Task FetchSaveLocation_MissingPage_DoesNotSetLocation()
        {
            var gameInfo = new GameInfo { Name = "NonExistentGame" };

            // Cargo query returns empty results
            _httpClient.DownloadStringAsync(Arg.Any<string>())
                .Returns(Task.FromResult("{\"cargoquery\":[]}"));

            await _wiki.FetchSaveLocation(gameInfo);

            Assert.Null(gameInfo.BaseNonXboxSaveLocation);
        }

        [Fact]
        public async Task FetchSaveLocation_NoSaveSection_DoesNotSetLocation()
        {
            var gameInfo = new GameInfo { Name = "Hades" };

            _httpClient.DownloadStringAsync(Arg.Is<string>(u => u.Contains("action=cargoquery")))
                .Returns(Task.FromResult("{\"cargoquery\":[{\"title\":{\"PageID\":\"12345\"}}]}"));

            _httpClient.DownloadStringAsync(Arg.Is<string>(u => u.Contains("action=parse")))
                .Returns(Task.FromResult("{\"parse\":{\"sections\":[{\"line\":\"Gameplay\",\"index\":\"1\"}]}}"));

            await _wiki.FetchSaveLocation(gameInfo);

            Assert.Null(gameInfo.BaseNonXboxSaveLocation);
        }

        [Fact]
        public async Task FetchSaveLocation_SteamOnly_SetsProfileType()
        {
            var gameInfo = new GameInfo { Name = "SteamGame" };

            _httpClient.DownloadStringAsync(Arg.Is<string>(u => u.Contains("action=cargoquery")))
                .Returns(Task.FromResult("{\"cargoquery\":[{\"title\":{\"PageID\":\"99\"}}]}"));

            _httpClient.DownloadStringAsync(Arg.Is<string>(u => u.Contains("action=parse") && u.Contains("prop=sections")))
                .Returns(Task.FromResult("{\"parse\":{\"sections\":[{\"line\":\"Save game data location\",\"index\":\"2\"}]}}"));

            // Only Steam entry, no Windows entry
            _httpClient.DownloadStringAsync(Arg.Is<string>(u => u.Contains("action=parse") && u.Contains("prop=wikitext")))
                .Returns(Task.FromResult("{\"parse\":{\"wikitext\":\"{{Game data/saves|Steam|<Steam-folder>\\\\saves}}\"}}"));

            await _wiki.FetchSaveLocation(gameInfo);

            Assert.Equal("<Steam-folder>\\saves", gameInfo.BaseNonXboxSaveLocation);
            Assert.NotNull(gameInfo.TargetProfileTypes);
            Assert.Single(gameInfo.TargetProfileTypes);
            Assert.Equal(NonXboxProfile.ProfileType.Steam, gameInfo.TargetProfileTypes[0]);
        }

        [Fact]
        public async Task FetchSaveLocation_HttpException_DoesNotThrow()
        {
            var gameInfo = new GameInfo { Name = "Hades" };

            _httpClient.DownloadStringAsync(Arg.Any<string>())
                .Returns<string>(x => { throw new System.Net.Http.HttpRequestException("Network error"); });

            await _wiki.FetchSaveLocation(gameInfo);

            Assert.Null(gameInfo.BaseNonXboxSaveLocation);
        }
    }
}
