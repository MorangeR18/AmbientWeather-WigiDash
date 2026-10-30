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
        private readonly object stateLock = new object();
        private readonly AmbientWeatherClient client;
        private readonly SemaphoreSlim refreshGate = new SemaphoreSlim(1, 1);
        private readonly WidgetLifetime lifetime;
        private Bitmap currentBitmap;
        private int rendering;
        private int saving;
        private volatile bool sleeping;
        private DateTime lastHistoryRefresh = DateTime.MinValue;
        private SettingsControl settingsControl;
        private long settingsVersion;
        private bool pendingSettingsRefresh;
        private bool pendingSave;

        internal string ApiKey = string.Empty;
        internal string ApplicationKey = string.Empty;
        internal string SelectedMacAddress = string.Empty;
        internal int RefreshSeconds = 60;
        internal DisplayUnits Units = DisplayUnits.Imperial;
        internal WeatherState State = new WeatherState();
        internal List<WeatherDevice> CurrentDevices = new List<WeatherDevice>();
        internal Task ShutdownCompletion { get { return lifetime.Completion; } }

        public IWidgetObject WidgetObject { get; private set; }
        public Guid Guid { get; private set; }
        public WidgetSize WidgetSize { get; private set; }
        public event WidgetUpdatedEventHandler WidgetUpdated;

        public AmbientWeatherWidgetInstance(IWidgetObject widgetObject, WidgetSize widgetSize, Guid instanceGuid)
            : this(widgetObject, widgetSize, instanceGuid, new AmbientWeatherClient(), true) { }

        internal AmbientWeatherWidgetInstance(IWidgetObject widgetObject, WidgetSize widgetSize, Guid instanceGuid,
            AmbientWeatherClient weatherClient, bool startWorker)
        {
            WidgetObject = widgetObject;
            WidgetSize = widgetSize;
            Guid = instanceGuid;
            client = weatherClient;
            lifetime = new WidgetLifetime(CleanupResources);
            CancellationToken token;
            lifetime.TryEnter(out token);
            try
            {
                LoadSettings();
                if (lifetime.IsStopping) return;
                State.IsConfigured = HasCredentials;
                RenderAndPublish();
                if (startWorker) StartOperation(WorkerAsync);
            }
            catch
            {
                lifetime.Stop();
                throw;
            }
            finally { lifetime.Exit(); }
        }

        internal bool HasCredentials
        {
            get { lock (stateLock) return !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApplicationKey); }
        }

        public void RequestUpdate() { RenderAndPublish(); }

        public void ClickEvent(ClickType clickType, int x, int y)
        {
            if (clickType == ClickType.Single || clickType == ClickType.Double) QueueRefresh();
        }

        public UserControl GetSettingsControl()
        {
            CancellationToken token;
            if (!lifetime.TryEnter(out token)) return settingsControl;
            try
            {
                if (settingsControl == null) settingsControl = new SettingsControl(this);
                return settingsControl;
            }
            finally { lifetime.Exit(); }
        }

        public void EnterSleep() { sleeping = true; }
        public void ExitSleep() { sleeping = false; RenderAndPublish(); }

        public void Dispose()
        {
            // Never wait on the host UI thread. A publication may currently be
            // calling back into that thread. The final operation owns cleanup.
            lifetime.Stop();
        }

        private void CleanupResources()
        {
            try
            {
                if (currentBitmap != null) currentBitmap.Dispose();
                currentBitmap = null;
            }
            finally
            {
                try { client.Dispose(); }
                finally { refreshGate.Dispose(); }
            }
        }

        internal async Task<List<WeatherDevice>> TestCredentialsAsync(string apiKey, string applicationKey)
        {
            CancellationToken token;
            if (!lifetime.TryEnter(out token)) throw new OperationCanceledException("The widget is shutting down.");
            try
            {
                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(applicationKey))
                    throw new ConnectionException(ConnectionErrorKind.CredentialsRequired);
                var devices = await client.GetDevicesAsync(apiKey.Trim(), applicationKey.Trim(), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                return devices;
            }
            finally { lifetime.Exit(); }
        }

        internal void ApplySettings(string apiKey, string applicationKey, string macAddress, int refreshSeconds, DisplayUnits units)
        {
            CancellationToken token;
            if (!lifetime.TryEnter(out token)) return;
            try
            {
                lock (stateLock)
                {
                    if (lifetime.IsStopping) return;
                    ApiKey = (apiKey ?? string.Empty).Trim();
                    ApplicationKey = (applicationKey ?? string.Empty).Trim();
                    SelectedMacAddress = macAddress ?? string.Empty;
                    RefreshSeconds = Math.Max(30, Math.Min(900, refreshSeconds));
                    Units = units;
                    settingsVersion++;
                    pendingSettingsRefresh = true;
                    lastHistoryRefresh = DateTime.MinValue;
                    State.IsConfigured = HasCredentials;
                }
                SaveSettings();
                QueueRefresh();
            }
            catch (Exception error) { RecordFailure(error, null); }
            finally { lifetime.Exit(); }
        }

        internal Task RequestRefreshAsync()
        {
            return StartOperation(delegate(CancellationToken token) { return RefreshAsync(true, token); });
        }

        private void QueueRefresh()
        {
            // StartOperation owns the lifetime and observes the returned task's errors.
            RequestRefreshAsync();
        }

        private Task StartOperation(Func<CancellationToken, Task> operation)
        {
            CancellationToken token;
            if (!lifetime.TryEnter(out token)) return Task.FromResult(0);
            try
            {
                // Admission happens before queueing: even a task that has not yet
                // started keeps its resources alive. All detached faults are observed.
                return Task.Run(async delegate
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        await operation(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception error) { RecordFailure(error, null); }
                    finally { lifetime.Exit(); }
                });
            }
            catch
            {
                lifetime.Exit();
                throw;
            }
        }

        private async Task WorkerAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!sleeping && HasCredentials) await RefreshAsync(false, token).ConfigureAwait(false);
                int seconds;
                lock (stateLock) seconds = Math.Max(30, RefreshSeconds);
                await Task.Delay(TimeSpan.FromSeconds(seconds), token).ConfigureAwait(false);
            }
        }

        private async Task RefreshAsync(bool forceHistory, CancellationToken token)
        {
            bool entered = false;
            RefreshSettings settings = null;
            try
            {
                entered = await refreshGate.WaitAsync(0, token).ConfigureAwait(false);
                if (!entered) return;
                lock (stateLock)
                {
                    settings = CaptureSettings();
                    pendingSettingsRefresh = false;
                    if (!HasCredentials || lifetime.IsStopping) return;
                    State.IsLoading = State.Current == null;
                }
                RenderAndPublish();
                token.ThrowIfCancellationRequested();
                List<WeatherDevice> devices = await client.GetDevicesAsync(settings.ApiKey, settings.ApplicationKey, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (devices.Count == 0) throw new ConnectionException(ConnectionErrorKind.NoStations);
                WeatherDevice selected = devices.FirstOrDefault(delegate(WeatherDevice d) { return string.Equals(d.MacAddress, settings.Station, StringComparison.OrdinalIgnoreCase); }) ?? devices[0];
                bool saveStation = false;
                bool refreshHistory;
                lock (stateLock)
                {
                    if (!IsCurrent(settings)) return;
                    CurrentDevices = devices;
                    if (string.IsNullOrEmpty(SelectedMacAddress))
                    {
                        SelectedMacAddress = selected.MacAddress;
                        saveStation = true;
                    }
                    State.StationName = selected.Name;
                    State.StationLocation = selected.Location;
                    State.Current = selected.LastData;
                    State.LastSuccessfulRefresh = DateTime.Now;
                    State.Error = null;
                    State.IsLoading = false;
                    State.IsConfigured = true;
                    refreshHistory = forceHistory || DateTime.UtcNow - lastHistoryRefresh >= TimeSpan.FromMinutes(15);
                }
                if (saveStation) SaveSettings();
                if (refreshHistory && IsCurrent(settings))
                {
                    // One snapshot supplies both requests, even if settings change
                    // while the device list or station history is in flight.
                    List<WeatherReading> history = await client.GetHistoryAsync(selected.MacAddress, settings.ApiKey, settings.ApplicationKey, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    lock (stateLock)
                    {
                        if (!IsCurrent(settings)) return;
                        State.History = history;
                        lastHistoryRefresh = DateTime.UtcNow;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { RecordFailure(error, settings == null ? (long?)null : settings.Version); }
            finally
            {
                if (entered)
                {
                    // Release does not depend on rendering or a host callback.
                    refreshGate.Release();
                    RenderAndPublish();
                    bool pending;
                    lock (stateLock) pending = pendingSettingsRefresh;
                    if (pending && !lifetime.IsStopping) QueueRefresh();
                }
            }
        }

        private bool IsCurrent(RefreshSettings settings)
        {
            lock (stateLock) return !lifetime.IsStopping && settings.Version == settingsVersion;
        }

        private void RecordFailure(Exception error, long? version)
        {
            string message = ConnectionErrors.GetMessage(error);
            lock (stateLock)
            {
                if (lifetime.IsStopping || (version.HasValue && version.Value != settingsVersion)) return;
                State.Error = message;
                State.IsLoading = false;
            }
            // Logging is a host callback too. Do not let a logger failure create
            // an unobserved task or include its arbitrary exception text in logs.
            try
            {
                IWidgetManager manager = WidgetObject.WidgetManager;
                if (manager != null && !lifetime.IsStopping)
                    manager.WriteLogMessage(this, LogLevel.WARN, "Weather refresh failed", message);
            }
            catch { }
        }

        private void RenderAndPublish()
        {
            CancellationToken token;
            if (!lifetime.TryEnter(out token)) return;
            bool entered = false;
            try
            {
                // Coalesce overlapping/reentrant publications. Never block a host
                // UI callback on a lock held by another publication.
                entered = Interlocked.CompareExchange(ref rendering, 1, 0) == 0;
                if (!entered || sleeping || lifetime.IsStopping) return;
                Bitmap next;
                lock (stateLock) next = WeatherRenderer.Render(State, WidgetSize.ToSize(), Units);
                if (lifetime.IsStopping) { next.Dispose(); return; }
                Bitmap old = currentBitmap;
                currentBitmap = next;
                try
                {
                    var handler = WidgetUpdated;
                    if (handler != null && !lifetime.IsStopping)
                        handler(this, new WidgetUpdatedEventArgs { WidgetBitmap = next, WaitMax = 1000 });
                }
                finally { if (old != null) old.Dispose(); }
            }
            catch (Exception error) { RecordFailure(error, null); }
            finally
            {
                if (entered) Interlocked.Exchange(ref rendering, 0);
                lifetime.Exit();
            }
        }

        private sealed class RefreshSettings
        {
            internal string ApiKey, ApplicationKey, Station;
            internal int RefreshSeconds;
            internal DisplayUnits Units;
            internal long Version;
        }

        // Caller holds stateLock so an operation cannot mix old and new keys.
        private RefreshSettings CaptureSettings()
        {
            return new RefreshSettings { ApiKey = ApiKey, ApplicationKey = ApplicationKey, Station = SelectedMacAddress,
                RefreshSeconds = RefreshSeconds, Units = Units, Version = settingsVersion };
        }

        private void SaveSettings()
        {
            lock (stateLock) pendingSave = true;
            // Serialize persistence without holding stateLock during host calls.
            // Concurrent/reentrant saves coalesce to the latest settings snapshot.
            while (!lifetime.IsStopping && Interlocked.CompareExchange(ref saving, 1, 0) == 0)
            {
                try
                {
                    RefreshSettings settings;
                    lock (stateLock) { settings = CaptureSettings(); pendingSave = false; }
                    IWidgetManager manager = WidgetObject.WidgetManager;
                    if (manager == null) return;
                    if (lifetime.IsStopping) return;
                    manager.StoreSetting(this, "ApiKeyProtected", SecretProtector.Protect(settings.ApiKey), false);
                    if (lifetime.IsStopping) return;
                    manager.StoreSetting(this, "ApplicationKeyProtected", SecretProtector.Protect(settings.ApplicationKey), false);
                    if (lifetime.IsStopping) return;
                    manager.StoreSetting(this, "SelectedStation", SecretProtector.Protect(settings.Station), false);
                    if (lifetime.IsStopping) return;
                    manager.StoreSetting(this, "RefreshSeconds", settings.RefreshSeconds.ToString(), true);
                    if (lifetime.IsStopping) return;
                    manager.StoreSetting(this, "Units", settings.Units.ToString(), true);
                }
                finally { Interlocked.Exchange(ref saving, 0); }
                lock (stateLock) if (!pendingSave) return;
            }
        }

        private void LoadSettings()
        {
            IWidgetManager manager = WidgetObject.WidgetManager;
            if (manager == null) return;
            string value;
            if (manager.LoadSetting(this, "ApiKeyProtected", out value)) ApiKey = SecretProtector.Unprotect(value);
            if (lifetime.IsStopping) return;
            if (manager.LoadSetting(this, "ApplicationKeyProtected", out value)) ApplicationKey = SecretProtector.Unprotect(value);
            if (lifetime.IsStopping) return;
            if (manager.LoadSetting(this, "SelectedStation", out value)) SelectedMacAddress = SecretProtector.Unprotect(value);
            if (lifetime.IsStopping) return;
            int seconds;
            if (manager.LoadSetting(this, "RefreshSeconds", out value) && int.TryParse(value, out seconds)) RefreshSeconds = Math.Max(30, Math.Min(900, seconds));
            if (lifetime.IsStopping) return;
            DisplayUnits parsed;
            if (manager.LoadSetting(this, "Units", out value) && Enum.TryParse(value, out parsed)) Units = parsed;
        }
    }
}
