using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WigiDashWidgetFramework;
using WigiDashWidgetFramework.WidgetUtility;

namespace AmbientWeatherWigiDash
{
    internal static class LifecycleTestProgram
    {
        private const string OldApi = "synthetic-old-api";
        private const string OldApp = "synthetic-old-application";
        private const string NewApi = "synthetic-new-api";
        private const string NewApp = "synthetic-new-application";
        private const string OldStation = "02:00:00:00:00:41";
        private const string NewStation = "02:00:00:00:00:42";
        private const string UnsafeDetail = "synthetic-old-api synthetic-new-application 02:00:00:00:00:41";
        private static int passed;

        private static int Main()
        {
            try
            {
                Test("Rapid refresh requests never overlap or queue duplicate requests", RapidRequests);
                Test("Dispose defers resource cleanup until a cancellation-ignoring refresh ends", LateRefresh);
                Test("Disposal during station history prevents late history publication", LateHistory);
                Test("Repeated concurrent disposal cleans resources exactly once", ConcurrentDispose);
                Test("Connection testing participates in shutdown ownership", CredentialTestOwnership);
                Test("Background worker request participates in shutdown ownership", WorkerOwnership);
                Test("Disposed widgets reject refresh and settings work", NoWorkAfterDispose);
                Test("Settings changes discard stale data and refresh a coherent configuration", SettingsDuringRefresh);
                Test("Reentrant shutdown during settings persistence stops subsequent writes", DisposeDuringSave);
                Test("Reentrant shutdown while loading settings defers constructor cleanup", DisposeDuringLoad);
                Test("Reentrant Dispose from a widget callback does not destroy its bitmap", ReentrantDispose);
                Test("Dispose does not wait for an admitted host callback", BlockedCallback);
                Test("Throwing widget callbacks release refresh resources and keep errors safe", ThrowingCallback);
                Test("Rendering failures do not strand the refresh gate", ThrowingRenderer);
                Test("Successful refresh still loads current readings and history", SuccessfulRefresh);
                Test("Cancellation callback reentrancy cannot trigger premature cleanup", ReentrantCancellation);
                Test("Concurrent lifetime shutdown closes admission and cleans once", LifetimeConcurrency);
                Test("Cancellation and cleanup failures cannot create unsafe task faults", CleanupFailures);
                Console.WriteLine("Lifecycle regression checks passed: " + passed + " scenarios.");
                return 0;
            }
            catch
            {
                // Tests inject sensitive-looking failures. Never print exception text.
                return 1;
            }
            finally { WeatherRenderer.BeforeRender = null; }
        }

        private static void Test(string name, Action action)
        {
            try { action(); }
            catch { Console.Error.WriteLine("FAIL: " + name); throw; }
            passed++;
            Console.WriteLine("PASS: " + name);
        }

        private static void RapidRequests()
        {
            using (var held = new HeldResponse())
            using (var fixture = new Fixture(held.Send))
            {
                Task first = fixture.Widget.RequestRefreshAsync();
                Await(held.Entered.Task);
                var requests = new List<Task>();
                for (int i = 0; i < 50; i++) requests.Add(fixture.Widget.RequestRefreshAsync());
                Await(Task.WhenAll(requests));
                Check(fixture.Handler.Requests == 1 && fixture.Handler.MaximumActive == 1);
                for (int i = 0; i < 20; i++) fixture.Widget.ClickEvent(ClickType.Single, 0, 0);
                fixture.Widget.Dispose();
                held.Reply(Devices(OldStation, 71));
                Await(first);
                Await(fixture.Widget.ShutdownCompletion);
                Check(fixture.Handler.Requests == 1 && !fixture.Handler.DisposedWhileActive);
            }
        }

