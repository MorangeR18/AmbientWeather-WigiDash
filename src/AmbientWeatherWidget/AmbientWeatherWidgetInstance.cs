using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using WigiDashWidgetFramework;
using WigiDashWidgetFramework.WidgetUtility;

namespace AmbientWeatherWigiDash
{
    public sealed class AmbientWeatherWidgetInstance : IWidgetInstance
    {
        private readonly object bitmapLock = new object();
        private readonly object stateLock = new object();
        private readonly AmbientWeatherClient client = new AmbientWeatherClient();
        private readonly SemaphoreSlim refreshGate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        private Bitmap currentBitmap;
        private Task workerTask;
        private bool sleeping;
        private DateTime lastHistoryRefresh = DateTime.MinValue;
        private SettingsControl settingsControl;

        internal string ApiKey = string.Empty;
        internal string ApplicationKey = string.Empty;
        internal string SelectedMacAddress = string.Empty;
        internal int RefreshSeconds = 60;
        internal DisplayUnits Units = DisplayUnits.Imperial;
        internal WeatherState State = new WeatherState();

        public IWidgetObject WidgetObject { get; private set; }
        public Guid Guid { get; private set; }
        public WidgetSize WidgetSize { get; private set; }
        public event WidgetUpdatedEventHandler WidgetUpdated;

        public AmbientWeatherWidgetInstance(IWidgetObject widgetObject, WidgetSize widgetSize, Guid instanceGuid)
        {
            WidgetObject = widgetObject;
            WidgetSize = widgetSize;
            Guid = instanceGuid;
            LoadSettings();
            State.IsConfigured = HasCredentials;
            RenderAndPublish();
            workerTask = Task.Run((Func<Task>)WorkerAsync);
        }

        internal bool HasCredentials { get { return !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApplicationKey); } }

        public void RequestUpdate() { RenderAndPublish(); }

        public void ClickEvent(ClickType clickType, int x, int y)
        {
            if (clickType == ClickType.Single || clickType == ClickType.Double)
                Task.Run((Func<Task>)(() => RefreshAsync(true, shutdown.Token)));
        }

        public UserControl GetSettingsControl()
        {
            if (settingsControl == null) settingsControl = new SettingsControl(this);
            return settingsControl;
        }

        public void EnterSleep() { sleeping = true; }
        public void ExitSleep() { sleeping = false; RenderAndPublish(); }

        public void Dispose()
        {
            shutdown.Cancel();
            try { if (workerTask != null) workerTask.Wait(1500); } catch { }
            lock (bitmapLock)
            {
                if (currentBitmap != null) currentBitmap.Dispose();
                currentBitmap = null;
            }
            client.Dispose();
            refreshGate.Dispose();
            shutdown.Dispose();
        }

