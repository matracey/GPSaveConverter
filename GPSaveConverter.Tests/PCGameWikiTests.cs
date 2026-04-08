using System.Collections.Generic;
using Xunit;
using GPSaveConverter.Library;

namespace GPSaveConverter.Tests
{
    public class PCGameWikiTests
    {
        [Theory]
        [InlineData("{{p|userprofile}}", "%USERPROFILE%")]
        [InlineData("{{p|appdata}}", "%APPDATA%")]
        [InlineData("{{p|localappdata}}", "%LOCALAPPDATA%")]
        [InlineData("{{p|programdata}}", "%PROGRAMDATA%")]
        [InlineData("{{p|uid}}", "<user-id>")]
        [InlineData("{{p|steam}}", "<Steam-folder>")]
        [InlineData("{{p|game}}", "<game-folder>")]
        [InlineData("{{p|userprofile\\Documents}}", "%USERPROFILE%\\Documents")]
        [InlineData("{{p|appdata\\SomeGame}}", "%APPDATA%\\SomeGame")]
        public void NameSubstitution_ReplacesFolderTokens(string input, string expected)
        {
            string result = PCGameWiki.NameSubstitution(input);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void NameSubstitution_ReplacesMultipleTokens()
        {
            string input = "{{p|appdata}}\\SomeGame\\{{p|uid}}";

            string result = PCGameWiki.NameSubstitution(input);

            Assert.Equal("%APPDATA%\\SomeGame\\<user-id>", result);
        }

        [Fact]
        public void NameSubstitution_IsCaseInsensitive()
        {
            string result = PCGameWiki.NameSubstitution("{{P|APPDATA}}");

            Assert.Equal("%APPDATA%", result);
        }

        [Fact]
        public void NameSubstitution_NoTokens_ReturnsUnchanged()
        {
            string input = "C:\\Users\\TestUser\\AppData";

            string result = PCGameWiki.NameSubstitution(input);

            Assert.Equal(input, result);
        }

        [Fact]
        public void ParseWikiTable_SingleEntry_ReturnsOneMapping()
        {
            string wikiText = "{{Game data/saves|%APPDATA%|\\SomeGame\\save.dat}}";

            Dictionary<string, string> result = PCGameWiki.ParseWikiTable(wikiText);

            Assert.Single(result);
            Assert.Equal("\\SomeGame\\save.dat", result["%APPDATA%"]);
        }

        [Fact]
        public void ParseWikiTable_MultipleEntries_ReturnsAll()
        {
            string wikiText =
                "{{Game data/saves|%APPDATA%|\\Game1\\save.dat}}" +
                "Some text between" +
                "{{Game data/saves|%LOCALAPPDATA%|\\Game2\\config.ini}}";

            Dictionary<string, string> result = PCGameWiki.ParseWikiTable(wikiText);

            Assert.Equal(2, result.Count);
            Assert.Equal("\\Game1\\save.dat", result["%APPDATA%"]);
            Assert.Equal("\\Game2\\config.ini", result["%LOCALAPPDATA%"]);
        }

        [Fact]
        public void ParseWikiTable_NoEntries_ReturnsEmptyDictionary()
        {
            string wikiText = "This is a wiki page with no save data entries.";

            Dictionary<string, string> result = PCGameWiki.ParseWikiTable(wikiText);

            Assert.Empty(result);
        }

        [Fact]
        public void ParseWikiTable_NestedBraces_HandlesCorrectly()
        {
            // Nested {{}} inside the entry — parser must find the matching closing braces
            string wikiText = "{{Game data/saves|{{p|appdata}}|\\SomeGame\\save.dat}}";

            Dictionary<string, string> result = PCGameWiki.ParseWikiTable(wikiText);

            Assert.Single(result);
            Assert.Equal("\\SomeGame\\save.dat", result["%APPDATA%"]);
        }

        [Fact]
        public void ParseWikiTable_NestedTemplateWithSubpath_SubstitutesAndPreservesPath()
        {
            // {{p|userprofile\Documents}} should be substituted to %USERPROFILE%\Documents
            string wikiText = "{{Game data/saves|{{p|userprofile\\Documents}}\\Saved Games\\Hades|}}";

            Dictionary<string, string> result = PCGameWiki.ParseWikiTable(wikiText);

            Assert.Single(result);
            Assert.Equal("%USERPROFILE%\\Documents\\Saved Games\\Hades", result.Keys.First());
        }

        [Fact]
        public void ParseWikiTable_KnownNestedTemplateWithSubpath_SubstitutesCorrectly()
        {
            // {{p|userprofile}} is substituted, but the \Documents subpath is outside the template
            string wikiText = "{{Game data/saves|{{p|userprofile}}\\Documents\\Saved Games\\Hades|}}";

            Dictionary<string, string> result = PCGameWiki.ParseWikiTable(wikiText);

            Assert.Single(result);
            Assert.Equal("%USERPROFILE%\\Documents\\Saved Games\\Hades", result.Keys.First());
        }

        [Fact]
        public void SplitTopLevelPipes_NoPipes_ReturnsSingleElement()
        {
            string[] result = PCGameWiki.SplitTopLevelPipes("hello");

            Assert.Single(result);
            Assert.Equal("hello", result[0]);
        }

        [Fact]
        public void SplitTopLevelPipes_TopLevelPipes_SplitsCorrectly()
        {
            string[] result = PCGameWiki.SplitTopLevelPipes("{{Game data/saves|path|file}}");

            Assert.Equal(3, result.Length);
            Assert.Equal("{{Game data/saves", result[0]);
            Assert.Equal("path", result[1]);
            Assert.Equal("file}}", result[2]);
        }

        [Fact]
        public void SplitTopLevelPipes_NestedPipes_NotSplit()
        {
            string[] result = PCGameWiki.SplitTopLevelPipes("{{Game data/saves|{{p|userprofile\\Docs}}\\Hades|save.dat}}");

            Assert.Equal(3, result.Length);
            Assert.Equal("{{Game data/saves", result[0]);
            Assert.Equal("{{p|userprofile\\Docs}}\\Hades", result[1]);
            Assert.Equal("save.dat}}", result[2]);
        }
    }
}
