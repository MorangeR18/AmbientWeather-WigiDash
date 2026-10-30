using System;
using System.Globalization;
using System.Net;
using System.Net.Http;

namespace AmbientWeatherWigiDash
{
    internal enum ConnectionErrorKind
    {
        Unknown, CredentialsRequired, NoStations, RejectedCredentials,
        RateLimit, Http, Network, Timeout, InvalidResponse
    }

    // Accept only a category and numeric status, never request/response text or an
    // inner exception. Even logging this exception must not disclose request data.
    internal sealed class ConnectionException : Exception
    {
        internal readonly ConnectionErrorKind Kind;
        internal readonly int StatusCode;

        internal ConnectionException(ConnectionErrorKind kind, int statusCode = 0)
            : base(ConnectionErrors.GetMessage(kind, statusCode))
        {
            Kind = kind;
            StatusCode = statusCode;
        }
    }

    internal static class ConnectionErrors
    {
        internal static string GetMessage(Exception error)
        {
            return ToSafeException(error).Message;
        }

        internal static ConnectionException ToSafeException(Exception error)
        {
            var known = error as ConnectionException;
            if (known != null) return new ConnectionException(known.Kind, known.StatusCode);
            if (error is OperationCanceledException) return new ConnectionException(ConnectionErrorKind.Timeout);
            if (error is HttpRequestException || error is WebException)
                return new ConnectionException(ConnectionErrorKind.Network);
            return new ConnectionException(ConnectionErrorKind.Unknown);
        }

        internal static string GetMessage(ConnectionErrorKind kind, int statusCode)
        {
            // Never inspect Exception.Message, response bodies, URLs, or saved
            // credentials. This also protects unsaved keys edited during a test.
            switch (kind)
            {
                case ConnectionErrorKind.CredentialsRequired:
                    return "Both API keys are required.";
                case ConnectionErrorKind.NoStations:
                    return "No weather stations were returned for this account.";
                case ConnectionErrorKind.RejectedCredentials:
                    return "Ambient Weather rejected the API credentials. Check both keys and try again.";
                case ConnectionErrorKind.RateLimit:
                    return "Ambient Weather rate limit reached. Wait a moment before trying again; background refresh will retry automatically.";
                case ConnectionErrorKind.Http:
                    if (statusCode >= 100 && statusCode <= 599)
                        return "Ambient Weather returned HTTP " + statusCode.ToString(CultureInfo.InvariantCulture) + ". Try again later.";
                    break;
                case ConnectionErrorKind.Network:
                    return "Could not connect to Ambient Weather. Check your internet connection and try again.";
                case ConnectionErrorKind.Timeout:
                    return "The Ambient Weather request timed out or was canceled. Try again.";
                case ConnectionErrorKind.InvalidResponse:
                    return "Ambient Weather returned an unreadable response. Try again later.";
            }
            return "Could not complete the Ambient Weather request. Try again; if the problem persists, check your connection and settings.";
        }
    }
}
