using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using GPSaveConverter.Library;

namespace GPSaveConverter.Tests
{
    public class StoredGameLibraryTests
    {
        [Fact]
        public void Serialize_Deserialize_RoundTrips()
        {
            var library = new StoredGameLibrary
            {
                Version = "1.0",
                GameInfo = new List<GameInfo>
                {
                    new GameInfo
                    {
                        Name = "Hades",
                        PackageName = "SupergiantGames.Hades_package",
                        BaseNonXboxSaveLocation = "%APPDATA%\\Hades\\save.dat"
                    },
                    new GameInfo
                    {
                        Name = "Celeste",
                        PackageName = "MattMakesGames.Celeste_package",
                        BaseNonXboxSaveLocation = "%LOCALAPPDATA%\\Celeste\\saves"
                    }
                }
            };

            string json = JsonSerializer.Serialize(library);
            var deserialized = JsonSerializer.Deserialize<StoredGameLibrary>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("1.0", deserialized.Version);
            Assert.Equal(2, deserialized.GameInfo.Count);
            Assert.Equal("Hades", deserialized.GameInfo[0].Name);
            Assert.Equal("SupergiantGames.Hades_package", deserialized.GameInfo[0].PackageName);
            Assert.Equal("%APPDATA%\\Hades\\save.dat", deserialized.GameInfo[0].BaseNonXboxSaveLocation);
            Assert.Equal("Celeste", deserialized.GameInfo[1].Name);
        }

        [Fact]
        public void Deserialize_WithFileTranslations_PreservesTranslations()
        {
            var library = new StoredGameLibrary
            {
                Version = "1.0",
                GameInfo = new List<GameInfo>
                {
                    new GameInfo
                    {
                        Name = "TestGame",
                        PackageName = "Test_package"
                    }
                }
            };
            library.GameInfo[0].FileTranslations.Add(new FileTranslation
            {
                NonXboxFilename = "save.dat",
                XboxFileID = "blob1",
                ContainerName1 = "c1",
                ContainerName2 = "c2",
                NamedRegexGroups = new[] { "(?<FileName>[\\w]+)" }
            });

            string json = JsonSerializer.Serialize(library);
            var deserialized = JsonSerializer.Deserialize<StoredGameLibrary>(json);

            Assert.Single(deserialized.GameInfo[0].FileTranslations);
            Assert.Equal("save.dat", deserialized.GameInfo[0].FileTranslations[0].NonXboxFilename);
            Assert.Equal("blob1", deserialized.GameInfo[0].FileTranslations[0].XboxFileID);
        }

        [Fact]
        public void Deserialize_WithTargetProfileTypes_PreservesTypes()
        {
            var library = new StoredGameLibrary
            {
                Version = "1.0",
                GameInfo = new List<GameInfo>
                {
                    new GameInfo
                    {
                        Name = "SteamGame",
                        PackageName = "Steam_package",
                        TargetProfileTypes = new[] { NonXboxProfile.ProfileType.Steam }
                    }
                }
            };

            string json = JsonSerializer.Serialize(library);
            var deserialized = JsonSerializer.Deserialize<StoredGameLibrary>(json);

            Assert.NotNull(deserialized.GameInfo[0].TargetProfileTypes);
            Assert.Single(deserialized.GameInfo[0].TargetProfileTypes);
            Assert.Equal(NonXboxProfile.ProfileType.Steam, deserialized.GameInfo[0].TargetProfileTypes[0]);
        }

        [Fact]
        public void Deserialize_EmptyLibrary_ReturnsValidObject()
        {
            string json = "{\"Version\":\"1.0\",\"GameInfo\":[]}";

            var deserialized = JsonSerializer.Deserialize<StoredGameLibrary>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("1.0", deserialized.Version);
            Assert.Empty(deserialized.GameInfo);
        }
    }
}
