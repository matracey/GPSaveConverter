using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using GPSaveConverter.Interfaces;

namespace GPSaveConverter.Library
{
    internal class Steam
    {
        private const ulong SteamID64IndividualProfile = 0x0110000100000000;

        private static readonly string? ApiKey = Environment.GetEnvironmentVariable("STEAM_API_KEY");

        private readonly IHttpClient httpClient;

        internal Steam(IHttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        internal static bool IsAvailable => !string.IsNullOrEmpty(ApiKey);

        internal async Task GetUserInformation(NonXboxProfile profile)
        {
            if (!IsAvailable) return;

            try
            {
                ulong steamID64 = profile.IDType == NonXboxProfile.UserIDType.steamID3 ? GetSteamID64(profile.UserID) : ulong.Parse(profile.UserID);
                string url = String.Format(@"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={0}&steamids={1}"
                                            , ApiKey
                                            , steamID64);
                string queryJson = await httpClient.DownloadStringAsync(url);
                JsonNode queryRoot = JsonValue.Parse(queryJson);

                profile.UserName = queryRoot["response"]["players"][0]["personaname"].GetValue<string>();
                profile.UserIconLocation = queryRoot["response"]["players"][0]["avatar"].GetValue<string>();
            }
            catch (Exception e) { }
        }

        internal async Task<System.Drawing.Bitmap?> LoadIcon(NonXboxProfile profile)
        {
            if (string.IsNullOrEmpty(profile.UserIconLocation)) return null;

            try
            {
                byte[] imageData = await httpClient.DownloadDataAsync(profile.UserIconLocation);
                return new System.Drawing.Bitmap(new System.IO.MemoryStream(imageData));
            }
            catch (Exception e) { }
            return null;
        }

        /// <summary>
        /// https://developer.valvesoftware.com/wiki/SteamID
        /// </summary>
        /// <param name="steam3ID"></param>
        /// <param name="accoundIDY"></param>
        /// <returns></returns>
        internal static ulong GetSteamID64(string steam3ID)
        {
            ulong steam3IDValue = ulong.Parse(steam3ID);

            return SteamID64IndividualProfile | (steam3IDValue);
        }
    }
}
