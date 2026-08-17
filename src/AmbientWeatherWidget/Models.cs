using System;
using System.Collections.Generic;
using System.Globalization;

namespace AmbientWeatherWigiDash
{
    internal sealed class WeatherDevice
    {
        public string MacAddress;
        public string Name;
        public string Location;
        public WeatherReading LastData;
        public string DisplayName { get { return string.IsNullOrWhiteSpace(Location) ? Name : Name + " — " + Location; } }
        public override string ToString() { return DisplayName; }
    }

    internal sealed class WeatherReading
    {
        private readonly Dictionary<string, object> values;
        public WeatherReading(Dictionary<string, object> source) { values = source ?? new Dictionary<string, object>(); }

        public double? Number(string key)
        {
            object value;
            if (!values.TryGetValue(key, out value) || value == null) return null;
            double result;
            return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out result)
                ? (double?)result : null;
        }

        public long? Integer(string key)
        {
            double? number = Number(key);
            return number.HasValue ? (long?)Convert.ToInt64(number.Value) : null;
        }

        public string Text(string key)
        {
            object value;
            return values.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;
        }

        public DateTime? Timestamp
        {
            get
            {
                long? milliseconds = Integer("dateutc");
                if (milliseconds.HasValue)
                    return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(milliseconds.Value).ToLocalTime();
                DateTime parsed;
                return DateTime.TryParse(Text("date"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed)
                    ? (DateTime?)parsed.ToLocalTime() : null;
            }
        }
    }

    internal sealed class WeatherState
    {
        public string StationName = "Ambient Weather";
        public string StationLocation = "Weather Station";
        public WeatherReading Current;
        public List<WeatherReading> History = new List<WeatherReading>();
        public DateTime LastSuccessfulRefresh;
        public string Error;
        public bool IsLoading;
        public bool IsConfigured;
    }

    internal enum DisplayUnits { Imperial, Metric }
}
