using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;

namespace AmbientWeatherWigiDash
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string output = args.Length > 0 ? args[0] : Path.Combine(Environment.CurrentDirectory, "artifacts", "previews");
            Directory.CreateDirectory(output);
            Save(output, "weather-full-5x4.png", new Size(1016, 592));
            Save(output, "weather-wide-3x2.png", new Size(608, 294));
            Save(output, "weather-medium-2x2.png", new Size(404, 294));
            Save(output, "weather-wide-2x1.png", new Size(404, 145));
            Save(output, "weather-tile-1x1.png", new Size(200, 145));

            bool credentialTestPassed = false;
            try
            {
                string secret = "test-secret-not-a-real-key";
                string protectedValue = SecretProtector.Protect(secret);
                if (secret != SecretProtector.Unprotect(protectedValue) || protectedValue.Contains(secret))
                {
                    Console.Error.WriteLine("Credential protection self-test failed.");
                    return 1;
                }
                credentialTestPassed = true;
            }
            catch (CryptographicException)
            {
                Console.WriteLine("Credential protection self-test skipped because this process has no loaded Windows user profile.");
            }
            const string sampleJson = "[{\"macAddress\":\"00:00:00:00:00:00\",\"info\":{\"name\":\"Test Station\",\"location\":\"Test Yard\"},\"lastData\":{\"tempf\":72.4,\"humidity\":54}}]";
            var devices = AmbientWeatherClient.ParseDevicesJson(sampleJson);
            if (devices.Count != 1 || devices[0].Name != "Test Station" || devices[0].LastData.Number("tempf") != 72.4)
            {
                Console.Error.WriteLine("Ambient Weather JSON parsing self-test failed.");
                return 1;
            }
            var historicalReadings = new System.Collections.Generic.List<WeatherReading>
            {
                new WeatherReading(new System.Collections.Generic.Dictionary<string, object> { { "tempf", 60.0 } }),
                new WeatherReading(new System.Collections.Generic.Dictionary<string, object> { { "tempf", 68.0 } })
            };
            var currentReading = new WeatherReading(new System.Collections.Generic.Dictionary<string, object> { { "tempf", 72.4 } });
            var currentInclusiveHistory = WeatherRenderer.IncludeCurrentReading(historicalReadings, currentReading);
            if (WeatherRenderer.HistoryExtreme(currentInclusiveHistory, "tempf", true) != 72.4)
            {
                Console.Error.WriteLine("Current-inclusive temperature range self-test failed.");
                return 1;
            }
            Console.WriteLine(credentialTestPassed
                ? "Rendered five previews and passed credential protection, API parsing, and temperature range self-tests."
                : "Rendered five previews and passed API parsing and temperature range self-tests.");
            return 0;
        }

        private static void Save(string output, string name, Size size)
        {
            using (Bitmap bitmap = WeatherRenderer.RenderPreview(size))
                bitmap.Save(Path.Combine(output, name), ImageFormat.Png);
        }
    }
}
