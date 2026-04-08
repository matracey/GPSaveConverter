using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GPSaveConverter.Interfaces;

namespace GPSaveConverter.Library
{
    internal class PCGameWiki
    {
        private static readonly NLog.Logger logger = LogHelper.getClassLogger();

        public static readonly string[,] FolderNameSubstitutions = new string[,] { { "{{p|userprofile}}",   "%USERPROFILE%"     }
                                                                                  , { "{{p|appdata}}",       "%APPDATA%"         }
                                                                                  , { "{{p|localappdata}}",  "%LOCALAPPDATA%"    }
                                                                                  , { "{{p|programdata}}",   "%PROGRAMDATA%"     }
                                                                                  , { "{{p|uid}}",           "<user-id>"         }
                                                                                  , { "{{p|steam}}",         "<Steam-folder>"    } };

        private readonly IHttpClient httpClient;

        internal PCGameWiki(IHttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task FetchSaveLocation(GameInfo gameInfo)
        {
            logger.Info("Fetching save data from pcgamingwiki.com for '{0}'", gameInfo.Name);

            string? wikiTable;
            try
            {
                wikiTable = await FetchSaveGameSectionWikiText(gameInfo.Name);
            }
            catch (Exception e)
            {
                logger.Warn(e, "Unable to fetch save data location for '{0}'", gameInfo.Name);
                return;
            }

            if (wikiTable == null)
            {
                return;
            }

            Dictionary<string, string> saveLocationTable = ParseWikiTable(wikiTable);

            if (saveLocationTable.TryGetValue("Windows", out string? foundLocation) && !string.IsNullOrEmpty(foundLocation))
            {
                gameInfo.BaseNonXboxSaveLocation = foundLocation;
                logger.Info("Save data location loaded for '{0}'", gameInfo.Name);
            }
            else if (saveLocationTable.TryGetValue("Steam", out foundLocation) && !string.IsNullOrEmpty(foundLocation))
            {
                gameInfo.BaseNonXboxSaveLocation = foundLocation;
                gameInfo.TargetProfileTypes = new NonXboxProfile.ProfileType[] { NonXboxProfile.ProfileType.Steam };
                gameInfo.TargetProfiles = new NonXboxProfile[] { new NonXboxProfile(0, NonXboxProfile.ProfileType.Steam) };
                logger.Info("Save data location loaded (Steam) for '{0}'", gameInfo.Name);
            }
        }

        private async Task<string?> FetchSaveGameSectionWikiText(string gameName)
        {
            string encodedName = Uri.EscapeDataString(gameName);
            string cargoUrl = $"https://www.pcgamingwiki.com/w/api.php?action=cargoquery&tables=Infobox_game&fields=Infobox_game._pageID=PageID&where=Infobox_game._pageName=%22{encodedName}%22&format=json";
            string cargoJson = await httpClient.DownloadStringAsync(cargoUrl);

            JsonNode? cargoRoot = JsonNode.Parse(cargoJson);
            JsonArray? results = cargoRoot?["cargoquery"]?.AsArray();

            if (results == null || results.Count == 0)
            {
                logger.Info("Game '{0}' not found on PCGamingWiki", gameName);
                return null;
            }

            string? pageID = results[0]?["title"]?["PageID"]?.GetValue<string>();
            if (string.IsNullOrEmpty(pageID))
            {
                logger.Warn("Page ID missing in Cargo response for '{0}'", gameName);
                return null;
            }

            string sectionsUrl = $"https://www.pcgamingwiki.com/w/api.php?action=parse&pageid={pageID}&formatversion=2&format=json&prop=sections";
            string sectionsJson = await httpClient.DownloadStringAsync(sectionsUrl);
            JsonNode? sectionsRoot = JsonNode.Parse(sectionsJson);
            JsonArray? sections = sectionsRoot?["parse"]?["sections"]?.AsArray();

            if (sections == null)
            {
                logger.Warn("Failed to parse sections for page {0}", pageID);
                return null;
            }

            string? sectionIndex = null;
            foreach (JsonNode? n in sections)
            {
                if (n?["line"]?.GetValue<string>() == "Save game data location")
                {
                    sectionIndex = n["index"]?.GetValue<string>();
                    break;
                }
            }

            if (string.IsNullOrEmpty(sectionIndex))
            {
                logger.Info("No 'Save game data location' section found for '{0}'", gameName);
                return null;
            }

            string wikiTextUrl = $"https://www.pcgamingwiki.com/w/api.php?action=parse&pageid={pageID}&formatversion=2&format=json&prop=wikitext&section={sectionIndex}";
            string saveFileSectionJson = await httpClient.DownloadStringAsync(wikiTextUrl);
            JsonNode? saveFileSectionRoot = JsonNode.Parse(saveFileSectionJson);

            return saveFileSectionRoot?["parse"]?["wikitext"]?.GetValue<string>();
        }

        internal static Dictionary<string, string> ParseWikiTable(string unparsedWikiTable)
        {
            int entryStart = unparsedWikiTable.IndexOf("{{Game data/saves|");
            int entryEnd = entryStart;
            Dictionary<string, string> result = new Dictionary<string, string>();
            while (entryStart != -1)
            {
                int subEntryEnd = entryStart;
                int subEntryStart = entryStart;
                do
                {
                    entryEnd = unparsedWikiTable.IndexOf("}}", subEntryEnd + 2);
                    subEntryStart = unparsedWikiTable.IndexOf("{{", subEntryEnd + 2, entryEnd - subEntryEnd - 2);
                    subEntryEnd = entryEnd;
                } while (subEntryStart != -1);

                string entryLine = unparsedWikiTable.Substring(entryStart, entryEnd - entryStart);
                entryLine = NameSubstitution(entryLine);
                string[] lineInfo = entryLine.Split('|');

                result.Add(lineInfo[1], lineInfo[2]);

                entryStart = unparsedWikiTable.IndexOf("{{Game data/saves|", entryEnd);
            }

            return result;
        }

        internal static string NameSubstitution(string path)
        {
            for (int i = 0; i < FolderNameSubstitutions.GetLength(0); i++)
            {
                path = Regex.Replace(path, Regex.Escape(FolderNameSubstitutions[i, 0]), FolderNameSubstitutions[i, 1].Replace("$", "$$"), RegexOptions.IgnoreCase);
            }
            return path;
        }
    }
}
