using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AmbientWeatherWigiDash
{
    // Compile the real SettingsControl against this in-memory parent. No widget
    // framework, persisted profile, network client, or real credentials are used.
    internal sealed class AmbientWeatherWidgetInstance
    {
        internal string ApiKey = "synthetic-saved-api/key";
        internal string ApplicationKey = "synthetic-saved-application+key";
        internal string SelectedMacAddress = "02:00:00:00:00:31";
        internal int RefreshSeconds = 60;
        internal DisplayUnits Units = DisplayUnits.Imperial;
        internal List<WeatherDevice> CurrentDevices = new List<WeatherDevice>();
        internal TaskCompletionSource<List<WeatherDevice>> Pending = new TaskCompletionSource<List<WeatherDevice>>();
        internal string TestedApiKey;
        internal string TestedApplicationKey;
        internal int SaveCount;
        internal bool HasCredentials { get { return ApiKey.Length > 0 && ApplicationKey.Length > 0; } }

        internal Task<List<WeatherDevice>> TestCredentialsAsync(string apiKey, string applicationKey)
        {
            TestedApiKey = apiKey;
            TestedApplicationKey = applicationKey;
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(applicationKey))
                throw new ConnectionException(ConnectionErrorKind.CredentialsRequired);
            return Pending.Task;
        }

        internal void ApplySettings(string apiKey, string applicationKey, string macAddress, int seconds, DisplayUnits units)
        {
            SaveCount++;
        }
    }

    internal static class Program
    {
        private const string NewApiKey = "synthetic-unsaved-api/key+value";
        private const string NewApplicationKey = "synthetic-unsaved-application/key+value";
        private const string EditedApiKey = "synthetic-edited-api/key";
        private const string EditedApplicationKey = "synthetic-edited-application/key";
        private const string OtherStation = "02:00:00:00:00:32";
        private static int passed;

        [STAThread]
        private static int Main()
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            try
            {
                Run("Connection failure hides unsaved and saved secrets after inputs change", TestUnsavedCredentials);
                Run("Unexpected exception text is not displayed by the settings control", TestUnexpectedError);
                Run("Missing credentials retain a helpful settings message", TestMissingCredentials);
                Run("Empty account retains a helpful settings message", TestNoStations);
                Run("Successful connection still populates the station selector", TestSuccessfulConnection);
                Console.WriteLine("Settings error checks passed: " + passed + " scenarios.");
                return 0;
            }
            catch
            {
                // Do not print caught exception text: these tests intentionally
                // inject sensitive-looking payloads into exceptions.
                return 1;
            }
        }

        private static void Run(string name, Action test)
        {
            try { test(); }
            catch
            {
                Console.Error.WriteLine("FAIL: " + name);
                throw;
            }
            passed++;
            Console.WriteLine("PASS: " + name);
        }

        private static void TestUnsavedCredentials()
        {
            var parent = new AmbientWeatherWidgetInstance();
            var control = new SettingsControl(parent);
            Field<PasswordBox>(control, "apiKey").Password = NewApiKey;
            Field<PasswordBox>(control, "applicationKey").Password = NewApplicationKey;
            ClickTest(control);
            Check(parent.TestedApiKey == NewApiKey && parent.TestedApplicationKey == NewApplicationKey);
            Check(!Field<Button>(control, "testButton").IsEnabled && !Field<Button>(control, "saveButton").IsEnabled);

            // A request retains its original values while the user edits the form.
            Field<PasswordBox>(control, "apiKey").Password = EditedApiKey;
            Field<PasswordBox>(control, "applicationKey").Password = EditedApplicationKey;
            string payload = SensitivePayload(parent);
            var failure = new HttpRequestException(payload, new Exception(payload));
            failure.Data["request"] = payload;
            parent.Pending.SetException(failure);
            Finish(control);
            string message = Field<TextBlock>(control, "status").Text;
            Check(message == ConnectionErrors.GetMessage(new HttpRequestException()));
            Check(message.IndexOf("internet", StringComparison.OrdinalIgnoreCase) >= 0);
            AssertSafe(message, parent);
            AssertUnchanged(parent);
        }

        private static void TestUnexpectedError()
        {
            var parent = new AmbientWeatherWidgetInstance();
            var control = new SettingsControl(parent);
            ClickTest(control);
            parent.Pending.SetException(new InvalidOperationException(SensitivePayload(parent)));
            Finish(control);
            string message = Field<TextBlock>(control, "status").Text;
            Check(!string.IsNullOrWhiteSpace(message));
            AssertSafe(message, parent);
            AssertUnchanged(parent);
        }

        private static void TestMissingCredentials()
        {
            var parent = new AmbientWeatherWidgetInstance();
            var control = new SettingsControl(parent);
            Field<PasswordBox>(control, "apiKey").Password = string.Empty;
            ClickTest(control);
            Finish(control);
            Check(Field<TextBlock>(control, "status").Text == "Both API keys are required.");
            AssertUnchanged(parent);
        }

        private static void TestNoStations()
        {
            var parent = new AmbientWeatherWidgetInstance();
            var control = new SettingsControl(parent);
            ClickTest(control);
            parent.Pending.SetResult(new List<WeatherDevice>());
            Finish(control);
            string message = Field<TextBlock>(control, "status").Text;
            Check(message == ConnectionErrors.GetMessage(new ConnectionException(ConnectionErrorKind.NoStations)));
            Check(message.IndexOf("stations", StringComparison.OrdinalIgnoreCase) >= 0);
            AssertSafe(message, parent);
            AssertUnchanged(parent);
        }

        private static void TestSuccessfulConnection()
        {
            var parent = new AmbientWeatherWidgetInstance();
            var control = new SettingsControl(parent);
            ClickTest(control);
            var station = new WeatherDevice { MacAddress = parent.SelectedMacAddress, Name = "Synthetic Station", Location = "Synthetic Yard" };
            parent.Pending.SetResult(new List<WeatherDevice> { station });
            Finish(control);
            Check(Field<TextBlock>(control, "status").Text == "Connected. Found 1 station.");
            var selector = Field<ComboBox>(control, "station");
            Check(selector.Items.Count == 1 && object.ReferenceEquals(selector.SelectedItem, station));
            AssertUnchanged(parent);
        }

        private static void ClickTest(SettingsControl control)
        {
            Field<Button>(control, "testButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        private static void Finish(SettingsControl control)
        {
            var button = Field<Button>(control, "testButton");
            if (!button.IsEnabled)
            {
                var frame = new DispatcherFrame();
                DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
                timer.Tick += delegate
                {
                    if (button.IsEnabled || DateTime.UtcNow >= deadline) frame.Continue = false;
                };
                timer.Start();
                try { Dispatcher.PushFrame(frame); }
                finally { timer.Stop(); }
            }
            Check(button.IsEnabled && Field<Button>(control, "saveButton").IsEnabled);
        }

        private static T Field<T>(SettingsControl control, string name) where T : class
        {
            return (T)typeof(SettingsControl).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(control);
        }

        private static string[] Secrets(AmbientWeatherWidgetInstance parent)
        {
            return new[] { parent.ApiKey, parent.ApplicationKey, parent.SelectedMacAddress, NewApiKey,
                NewApplicationKey, EditedApiKey, EditedApplicationKey, OtherStation };
        }

        private static string SensitivePayload(AmbientWeatherWidgetInstance parent)
        {
            var values = new List<string>();
            foreach (string secret in Secrets(parent))
            {
                values.Add(secret);
                values.Add(Uri.EscapeDataString(secret));
                values.Add(Uri.EscapeDataString(Uri.EscapeDataString(secret)));
            }
            return "Synthetic request failure: " + string.Join(" ", values);
        }

        private static void AssertSafe(string message, AmbientWeatherWidgetInstance parent)
        {
            foreach (string secret in Secrets(parent))
            {
                Check(message.IndexOf(secret, StringComparison.OrdinalIgnoreCase) < 0);
                string encoded = Uri.EscapeDataString(secret);
                Check(message.IndexOf(encoded, StringComparison.OrdinalIgnoreCase) < 0);
                Check(message.IndexOf(Uri.EscapeDataString(encoded), StringComparison.OrdinalIgnoreCase) < 0);
            }
        }

        private static void AssertUnchanged(AmbientWeatherWidgetInstance parent)
        {
            Check(parent.SaveCount == 0);
            Check(parent.ApiKey == "synthetic-saved-api/key" && parent.ApplicationKey == "synthetic-saved-application+key");
            Check(parent.SelectedMacAddress == "02:00:00:00:00:31");
        }

        private static void Check(bool condition)
        {
            if (!condition) throw new InvalidOperationException("Settings assertion failed.");
        }
    }
}