        private static void LateRefresh()
        {
            using (var held = new HeldResponse())
            using (var fixture = new Fixture(held.Send))
            {
                int published = 0;
                fixture.Widget.WidgetUpdated += delegate { Interlocked.Increment(ref published); };
                Task refresh = fixture.Widget.RequestRefreshAsync();
                Await(held.Entered.Task);
                int beforePublish = published;
                int beforeStore = fixture.Manager.Stores;
                int beforeLog = fixture.Manager.Logs;
                Await(Task.Run((Action)fixture.Widget.Dispose));
                Check(!fixture.Widget.ShutdownCompletion.IsCompleted);
                Check(fixture.Handler.Disposals == 0 && held.Token.IsCancellationRequested);
                held.Reply(Devices(OldStation, 71));
                Await(refresh);
                Await(fixture.Widget.ShutdownCompletion);
                Check(fixture.Handler.Disposals == 1 && !fixture.Handler.DisposedWhileActive);
                Check(published == beforePublish && fixture.Manager.Stores == beforeStore && fixture.Manager.Logs == beforeLog);
                Check(fixture.Widget.State.Current == null);
            }
        }

        private static void ConcurrentDispose()
        {
            using (var held = new HeldResponse())
            using (var fixture = new Fixture(held.Send))
            {
                Task refresh = fixture.Widget.RequestRefreshAsync();
                Await(held.Entered.Task);
                var disposals = new List<Task>();
                for (int i = 0; i < 20; i++) disposals.Add(Task.Run((Action)fixture.Widget.Dispose));
                Await(Task.WhenAll(disposals));
                Check(fixture.Handler.Disposals == 0);
                held.Fail(new HttpRequestException(UnsafeDetail));
                Await(refresh);
                Await(fixture.Widget.ShutdownCompletion);
                fixture.Widget.Dispose();
                Check(fixture.Handler.Disposals == 1 && !fixture.Handler.DisposedWhileActive);
                fixture.Manager.AssertSafeLogs();
            }
        }

        private static void LateHistory()
        {
            using (var held = new HeldResponse())
            using (var fixture = new Fixture(delegate(HttpRequestMessage message, CancellationToken token)
            {
                if (message.RequestUri.AbsolutePath.EndsWith("/devices", StringComparison.Ordinal))
                    return Reply(Devices(OldStation, 71));
                return held.Send(message, token);
            }))
            {
                int published = 0;
                fixture.Widget.WidgetUpdated += delegate { Interlocked.Increment(ref published); };
                Task refresh = fixture.Widget.RequestRefreshAsync();
                Await(held.Entered.Task);
                Check(fixture.Handler.Requests == 2 && fixture.Widget.State.Current != null);
                int beforePublish = published;
                fixture.Widget.Dispose();
                Check(!fixture.Widget.ShutdownCompletion.IsCompleted && fixture.Handler.Disposals == 0);
                held.Reply("[{\"tempf\":99}]");
                Await(refresh);
                Await(fixture.Widget.ShutdownCompletion);
                Check(fixture.Widget.State.History.Count == 0 && published == beforePublish);
                Check(fixture.Handler.Disposals == 1 && !fixture.Handler.DisposedWhileActive);
            }
        }

        private static void CredentialTestOwnership()
        {
            using (var held = new HeldResponse())
            using (var fixture = new Fixture(held.Send, false))
            {
                Task<List<WeatherDevice>> request = fixture.Widget.TestCredentialsAsync(NewApi, NewApp);
                Await(held.Entered.Task);
                fixture.Widget.Dispose();
                Check(!fixture.Widget.ShutdownCompletion.IsCompleted && fixture.Handler.Disposals == 0);
                held.Reply(Devices(NewStation, 82));
                AwaitCancellation(request);
                Await(fixture.Widget.ShutdownCompletion);
                Check(fixture.Handler.Disposals == 1 && !fixture.Handler.DisposedWhileActive);
            }
        }

        private static void WorkerOwnership()
        {
            using (var held = new HeldResponse())
            using (var fixture = new Fixture(held.Send, true, true))
            {
                Await(held.Entered.Task);
                fixture.Widget.Dispose();
                Check(!fixture.Widget.ShutdownCompletion.IsCompleted && fixture.Handler.Disposals == 0);
                held.Fail(new OperationCanceledException(UnsafeDetail));
                Await(fixture.Widget.ShutdownCompletion);
                Check(fixture.Handler.Disposals == 1 && fixture.Manager.Logs == 0);
            }
        }

