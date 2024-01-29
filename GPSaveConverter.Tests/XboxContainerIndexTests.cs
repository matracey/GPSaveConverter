using System;
using System.IO;
using System.Text;
using NSubstitute;
using Xunit;
using GPSaveConverter.Interfaces;
using GPSaveConverter.Xbox;
using GPSaveConverter.Library;

namespace GPSaveConverter.Tests
{
    public class XboxContainerIndexTests
    {
        private static readonly string FixturesDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Fixtures");

        private readonly IFileSystem _fileSystem;

        public XboxContainerIndexTests()
        {
            _fileSystem = Substitute.For<IFileSystem>();
            XboxContainerIndex.FileSystem = _fileSystem;
            XboxFileContainer.FileSystem = _fileSystem;
            XboxFileInfo.FileSystem = _fileSystem;
        }

        [Fact]
        public void ParseContainersIndex_FromFixture_ReadsCorrectly()
        {
            byte[] indexData = File.ReadAllBytes(Path.Combine(FixturesDir, "containers.index"));

            string fakeProfileId = "0000000000000001";
            string profileFolder = @"C:\fake\wgs\" + fakeProfileId + "_0000000000000000";
            string indexPath = Path.Combine(profileFolder, "containers.index");

            var gameInfo = new GameInfo { PackageName = "FutureFriendsGames.CloverPit_2whsqx9fyfsdj" };

            _fileSystem.GetDirectories(Arg.Any<string>(), Arg.Any<string>())
                .Returns(new[] { profileFolder });
            _fileSystem.ReadAllBytes(indexPath)
                .Returns(indexData);

            XboxPackageList.Environment = Substitute.For<IEnvironment>();
            XboxPackageList.Environment.GetFolderPath(Arg.Any<Environment.SpecialFolder>())
                .Returns(@"C:\fake");
            _fileSystem.DirectoryExists(Arg.Any<string>()).Returns(true);
            _fileSystem.GetDirectories(Arg.Is<string>(s => s.Contains("Packages")))
                .Returns(new[] { @"C:\fake\Packages\FutureFriendsGames.CloverPit_2whsqx9fyfsdj" });
            _fileSystem.GetDirectories(Arg.Is<string>(s => s.Contains("wgs")), Arg.Any<string>())
                .Returns(new[] { profileFolder });
            XboxPackageList.FileSystem = _fileSystem;

            var index = new XboxContainerIndex(gameInfo, fakeProfileId);

            Assert.NotNull(index.Children);
            Assert.True(index.Children.Length > 0, "Expected at least one container");
            Assert.Equal(fakeProfileId, index.XboxProfileID);
        }

        [Fact]
        public void ParseContainersIndex_WrongPackageName_ThrowsFormatException()
        {
            byte[] indexData = File.ReadAllBytes(Path.Combine(FixturesDir, "containers.index"));

            string fakeProfileId = "0000000000000001";
            string profileFolder = @"C:\fake\wgs\" + fakeProfileId + "_0000000000000000";

            var gameInfo = new GameInfo { PackageName = "WrongPackage_name" };

            _fileSystem.GetDirectories(Arg.Any<string>(), Arg.Any<string>())
                .Returns(new[] { profileFolder });
            _fileSystem.ReadAllBytes(Arg.Any<string>())
                .Returns(indexData);

            XboxPackageList.Environment = Substitute.For<IEnvironment>();
            XboxPackageList.Environment.GetFolderPath(Arg.Any<Environment.SpecialFolder>())
                .Returns(@"C:\fake");
            _fileSystem.DirectoryExists(Arg.Any<string>()).Returns(true);
            _fileSystem.GetDirectories(Arg.Is<string>(s => s.Contains("Packages")))
                .Returns(new[] { @"C:\fake\Packages\WrongPackage_name" });
            _fileSystem.GetDirectories(Arg.Is<string>(s => s.Contains("wgs")), Arg.Any<string>())
                .Returns(new[] { profileFolder });
            XboxPackageList.FileSystem = _fileSystem;

            Assert.Throws<FileFormatException>(() =>
                new XboxContainerIndex(gameInfo, fakeProfileId));
        }
    }
}
