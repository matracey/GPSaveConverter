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

        public static readonly string[,] FolderNameSubstitutions = new string[,] { { "userprofile",   "%USERPROFILE%"     }
                                                                                  , { "appdata",       "%APPDATA%"         }
                                                                                  , { "localappdata",  "%LOCALAPPDATA%"    }
                                                                                  , { "programdata",   "%PROGRAMDATA%"     }
                                                                                  , { "uid",           "<user-id>"         }
                                                                                  , { "steam",         "<Steam-folder>"    }
                                                                                  , { "game",          "<game-folder>"     } };

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
                string[] lineInfo = SplitTopLevelPipes(entryLine);

                if (lineInfo.Length >= 3)
                {
                    result.Add(lineInfo[1], lineInfo[2]);
                }

                entryStart = unparsedWikiTable.IndexOf("{{Game data/saves|", entryEnd);
            }

            return result;
        }

        /// <summary>
        /// Split on '|' characters that are not inside nested {{ }} templates.
        /// </summary>
        internal static string[] SplitTopLevelPipes(string entry)
        {
            var parts = new List<string>();
            int depth = 0;
            int segmentStart = 0;

            for (int i = 0; i < entry.Length; i++)
            {
                if (i < entry.Length - 1 && entry[i] == '{' && entry[i + 1] == '{')
                {
                    depth++;
                    i++; // skip second '{'
                }
                else if (i < entry.Length - 1 && entry[i] == '}' && entry[i + 1] == '}')
                {
                    depth--;
                    i++; // skip second '}'
                }
                else if (entry[i] == '|' && depth <= 1)
                {
                    // depth 1 = inside the outer {{Game data/saves|...}} — these are the real delimiters
                    // depth 2+ = inside nested {{p|...}} — skip these
                    parts.Add(entry.Substring(segmentStart, i - segmentStart));
                    segmentStart = i + 1;
                }
            }

            parts.Add(entry.Substring(segmentStart));
            return parts.ToArray();
        }

        internal static string NameSubstitution(string path)
        {
            for (int i = 0; i < FolderNameSubstitutions.GetLength(0); i++)
            {
                string folderName = FolderNameSubstitutions[i, 0];
                string replacement = FolderNameSubstitutions[i, 1];
                // Match {{p|folderName}} or {{p|folderName\subpath}}
                string pattern = @"\{\{p\|" + Regex.Escape(folderName) + @"(\\[^}]*)?\}\}";
                path = Regex.Replace(path, pattern, replacement.Replace("$", "$$") + "$1", RegexOptions.IgnoreCase);
            }
            return path;
        }
    }
}