        private static void NoWorkAfterDispose()
        {
            using (var fixture = new Fixture(SuccessResponse))
            {
                fixture.Widget.Dispose();
                Await(fixture.Widget.ShutdownCompletion);
                int renders = WeatherRenderer.Calls;
                int stores = fixture.Manager.Stores;
                fixture.Widget.ClickEvent(ClickType.Single, 0, 0);
                fixture.Widget.RequestUpdate();
                fixture.Widget.EnterSleep();
                fixture.Widget.ExitSleep();
                fixture.Widget.ApplySettings(NewApi, NewApp, NewStation, 60, DisplayUnits.Metric);
                Await(fixture.Widget.RequestRefreshAsync());
                AwaitCancellation(fixture.Widget.TestCredentialsAsync(NewApi, NewApp));
                Check(fixture.Handler.Requests == 0 && fixture.Manager.Stores == stores && WeatherRenderer.Calls == renders);
                Check(fixture.Handler.Disposals == 1);
            }
        }

        private static void SettingsDuringRefresh()
        {
            using (var held = new HeldResponse())
            {
                int request = 0;
                bool mixedKeys = false;
                var latestPublished = Signal();
                using (var fixture = new Fixture(delegate(HttpRequestMessage message, CancellationToken token)
                {
                    string query = Uri.UnescapeDataString(message.RequestUri.Query);
                    if (Interlocked.Increment(ref request) == 1)
                    {
                        Check(query.Contains("apiKey=" + OldApi) && query.Contains("applicationKey=" + OldApp));
                        return held.Send(message, token);
                    }
                    if (!query.Contains("apiKey=" + NewApi) || !query.Contains("applicationKey=" + NewApp)) mixedKeys = true;
                    string path = Uri.UnescapeDataString(message.RequestUri.AbsolutePath);
                    if (path.EndsWith("/devices", StringComparison.Ordinal)) return Reply(Devices(NewStation, 82));
                    if (!path.EndsWith("/" + NewStation, StringComparison.Ordinal)) mixedKeys = true;
                    return Reply("[{\"tempf\":81}]");
                }))
                {
                    bool stalePublished = false;
                    fixture.Widget.WidgetUpdated += delegate
                    {
                        WeatherReading reading = fixture.Widget.State.Current;
                        if (reading != null && reading.Number("tempf") == 71) stalePublished = true;
                        if (reading != null && reading.Number("tempf") == 82 && fixture.Widget.State.History.Count > 0)
                            latestPublished.TrySetResult(true);
                    };
                    Task first = fixture.Widget.RequestRefreshAsync();
                    Await(held.Entered.Task);
                    fixture.Widget.ApplySettings(NewApi, NewApp, NewStation, 60, DisplayUnits.Metric);
                    held.Reply(Devices(OldStation, 71));
                    Await(first);
                    Await(latestPublished.Task);
                    Check(!mixedKeys && !stalePublished);
                    Check(fixture.Widget.SelectedMacAddress == NewStation && fixture.Widget.State.Current.Number("tempf") == 82);
                    Check(fixture.Widget.State.History[0].Number("tempf") == 81);
                    fixture.Manager.AssertSafeLogs();
                }
            }
        }

        private static void ReentrantDispose()
        {
            using (var fixture = new Fixture(SuccessResponse))
            {
                bool ran = false;
                bool completedCallback = false;
                fixture.Widget.WidgetUpdated += delegate(object sender, WidgetUpdatedEventArgs args)
                {
                    ran = true;
                    Check(args.WidgetBitmap.Width == 2);
                    fixture.Widget.Dispose();
                    Check(!fixture.Widget.ShutdownCompletion.IsCompleted && args.WidgetBitmap.Width == 2);
                    completedCallback = true;
                };
                fixture.Widget.RequestUpdate();
                Await(fixture.Widget.ShutdownCompletion);
                Check(ran && completedCallback && fixture.Handler.Disposals == 1);
            }
        }

