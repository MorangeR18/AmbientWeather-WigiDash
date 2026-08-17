using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace AmbientWeatherWigiDash
{
    internal sealed class AmbientWeatherClient : IDisposable
    {
        private const string BaseUrl = "https://rt.ambientweather.net/v1";
        private readonly HttpClient http;
        private readonly SemaphoreSlim requestGate = new SemaphoreSlim(1, 1);
        private DateTime lastRequestUtc = DateTime.MinValue;

        public AmbientWeatherClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(15);
            Version version = typeof(AmbientWeatherClient).Assembly.GetName().Version;
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AmbientWeather-WigiDash/" + version.Major + "." + version.Minor + "." + version.Build);
        }

        public async Task<List<WeatherDevice>> GetDevicesAsync(string apiKey, string applicationKey, CancellationToken token)
        {
            string url = BaseUrl + "/devices?apiKey=" + Uri.EscapeDataString(apiKey) +
                         "&applicationKey=" + Uri.EscapeDataString(applicationKey);
            object parsed = await GetJsonAsync(url, token).ConfigureAwait(false);
            return ParseDevices(parsed);
        }

        internal static List<WeatherDevice> ParseDevicesJson(string json)
        {
            object parsed = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.DeserializeObject(json);
            return ParseDevices(parsed);
        }

        private static List<WeatherDevice> ParseDevices(object parsed)
        {
            var devices = new List<WeatherDevice>();
            var array = parsed as IEnumerable;
            if (array == null) return devices;

            foreach (object item in array)
            {
                var root = item as Dictionary<string, object>;
                if (root == null) continue;
                var info = GetDictionary(root, "info");
                var reading = GetDictionary(root, "lastData");
                devices.Add(new WeatherDevice
                {
                    MacAddress = GetString(root, "macAddress"),
                    Name = FirstNonEmpty(GetString(info, "name"), "Weather Station"),
                    Location = FirstNonEmpty(GetString(info, "location"), string.Empty),
                    LastData = new WeatherReading(reading)
                });
            }
            return devices;
        }

        public async Task<List<WeatherReading>> GetHistoryAsync(string macAddress, string apiKey, string applicationKey, CancellationToken token)
        {
            string url = BaseUrl + "/devices/" + Uri.EscapeDataString(macAddress) +
                         "?apiKey=" + Uri.EscapeDataString(apiKey) +
                         "&applicationKey=" + Uri.EscapeDataString(applicationKey) + "&limit=288";
            object parsed = await GetJsonAsync(url, token).ConfigureAwait(false);
            var result = new List<WeatherReading>();
            var array = parsed as IEnumerable;
            if (array == null) return result;
            foreach (object item in array)
            {
                var reading = item as Dictionary<string, object>;
                if (reading != null) result.Add(new WeatherReading(reading));
            }
            result.Reverse();
            return result;
        }

        private async Task<object> GetJsonAsync(string url, CancellationToken token)
        {
            await requestGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                TimeSpan remaining = TimeSpan.FromMilliseconds(1100) - (DateTime.UtcNow - lastRequestUtc);
                if (remaining > TimeSpan.Zero) await Task.Delay(remaining, token).ConfigureAwait(false);
                using (HttpResponseMessage response = await http.GetAsync(url, token).ConfigureAwait(false))
                {
                    lastRequestUtc = DateTime.UtcNow;
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if ((int)response.StatusCode == 429) throw new InvalidOperationException("Ambient Weather rate limit reached. The widget will retry automatically.");
                    if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                        throw new InvalidOperationException("Ambient Weather rejected the API credentials.");
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("Ambient Weather returned HTTP " + (int)response.StatusCode + ".");
                    return new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.DeserializeObject(body);
                }
            }
            finally { requestGate.Release(); }
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> source, string key)
        {
            object value;
            return source != null && source.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        private static string GetString(Dictionary<string, object> source, string key)
        {
            object value;
            return source != null && source.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : null;
        }

        private static string FirstNonEmpty(string value, string fallback) { return string.IsNullOrWhiteSpace(value) ? fallback : value; }
        public void Dispose() { http.Dispose(); requestGate.Dispose(); }
    }
}