        internal async Task<List<WeatherDevice>> TestCredentialsAsync(string apiKey, string applicationKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(applicationKey))
                throw new InvalidOperationException("Both API keys are required.");
            return await client.GetDevicesAsync(apiKey.Trim(), applicationKey.Trim(), shutdown.Token);
        }

        internal void ApplySettings(string apiKey, string applicationKey, string macAddress, int refreshSeconds, DisplayUnits units)
        {
            ApiKey = (apiKey ?? string.Empty).Trim();
            ApplicationKey = (applicationKey ?? string.Empty).Trim();
            SelectedMacAddress = macAddress ?? string.Empty;
            RefreshSeconds = Math.Max(30, Math.Min(900, refreshSeconds));
            Units = units;
            State.IsConfigured = HasCredentials;
            SaveSettings();
            Task.Run((Func<Task>)(() => RefreshAsync(true, shutdown.Token)));
        }

        internal List<WeatherDevice> CurrentDevices = new List<WeatherDevice>();

        private async Task WorkerAsync()
        {
            while (!shutdown.IsCancellationRequested)
            {
                if (!sleeping && HasCredentials) await RefreshAsync(false, shutdown.Token).ConfigureAwait(false);
                try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(30, RefreshSeconds)), shutdown.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task RefreshAsync(bool forceHistory, CancellationToken token)
        {
            if (!HasCredentials || !await refreshGate.WaitAsync(0, token).ConfigureAwait(false)) return;
            try
            {
                State.IsLoading = State.Current == null;
                RenderAndPublish();
                List<WeatherDevice> devices = await client.GetDevicesAsync(ApiKey, ApplicationKey, token).ConfigureAwait(false);
                if (devices.Count == 0) throw new InvalidOperationException("No weather stations were returned for this account.");
                CurrentDevices = devices;
                WeatherDevice selected = devices.FirstOrDefault(delegate(WeatherDevice d) { return string.Equals(d.MacAddress, SelectedMacAddress, StringComparison.OrdinalIgnoreCase); }) ?? devices[0];
                if (string.IsNullOrEmpty(SelectedMacAddress))
                {
                    SelectedMacAddress = selected.MacAddress;
                    SaveSettings();
                }

                lock (stateLock)
                {
                    State.StationName = selected.Name;
                    State.StationLocation = selected.Location;
                    State.Current = selected.LastData;
                    State.LastSuccessfulRefresh = DateTime.Now;
                    State.Error = null;
                    State.IsLoading = false;
                    State.IsConfigured = true;
                }

                if (forceHistory || DateTime.UtcNow - lastHistoryRefresh >= TimeSpan.FromMinutes(15))
                {
                    List<WeatherReading> history = await client.GetHistoryAsync(selected.MacAddress, ApiKey, ApplicationKey, token).ConfigureAwait(false);
                    lock (stateLock) State.History = history;
                    lastHistoryRefresh = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                lock (stateLock)
                {
                    State.Error = SafeError(ex);
                    State.IsLoading = false;
                }
                WriteLog(LogLevel.WARN, "Weather refresh failed", State.Error);
            }
            finally
            {
                RenderAndPublish();
                refreshGate.Release();
            }
        }

        private void RenderAndPublish()
        {
            if (sleeping) return;
            Bitmap next;
            lock (stateLock) next = WeatherRenderer.Render(State, WidgetSize.ToSize(), Units);
            Bitmap old;
            lock (bitmapLock)
            {
                old = currentBitmap;
                currentBitmap = next;
            }
            var handler = WidgetUpdated;
            if (handler != null)
            {
                handler(this, new WidgetUpdatedEventArgs { WidgetBitmap = next, WaitMax = 1000 });
            }
            if (old != null) old.Dispose();
        }

        private void SaveSettings()
        {
            IWidgetManager manager = WidgetObject.WidgetManager;
            if (manager == null) return;
            manager.StoreSetting(this, "ApiKeyProtected", SecretProtector.Protect(ApiKey), false);
            manager.StoreSetting(this, "ApplicationKeyProtected", SecretProtector.Protect(ApplicationKey), false);
            manager.StoreSetting(this, "SelectedStation", SecretProtector.Protect(SelectedMacAddress), false);
            manager.StoreSetting(this, "RefreshSeconds", RefreshSeconds.ToString(), true);
            manager.StoreSetting(this, "Units", Units.ToString(), true);
        }

        private void LoadSettings()
        {
            IWidgetManager manager = WidgetObject.WidgetManager;
            if (manager == null) return;
            string value;
            if (manager.LoadSetting(this, "ApiKeyProtected", out value)) ApiKey = SecretProtector.Unprotect(value);
            if (manager.LoadSetting(this, "ApplicationKeyProtected", out value)) ApplicationKey = SecretProtector.Unprotect(value);
            if (manager.LoadSetting(this, "SelectedStation", out value)) SelectedMacAddress = SecretProtector.Unprotect(value);
            int seconds;
            if (manager.LoadSetting(this, "RefreshSeconds", out value) && int.TryParse(value, out seconds)) RefreshSeconds = Math.Max(30, Math.Min(900, seconds));
            DisplayUnits parsed;
            if (manager.LoadSetting(this, "Units", out value) && Enum.TryParse(value, out parsed)) Units = parsed;
        }

        private void WriteLog(LogLevel level, string message, string detail)
        {
            IWidgetManager manager = WidgetObject.WidgetManager;
            if (manager != null) manager.WriteLogMessage(this, level, message, detail ?? string.Empty);
        }

        private string SafeError(Exception ex)
        {
            string message = ex == null ? "Unknown connection error." : ex.Message;
            if (!string.IsNullOrEmpty(ApiKey)) message = message.Replace(ApiKey, "[redacted]");
            if (!string.IsNullOrEmpty(ApplicationKey)) message = message.Replace(ApplicationKey, "[redacted]");
            if (!string.IsNullOrEmpty(SelectedMacAddress)) message = message.Replace(SelectedMacAddress, "[station]");
            return message;
        }
    }
}