        private static void DisposeDuringSave()
        {
            using (var fixture = new Fixture(SuccessResponse))
            {
                bool keptAlive = false;
                bool completedCallback = false;
                fixture.Manager.StoreHook = delegate
                {
                    fixture.Widget.Dispose();
                    keptAlive = !fixture.Widget.ShutdownCompletion.IsCompleted && fixture.Handler.Disposals == 0;
                    Check(keptAlive);
                    completedCallback = true;
                };
                fixture.Widget.ApplySettings(NewApi, NewApp, NewStation, 60, DisplayUnits.Metric);
                Await(fixture.Widget.ShutdownCompletion);
                Check(keptAlive && completedCallback && fixture.Manager.Stores == 1 && fixture.Handler.Requests == 0 && fixture.Handler.Disposals == 1);
            }
        }

        private static void DisposeDuringLoad()
        {
            bool completedCallback = false;
            int renders = WeatherRenderer.Calls;
            using (var fixture = new Fixture(SuccessResponse, true, true, delegate(IWidgetInstance instance)
            {
                var widget = (AmbientWeatherWidgetInstance)instance;
                widget.Dispose();
                Check(!widget.ShutdownCompletion.IsCompleted);
                completedCallback = true;
            }))
            {
                Await(fixture.Widget.ShutdownCompletion);
                Check(completedCallback && fixture.Manager.Loads == 1 && fixture.Handler.Disposals == 1);
                Check(fixture.Handler.Requests == 0 && WeatherRenderer.Calls == renders);
            }
        }

        private static void BlockedCallback()
        {
            var entered = Signal();
            var release = Signal();
            bool completedCallback = false;
            using (var fixture = new Fixture(SuccessResponse))
            {
                fixture.Widget.WidgetUpdated += delegate(object sender, WidgetUpdatedEventArgs args)
                {
                    entered.TrySetResult(true);
                    Await(release.Task);
                    Check(args.WidgetBitmap.Width == 2);
                    completedCallback = true;
                };
                Task publish = Task.Run((Action)fixture.Widget.RequestUpdate);
                try
                {
                    Await(entered.Task);
                    Await(Task.Run((Action)fixture.Widget.Dispose));
                    Check(!fixture.Widget.ShutdownCompletion.IsCompleted && fixture.Handler.Disposals == 0);
                }
                finally { release.TrySetResult(true); }
                Await(publish);
                Await(fixture.Widget.ShutdownCompletion);
                Check(completedCallback && fixture.Handler.Disposals == 1);
            }
        }

        private static void ThrowingCallback()
        {
            using (var fixture = new Fixture(SuccessResponse))
            {
                int callbacks = 0;
                fixture.Manager.ThrowFromLog = true;
                fixture.Widget.WidgetUpdated += delegate
                {
                    if (Interlocked.Increment(ref callbacks) == 1) throw new InvalidOperationException(UnsafeDetail);
                };
                Await(fixture.Widget.RequestRefreshAsync());
                Await(fixture.Widget.RequestRefreshAsync());
                Check(fixture.Handler.Requests >= 2 && fixture.Widget.State.Current != null && fixture.Manager.Logs > 0);
                fixture.Manager.AssertSafeLogs();
            }
        }

        private static void ThrowingRenderer()
        {
            using (var fixture = new Fixture(SuccessResponse))
            {
                int calls = 0;
                WeatherRenderer.BeforeRender = delegate
                {
                    if (Interlocked.Increment(ref calls) == 1) throw new InvalidOperationException(UnsafeDetail);
                };
                try
                {
                    Await(fixture.Widget.RequestRefreshAsync());
                    Await(fixture.Widget.RequestRefreshAsync());
                    Check(fixture.Handler.Requests >= 2 && fixture.Widget.State.Current != null && fixture.Manager.Logs > 0);
                    fixture.Manager.AssertSafeLogs();
                }
                finally { WeatherRenderer.BeforeRender = null; }
            }
        }

