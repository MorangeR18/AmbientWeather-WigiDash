using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientWeatherWigiDash
{
    // All credentials, identifiers, responses and transport failures are synthetic.
    // Failure reporting intentionally excludes exception text and fixture values.
    internal static class ConnectionTestProgram
    {
        private const string ApiKey = "unsaved api+SYNTHETIC/41?x=7";
        private const string ApplicationKey = "unsaved app+SYNTHETIC/93?x=4";
        private const string Station = "DE:AD:BE:EF:00:21";
        private static readonly string[] Secrets = {
            ApiKey, ApplicationKey, Station,
            "saved-api-SYNTHETIC-7b2", "saved-app-SYNTHETIC-9c4", "AB:CD:EF:12:34:56"
        };
        private static int passed;
        private static int failed;

        private static int Main()
        {
            Test("Raw, encoded and alternate station values never enter error messages", delegate {
                foreach (string secret in Secrets)
                {
                    foreach (string variant in Variants(secret))
                    {
                        Exception error = new InvalidOperationException("untrusted-server-message " + variant);
                        AssertSafe(ConnectionErrors.GetMessage(error));
                        AssertSafeException(ConnectionErrors.ToSafeException(error));
                    }
                }
            });
            Test("Nested exceptions and exception data are excluded", delegate {
                Exception error = new AggregateException("untrusted-server-message " + Payload(),
                    new HttpRequestException(Payload(), new InvalidOperationException(Payload())));
                error.Data["request"] = Payload();
                AssertSafe(ConnectionErrors.GetMessage(error));
                AssertSafeException(ConnectionErrors.ToSafeException(error));
            });
            Test("Arbitrary exception messages cannot impersonate approved errors", delegate {
                string approved = ConnectionErrors.GetMessage(new ConnectionException(ConnectionErrorKind.RejectedCredentials));
                string fallback = ConnectionErrors.GetMessage(new ConnectionException(ConnectionErrorKind.Unknown));
                Assert(ConnectionErrors.GetMessage(new InvalidOperationException(approved + " " + Payload())) == fallback);
                Assert(ConnectionErrors.GetMessage(new InvalidOperationException(approved)) == fallback);
                Assert(ConnectionErrors.GetMessage(null) == fallback);
            });
            Test("Unknown exceptions are classified without reading their text", delegate {
                AssertSafe(ConnectionErrors.GetMessage(new UnreadableException()));
                AssertSafeException(ConnectionErrors.ToSafeException(new UnreadableException()));
            });
            Test("Known error metadata retains useful guidance without exception data", delegate {
                foreach (ConnectionErrorKind kind in Enum.GetValues(typeof(ConnectionErrorKind)))
                {
                    var error = new ConnectionException(kind, 503);
                    error.Data["request"] = Payload();
                    ConnectionException safe = ConnectionErrors.ToSafeException(error);
                    AssertSafeException(safe);
                    Assert(!string.IsNullOrWhiteSpace(ConnectionErrors.GetMessage(safe)));
                }
                Assert(ConnectionErrors.GetMessage(new ConnectionException(ConnectionErrorKind.CredentialsRequired)).IndexOf("required", StringComparison.OrdinalIgnoreCase) >= 0);
                Assert(ConnectionErrors.GetMessage(new ConnectionException(ConnectionErrorKind.NoStations)).IndexOf("stations", StringComparison.OrdinalIgnoreCase) >= 0);
            });
            Test("Invalid error metadata falls back to a fixed message", delegate {
                string fallback = ConnectionErrors.GetMessage(new ConnectionException(ConnectionErrorKind.Unknown));
                Assert(ConnectionErrors.GetMessage(new ConnectionException((ConnectionErrorKind)int.MaxValue)) == fallback);
                Assert(ConnectionErrors.GetMessage(new ConnectionException(ConnectionErrorKind.Http, int.MaxValue)) == fallback);
            });

            foreach (int code in new[] { 401, 403, 429, 500, 503 })
            {
                int status = code;
                Test("HTTP " + status + " omits server reason and body", delegate {
                    using (var client = Client(delegate(HttpRequestMessage request, CancellationToken token) {
                        AssertRequest(request);
                        return Completed(new HttpResponseMessage((HttpStatusCode)status) {
                            ReasonPhrase = "untrusted-server-message " + ApiKey,
                            Content = new StringContent(Payload())
                        });
                    }))
                    {
                        var error = Failure(delegate { client.GetDevicesAsync(ApiKey, ApplicationKey, CancellationToken.None).GetAwaiter().GetResult(); });
                        AssertSafeException(error);
                        string message = ConnectionErrors.GetMessage(error);
                        if (status == 401 || status == 403)
                            Assert(message.IndexOf("credentials", StringComparison.OrdinalIgnoreCase) >= 0);
                        else if (status == 429)
                            Assert(message.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0);
                        else
                            Assert(message.Contains("HTTP " + status));
                    }
                });
            }
            Test("Transport failure excludes the request URL and nested details", delegate {
                using (var client = Client(delegate(HttpRequestMessage request, CancellationToken token) {
                    AssertRequest(request);
                    return Faulted(new HttpRequestException(request.RequestUri.AbsoluteUri + " " + Payload(), new IOException(Payload())));
                }))
                {
                    var error = Failure(delegate { client.GetDevicesAsync(ApiKey, ApplicationKey, CancellationToken.None).GetAwaiter().GetResult(); });
                    AssertSafeException(error);
                    Assert(error is ConnectionException && ((ConnectionException)error).Kind == ConnectionErrorKind.Network);
                }
            });
            Test("History request failure excludes its station path and credentials", delegate {
                using (var client = Client(delegate(HttpRequestMessage request, CancellationToken token) {
                    AssertRequest(request);
                    Assert(Uri.UnescapeDataString(request.RequestUri.AbsolutePath).EndsWith("/" + Station, StringComparison.Ordinal));
                    return Faulted(new HttpRequestException(request.RequestUri.AbsoluteUri + " " + Payload()));
                }))
                {
                    var error = Failure(delegate { client.GetHistoryAsync(Station, ApiKey, ApplicationKey, CancellationToken.None).GetAwaiter().GetResult(); });
                    AssertSafeException(error);
                    Assert(error is ConnectionException && ((ConnectionException)error).Kind == ConnectionErrorKind.Network);
                }
            });
            Test("Unknown transport failure cannot pass arbitrary exception text", delegate {
                using (var client = Client(delegate(HttpRequestMessage request, CancellationToken token) {
                    return Faulted(new InvalidOperationException(Payload()));
                }))
                {
                    AssertSafeException(Failure(delegate { client.GetDevicesAsync(ApiKey, ApplicationKey, CancellationToken.None).GetAwaiter().GetResult(); }));
                }
            });
            Test("Malformed JSON cannot echo a response containing credentials", delegate {
                using (var client = Client(delegate(HttpRequestMessage request, CancellationToken token) {
                    return Completed(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"invalid\":" + Payload()) });
                }))
                {
                    var error = Failure(delegate { client.GetDevicesAsync(ApiKey, ApplicationKey, CancellationToken.None).GetAwaiter().GetResult(); });
                    AssertSafeException(error);
                    Assert(error is ConnectionException && ((ConnectionException)error).Kind == ConnectionErrorKind.InvalidResponse);
                }
            });
            Test("History JSON failure is sanitized before leaving the client", delegate {
                using (var client = Client(delegate(HttpRequestMessage request, CancellationToken token) {
                    return Completed(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"invalid\":" + Payload()) });
                }))
                {
                    AssertSafeException(Failure(delegate { client.GetHistoryAsync(Station, ApiKey, ApplicationKey, CancellationToken.None).GetAwaiter().GetResult(); }));
                }
            });
            Test("Timeout preserves guidance without canceled exception details", delegate {
                using (var client = Client(delegate(HttpRequestMessage request, CancellationToken token) {
                    return Faulted(new TaskCanceledException(Payload()));
                }))
                {
                    var error = Failure(delegate { client.GetDevicesAsync(ApiKey, ApplicationKey, CancellationToken.None).GetAwaiter().GetResult(); });
                    AssertSafeException(error);
                    Assert(error is ConnectionException && ((ConnectionException)error).Kind == ConnectionErrorKind.Timeout);
                }
            });
            Test("Caller cancellation stays cancellable without original details", delegate {
                using (var cancellation = new CancellationTokenSource())
                using (var client = Client(delegate(HttpRequestMessage request, CancellationToken token) {
                    cancellation.Cancel();
                    return Faulted(new OperationCanceledException(Payload(), new IOException(Payload()), token));
                }))
                {
                    var error = Failure(delegate { client.GetDevicesAsync(ApiKey, ApplicationKey, cancellation.Token).GetAwaiter().GetResult(); });
                    Assert(error is OperationCanceledException);
                    AssertSafeException(error);
                }
            });
            Test("Already canceled requests do not reach the transport", delegate {
                bool reached = false;
                using (var cancellation = new CancellationTokenSource())
                using (var client = Client(delegate(HttpRequestMessage request, CancellationToken token) {
                    reached = true;
                    return Faulted(new InvalidOperationException(Payload()));
                }))
                {
                    cancellation.Cancel();
                    var error = Failure(delegate { client.GetHistoryAsync(Station, ApiKey, ApplicationKey, cancellation.Token).GetAwaiter().GetResult(); });
                    Assert(error is OperationCanceledException);
                    AssertSafeException(error);
                    Assert(!reached);
                }
            });
            Test("Valid device responses still return station and observation data", delegate {
                using (var client = Client(delegate(HttpRequestMessage request, CancellationToken token) {
                    AssertRequest(request);
                    return Completed(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                        "[{\"macAddress\":\"" + Station + "\",\"info\":{\"name\":\"Synthetic Station\",\"location\":\"Synthetic Yard\"},\"lastData\":{\"tempf\":72.4}}]") });
                }))
                {
                    List<WeatherDevice> devices = client.GetDevicesAsync(ApiKey, ApplicationKey, CancellationToken.None).GetAwaiter().GetResult();
                    Assert(devices.Count == 1 && devices[0].MacAddress == Station && devices[0].Name == "Synthetic Station" && devices[0].LastData.Number("tempf") == 72.4);
                }
            });
            Test("Valid history responses retain chronological ordering", delegate {
                using (var client = Client(delegate(HttpRequestMessage request, CancellationToken token) {
                    AssertRequest(request);
                    return Completed(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[{\"tempf\":72.4},{\"tempf\":68.2}]") });
                }))
                {
                    List<WeatherReading> readings = client.GetHistoryAsync(Station, ApiKey, ApplicationKey, CancellationToken.None).GetAwaiter().GetResult();
                    Assert(readings.Count == 2 && readings[0].Number("tempf") == 68.2 && readings[1].Number("tempf") == 72.4);
                }
            });

            Console.WriteLine("Connection error regression checks: " + passed + " passed, " + failed + " failed.");
            return failed == 0 ? 0 : 1;
        }

        private static void Test(string name, Action action)
        {
            try { action(); passed++; Console.WriteLine("PASS: " + name); }
            catch { failed++; Console.Error.WriteLine("FAIL: " + name + ". Details suppressed to keep fixtures out of logs."); }
        }

        private static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed."); }
        private static Exception Failure(Action action)
        {
            try { action(); }
            catch (Exception error) { return error; }
            throw new Exception("Expected failure was not raised.");
        }
        private static void AssertSafeException(Exception error)
        {
            Assert(error.InnerException == null && error.Data.Count == 0);
            AssertSafe(error.Message);
            AssertSafe(error.ToString());
            AssertSafe(ConnectionErrors.GetMessage(error));
        }
        private static void AssertSafe(string text)
        {
            Assert(!string.IsNullOrWhiteSpace(text));
            foreach (string secret in Secrets)
                foreach (string variant in Variants(secret))
                    Assert(text.IndexOf(variant, StringComparison.OrdinalIgnoreCase) < 0);
            foreach (string marker in new[] { "untrusted-server-message", "rt.ambientweather.net", "apiKey=", "applicationKey=" })
                Assert(text.IndexOf(marker, StringComparison.OrdinalIgnoreCase) < 0);
        }
        private static IEnumerable<string> Variants(string value)
        {
            yield return value;
            yield return Uri.EscapeDataString(value);
            yield return Uri.EscapeDataString(Uri.EscapeDataString(value));
            yield return WebUtility.UrlEncode(value);
            if (value.Contains(":")) { yield return value.Replace(":", "-"); yield return value.Replace(":", string.Empty); }
        }
        private static string Payload()
        {
            var parts = new List<string>();
            foreach (string secret in Secrets) parts.AddRange(Variants(secret));
            return "untrusted-server-message " + string.Join(" | ", parts.ToArray());
        }
        private static void AssertRequest(HttpRequestMessage request)
        {
            Assert(request.RequestUri.Scheme == "https");
            Assert(request.RequestUri.Query.Contains("apiKey=" + Uri.EscapeDataString(ApiKey)));
            Assert(request.RequestUri.Query.Contains("applicationKey=" + Uri.EscapeDataString(ApplicationKey)));
        }
        private static AmbientWeatherClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
        {
            return new AmbientWeatherClient(new SyntheticHandler(callback));
        }
        private static Task<HttpResponseMessage> Completed(HttpResponseMessage response) { return Task.FromResult(response); }
        private static Task<HttpResponseMessage> Faulted(Exception error)
        {
            var completion = new TaskCompletionSource<HttpResponseMessage>();
            completion.SetException(error);
            return completion.Task;
        }
        private sealed class SyntheticHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback;
            internal SyntheticHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) { callback = action; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return callback(request, cancellationToken);
            }
        }
        private sealed class UnreadableException : Exception
        {
            public override string Message { get { throw new InvalidOperationException("Exception text must not be inspected."); } }
            public override string ToString() { throw new InvalidOperationException("Exception text must not be inspected."); }
        }
    }
}
