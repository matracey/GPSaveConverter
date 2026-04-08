namespace GPSaveConverter.Interfaces
{
    using System.Net.Http;
    using System.Reflection;
    using System.Threading.Tasks;

    /// <summary>
    /// Default implementation that delegates to System.Net.Http.HttpClient.
    /// </summary>
    public class DefaultHttpClient : IHttpClient
    {
        private static readonly HttpClient httpClient = CreateHttpClient();

        public string DownloadString(string address)
        {
            return httpClient.GetStringAsync(address).GetAwaiter().GetResult();
        }

        public async Task<string> DownloadStringAsync(string address)
        {
            return await httpClient.GetStringAsync(address);
        }

        public async Task<byte[]> DownloadDataAsync(string address)
        {
            return await httpClient.GetByteArrayAsync(address);
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            var assemblyName = Assembly.GetExecutingAssembly().GetName();
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"{assemblyName.Name}/{assemblyName.Version}");
            return client;
        }
    }
}