        private static void SuccessfulRefresh()
        {
            using (var fixture = new Fixture(SuccessResponse))
            {
                int published = 0;
                fixture.Widget.WidgetUpdated += delegate { Interlocked.Increment(ref published); };
                Await(fixture.Widget.RequestRefreshAsync());
                Check(fixture.Handler.Requests == 2 && fixture.Handler.MaximumActive == 1);
                Check(fixture.Widget.State.Current.Number("tempf") == 71 && fixture.Widget.State.History.Count == 1);
                Check(fixture.Widget.CurrentDevices.Count == 1 && published >= 1 && fixture.Widget.State.Error == null);
            }
        }

        private static void ReentrantCancellation()
        {
            int cleanups = 0;
            var lifetime = new WidgetLifetime(delegate { Interlocked.Increment(ref cleanups); });
            CancellationToken token;
            Check(lifetime.TryEnter(out token));
            using (token.Register(delegate
            {
                lifetime.Exit();
                lifetime.Stop();
                Check(cleanups == 0 && !lifetime.Completion.IsCompleted);
            }))
            {
                lifetime.Stop();
                Await(lifetime.Completion);
                Check(cleanups == 1 && token.IsCancellationRequested && lifetime.CleanupError == null);
            }
        }

        private static void LifetimeConcurrency()
        {
            int cleanups = 0;
            var lifetime = new WidgetLifetime(delegate { Interlocked.Increment(ref cleanups); });
            CancellationToken token;
            Check(lifetime.TryEnter(out token));
            var requests = new List<Task>();
            for (int i = 0; i < 20; i++) requests.Add(Task.Run((Action)lifetime.Stop));
            Await(Task.WhenAll(requests));
            Check(!lifetime.TryEnter(out token) && !lifetime.Completion.IsCompleted && cleanups == 0);
            lifetime.Exit();
            Await(lifetime.Completion);
            lifetime.Stop();
            Check(cleanups == 1);
        }

        private static void CleanupFailures()
        {
            var lifetime = new WidgetLifetime(delegate { throw new InvalidOperationException(UnsafeDetail); });
            CancellationToken token;
            Check(lifetime.TryEnter(out token));
            using (token.Register(delegate { throw new InvalidOperationException(UnsafeDetail); }))
            {
                lifetime.Exit();
                lifetime.Stop();
                Await(lifetime.Completion);
                Check(lifetime.Completion.Status == TaskStatus.RanToCompletion);
                Check(!string.IsNullOrEmpty(lifetime.CleanupError));
                AssertSafe(lifetime.CleanupError);
            }
        }

        private static Task<HttpResponseMessage> SuccessResponse(HttpRequestMessage request, CancellationToken token)
        {
            return Reply(request.RequestUri.AbsolutePath.EndsWith("/devices", StringComparison.Ordinal)
                ? Devices(OldStation, 71) : "[{\"tempf\":70}]");
        }
        private static string Devices(string station, int temperature)
        {
            return "[{\"macAddress\":\"" + station + "\",\"info\":{\"name\":\"Synthetic Station\"},\"lastData\":{\"tempf\":" + temperature + "}}]";
        }
        private static HttpResponseMessage Response(string json) { return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }; }
        private static Task<HttpResponseMessage> Reply(string json) { return Task.FromResult(Response(json)); }
        private static TaskCompletionSource<bool> Signal() { return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); }
        private static void Await(Task task)
        {
            if (!task.Wait(5000)) throw new InvalidOperationException("Timed out waiting for a test barrier.");
            task.GetAwaiter().GetResult();
        }
        private static void AwaitCancellation(Task task)
        {
            try { Await(task); }
            catch (AggregateException error)
            {
                Check(error.InnerExceptions.Count == 1 && error.InnerException is OperationCanceledException);
                return;
            }
            catch (OperationCanceledException) { return; }
            throw new InvalidOperationException("Expected cancellation.");
        }
        private static void Check(bool condition) { if (!condition) throw new InvalidOperationException("Lifecycle assertion failed."); }
        private static void AssertSafe(string value)
        {
            foreach (string secret in new[] { OldApi, OldApp, NewApi, NewApp, OldStation, NewStation })
                Check(value == null || value.IndexOf(secret, StringComparison.OrdinalIgnoreCase) < 0);
        }

        private sealed class HeldResponse : IDisposable
        {
            internal readonly TaskCompletionSource<bool> Entered = Signal();
            private readonly TaskCompletionSource<HttpResponseMessage> response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal CancellationToken Token;
            internal Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken token)
            {
                Token = token;
                Entered.TrySetResult(true);
                return response.Task;
            }
            internal void Reply(string json) { response.TrySetResult(Response(json)); }
            internal void Fail(Exception error) { response.TrySetException(error); }
            public void Dispose() { response.TrySetCanceled(); }
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly TrackingHandler Handler;
            internal readonly RecordingManager Manager;
            internal readonly AmbientWeatherWidgetInstance Widget;
            internal Fixture(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send, bool configured = true, bool startWorker = false,
                Action<IWidgetInstance> loadHook = null)
            {
                WeatherRenderer.BeforeRender = null;
                Manager = new RecordingManager(configured);
                Manager.LoadHook = loadHook;
                Handler = new TrackingHandler(send);
                Widget = new AmbientWeatherWidgetInstance(new TestWidgetObject(Manager), new WidgetSize(1, 1), Guid.NewGuid(), new AmbientWeatherClient(Handler), startWorker);
            }
            public void Dispose()
            {
                WeatherRenderer.BeforeRender = null;
                Widget.Dispose();
                Await(Widget.ShutdownCompletion);
            }
        }

        private sealed class TrackingHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;
            internal int Requests;
            internal int MaximumActive;
            internal int Disposals;
            internal bool DisposedWhileActive;
            private int active;
            internal TrackingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) { this.send = send; }
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                Interlocked.Increment(ref Requests);
                int current = Interlocked.Increment(ref active);
                int prior;
                do { prior = MaximumActive; if (prior >= current) break; }
                while (Interlocked.CompareExchange(ref MaximumActive, current, prior) != prior);
                try { return await send(request, token).ConfigureAwait(false); }
                finally { Interlocked.Decrement(ref active); }
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (Volatile.Read(ref active) != 0) DisposedWhileActive = true;
                    Interlocked.Increment(ref Disposals);
                }
                base.Dispose(disposing);
            }
        }

        private sealed class TestWidgetObject : IWidgetObject
        {
            public IWidgetManager WidgetManager { get; private set; }
            internal TestWidgetObject(IWidgetManager manager) { WidgetManager = manager; }
        }

        private sealed class RecordingManager : IWidgetManager
        {
            private readonly Dictionary<string, string> settings = new Dictionary<string, string>();
            private readonly List<string> logDetails = new List<string>();
            internal int Stores;
            internal int Loads;
            internal int Logs;
            internal Action StoreHook;
            internal Action<IWidgetInstance> LoadHook;
            internal bool ThrowFromLog;
            internal RecordingManager(bool configured)
            {
                if (configured)
                {
                    settings["ApiKeyProtected"] = OldApi;
                    settings["ApplicationKeyProtected"] = OldApp;
                    settings["SelectedStation"] = OldStation;
                }
            }
            public bool LoadSetting(IWidgetInstance instance, string key, out string value)
            {
                Interlocked.Increment(ref Loads);
                Action<IWidgetInstance> hook = LoadHook;
                if (hook != null) hook(instance);
                lock (settings) return settings.TryGetValue(key, out value);
            }
            public void StoreSetting(IWidgetInstance instance, string key, string value, bool plainText)
            {
                lock (settings) settings[key] = value;
                Interlocked.Increment(ref Stores);
                Action hook = StoreHook;
                if (hook != null) hook();
            }
            public void WriteLogMessage(IWidgetInstance instance, LogLevel level, string message, string detail)
            {
                lock (logDetails) { logDetails.Add(message); logDetails.Add(detail); }
                Interlocked.Increment(ref Logs);
                if (ThrowFromLog) throw new InvalidOperationException(UnsafeDetail);
            }
            internal void AssertSafeLogs() { lock (logDetails) foreach (string value in logDetails) AssertSafe(value); }
        }
    }
}
