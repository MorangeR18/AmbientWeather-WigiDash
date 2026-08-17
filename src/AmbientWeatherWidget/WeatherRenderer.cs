using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;

namespace AmbientWeatherWigiDash
{
    internal static class WeatherRenderer
    {
        private static readonly Color BackgroundTop = Color.FromArgb(7, 20, 34);
        private static readonly Color BackgroundBottom = Color.FromArgb(10, 42, 58);
        private static readonly Color Card = Color.FromArgb(185, 20, 49, 65);
        private static readonly Color CardBorder = Color.FromArgb(55, 126, 179, 196);
        private static readonly Color Text = Color.FromArgb(242, 248, 250);
        private static readonly Color Muted = Color.FromArgb(165, 195, 205);
        private static readonly Color Cyan = Color.FromArgb(69, 211, 231);
        private static readonly Color Blue = Color.FromArgb(74, 151, 255);
        private static readonly Color Yellow = Color.FromArgb(255, 200, 84);
        private static readonly Color Green = Color.FromArgb(76, 220, 146);
        private static readonly Color Red = Color.FromArgb(255, 101, 116);

        public static Bitmap Render(WeatherState state, Size size, DisplayUnits units)
        {
            var bitmap = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format16bppRgb565);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                using (var background = new LinearGradientBrush(new Rectangle(Point.Empty, size), BackgroundTop, BackgroundBottom, 35f))
                    g.FillRectangle(background, 0, 0, size.Width, size.Height);

                if (!state.IsConfigured) DrawSetup(g, size);
                else if (state.Current == null) DrawLoadingOrError(g, size, state);
                else if (size.Width >= 800 && size.Height >= 500) DrawFull(g, size, state, units);
                else if (size.Width >= 400 && size.Height >= 280) DrawMedium(g, size, state, units);
                else DrawCompact(g, size, state, units);
            }
            return bitmap;
        }

        public static Bitmap RenderPreview(Size size)
        {
            var now = DateTime.Now;
            var current = new Dictionary<string, object>
            {
                { "dateutc", (long)(now.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalMilliseconds },
                { "tempf", 72.4 }, { "feelsLike", 72.1 }, { "humidity", 54 }, { "dewPoint", 54.7 },
                { "windspeedmph", 5.8 }, { "windgustmph", 11.2 }, { "winddir", 238 },
                { "baromrelin", 29.96 }, { "hourlyrainin", 0.0 }, { "dailyrainin", 0.08 },
                { "monthlyrainin", 2.14 }, { "tempinf", 70.2 }, { "humidityin", 45 },
                { "uv", 3 }, { "solarradiation", 418 }
            };
            var history = new List<WeatherReading>();
            for (int i = 0; i < 48; i++)
                history.Add(new WeatherReading(new Dictionary<string, object> { { "tempf", 60 + Math.Sin(i / 11.0) * 8 + i * .18 }, { "baromrelin", 29.84 + i * .002 } }));
            return Render(new WeatherState
            {
                IsConfigured = true,
                StationName = "Sample Weather Station",
                StationLocation = "Demo Location",
                Current = new WeatherReading(current),
                History = history,
                LastSuccessfulRefresh = now
            }, size, DisplayUnits.Imperial);
        }

        private static void DrawSetup(Graphics g, Size size)
        {
            float scale = Math.Min(size.Width / 1016f, size.Height / 592f);
            DrawWeatherGlyph(g, new RectangleF(size.Width / 2f - 65 * scale, 44 * scale, 130 * scale, 105 * scale), false, true);
            DrawCentered(g, "AMBIENT WEATHER", new RectangleF(10, 175 * scale, size.Width - 20, 52 * scale), 32 * scale, FontStyle.Bold, Text);
            DrawCentered(g, "Open widget settings to add your API and application keys", new RectangleF(22, 240 * scale, size.Width - 44, 80 * scale), 18 * scale, FontStyle.Regular, Muted);
            DrawPill(g, new RectangleF(size.Width / 2f - 100 * scale, 340 * scale, 200 * scale, 44 * scale), "SET UP STATION", Cyan);
        }

        private static void DrawLoadingOrError(Graphics g, Size size, WeatherState state)
        {
            float scale = Math.Min(size.Width / 1016f, size.Height / 592f);
            string title = string.IsNullOrEmpty(state.Error) ? "CONNECTING TO STATION" : "STATION OFFLINE";
            string detail = string.IsNullOrEmpty(state.Error) ? "Fetching the latest observation…" : state.Error;
            DrawCentered(g, title, new RectangleF(16, size.Height * .32f, size.Width - 32, 50 * scale), 28 * scale, FontStyle.Bold, string.IsNullOrEmpty(state.Error) ? Cyan : Red);
            DrawCentered(g, detail, new RectangleF(24, size.Height * .47f, size.Width - 48, 110 * scale), 17 * scale, FontStyle.Regular, Muted);
        }

        private static void DrawFull(Graphics g, Size actual, WeatherState state, DisplayUnits units)
        {
            float sx = actual.Width / 1016f;
            float sy = actual.Height / 592f;
            GraphicsState saved = g.Save();
            g.ScaleTransform(sx, sy);
            var reading = state.Current;
            var trendReadings = IncludeCurrentReading(state.History, reading);

            DrawHeader(g, state, 1016);
            DrawCard(g, new RectangleF(18, 74, 316, 360));
            DrawCard(g, new RectangleF(346, 74, 310, 360));
            DrawCard(g, new RectangleF(668, 74, 330, 360));
            DrawCard(g, new RectangleF(18, 446, 980, 128));

            DrawWeatherGlyph(g, new RectangleF(38, 97, 106, 82), IsRaining(reading), IsDaylight(reading));
            DrawText(g, "OUTDOOR", new RectangleF(158, 98, 150, 24), 13, FontStyle.Bold, Muted, StringAlignment.Near);
            DrawText(g, FormatTemp(reading.Number("tempf"), units, 0), new RectangleF(146, 116, 172, 78), 60, FontStyle.Bold, Text, StringAlignment.Near);
            DrawText(g, "FEELS " + FormatTemp(reading.Number("feelsLike"), units, 0), new RectangleF(38, 200, 270, 28), 17, FontStyle.Bold, Cyan, StringAlignment.Near);
            DrawMetricPair(g, 38, 248, "HUMIDITY", FormatNumber(reading.Number("humidity"), "0", "%"), "DEW POINT", FormatTemp(reading.Number("dewPoint"), units, 0));
            DrawMetricPair(g, 38, 325, "INDOOR", FormatTemp(reading.Number("tempinf"), units, 0), "INDOOR HUM.", FormatNumber(reading.Number("humidityin"), "0", "%"));
            string condition = ConditionText(reading);
            DrawPill(g, new RectangleF(38, 393, 276, 28), condition.ToUpperInvariant(), IsRaining(reading) ? Blue : Cyan);

            DrawWindCompass(g, new PointF(500, 198), 90, reading.Number("winddir"));
            DrawText(g, "WIND", new RectangleF(366, 82, 270, 20), 12, FontStyle.Bold, Muted, StringAlignment.Center);
            DrawText(g, FormatSpeed(reading.Number("windspeedmph"), units, 0), new RectangleF(390, 306, 220, 48), 32, FontStyle.Bold, Text, StringAlignment.Center);
            DrawMetricPair(g, 376, 365, "GUST", FormatSpeed(reading.Number("windgustmph"), units, 0), "DIRECTION", FormatDirection(reading.Number("winddir")));

            DrawText(g, "DETAILS", new RectangleF(690, 91, 284, 24), 13, FontStyle.Bold, Muted, StringAlignment.Near);
            DrawDetailRow(g, 690, 128, "PRESSURE", FormatPressure(reading.Number("baromrelin"), units), PressureTrend(trendReadings));
            DrawDetailRow(g, 690, 179, "RAIN / HOUR", FormatRain(reading.Number("hourlyrainin"), units), "TODAY  " + FormatRain(reading.Number("dailyrainin"), units));
            DrawDetailRow(g, 690, 230, "MONTHLY RAIN", FormatRain(reading.Number("monthlyrainin"), units), "YEAR  " + FormatRain(reading.Number("yearlyrainin"), units));
            DrawDetailRow(g, 690, 281, "UV INDEX", FormatNumber(reading.Number("uv"), "0.#", ""), "SOLAR  " + FormatNumber(reading.Number("solarradiation"), "0", " W/m²"));
            string specialLabel;
            string specialValue;
            string specialSub;
            GetSpecialReading(reading, out specialLabel, out specialValue, out specialSub);
            DrawDetailRow(g, 690, 332, specialLabel, specialValue, specialSub);
            DrawBatteryRow(g, 690, 390, reading);

            DrawText(g, "24-HOUR TEMPERATURE", new RectangleF(38, 460, 260, 22), 12, FontStyle.Bold, Muted, StringAlignment.Near);
            DrawSparkline(g, new RectangleF(38, 488, 620, 64), trendReadings, "tempf", Blue, units == DisplayUnits.Metric);
            double? min = HistoryExtreme(trendReadings, "tempf", false);
            double? max = HistoryExtreme(trendReadings, "tempf", true);
            DrawText(g, "LOW  " + FormatTemp(min, units, 0), new RectangleF(682, 480, 135, 28), 16, FontStyle.Bold, Muted, StringAlignment.Near);
            DrawText(g, "HIGH  " + FormatTemp(max, units, 0), new RectangleF(830, 480, 148, 28), 16, FontStyle.Bold, Yellow, StringAlignment.Near);
            DrawText(g, "Pressure " + PressureTrend(trendReadings), new RectangleF(682, 529, 296, 26), 14, FontStyle.Bold, Cyan, StringAlignment.Near);
            g.Restore(saved);
        }

        private static void DrawMedium(Graphics g, Size actual, WeatherState state, DisplayUnits units)
        {
            float sx = actual.Width / 404f;
            float sy = actual.Height / 294f;
            GraphicsState saved = g.Save();
            g.ScaleTransform(sx, sy);
            var r = state.Current;
            var trendReadings = IncludeCurrentReading(state.History, r);
            DrawText(g, state.StationName, new RectangleF(14, 11, 270, 26), 16, FontStyle.Bold, Text, StringAlignment.Near);
            DrawStatus(g, new RectangleF(268, 10, 124, 24), state);
            DrawCard(g, new RectangleF(12, 48, 184, 145));
            DrawCard(g, new RectangleF(208, 48, 184, 145));
            DrawWeatherGlyph(g, new RectangleF(25, 64, 62, 50), IsRaining(r), IsDaylight(r));
            DrawText(g, FormatTemp(r.Number("tempf"), units, 0), new RectangleF(88, 57, 96, 58), 39, FontStyle.Bold, Text, StringAlignment.Near);
            DrawText(g, "FEELS " + FormatTemp(r.Number("feelsLike"), units, 0), new RectangleF(25, 124, 150, 22), 12, FontStyle.Bold, Cyan, StringAlignment.Near);
            DrawText(g, FormatNumber(r.Number("humidity"), "0", "%") + " HUM  ·  " + FormatTemp(r.Number("dewPoint"), units, 0) + " DEW", new RectangleF(25, 157, 160, 20), 11, FontStyle.Bold, Muted, StringAlignment.Near);
            DrawWindCompass(g, new PointF(254, 112), 48, r.Number("winddir"));
            DrawText(g, FormatSpeed(r.Number("windspeedmph"), units, 0), new RectangleF(304, 78, 78, 28), 18, FontStyle.Bold, Text, StringAlignment.Center);
            DrawText(g, "GUST " + FormatSpeed(r.Number("windgustmph"), units, 0), new RectangleF(301, 117, 84, 18), 10, FontStyle.Bold, Muted, StringAlignment.Center);
            DrawText(g, FormatPressure(r.Number("baromrelin"), units), new RectangleF(301, 151, 84, 18), 10, FontStyle.Bold, Cyan, StringAlignment.Center);
            DrawCard(g, new RectangleF(12, 205, 380, 76));
            DrawText(g, "RAIN " + FormatRain(r.Number("dailyrainin"), units), new RectangleF(24, 218, 120, 20), 12, FontStyle.Bold, Blue, StringAlignment.Near);
            DrawText(g, "UV " + FormatNumber(r.Number("uv"), "0.#", ""), new RectangleF(150, 218, 75, 20), 12, FontStyle.Bold, Yellow, StringAlignment.Near);
            DrawText(g, "IN " + FormatTemp(r.Number("tempinf"), units, 0), new RectangleF(230, 218, 90, 20), 12, FontStyle.Bold, Muted, StringAlignment.Near);
            DrawSparkline(g, new RectangleF(24, 246, 356, 24), trendReadings, "tempf", Blue, units == DisplayUnits.Metric);
            g.Restore(saved);
        }

        private static void DrawCompact(Graphics g, Size actual, WeatherState state, DisplayUnits units)
        {
            var r = state.Current;
            float scale = Math.Min(actual.Width / 200f, actual.Height / 145f);
            DrawWeatherGlyph(g, new RectangleF(10 * scale, 13 * scale, 58 * scale, 48 * scale), IsRaining(r), IsDaylight(r));
            DrawText(g, FormatTemp(r.Number("tempf"), units, 0), new RectangleF(70 * scale, 10 * scale, actual.Width - 76 * scale, 55 * scale), 38 * scale, FontStyle.Bold, Text, StringAlignment.Near);
            DrawText(g, FormatNumber(r.Number("humidity"), "0", "%") + " HUMIDITY", new RectangleF(13 * scale, 74 * scale, actual.Width - 26 * scale, 21 * scale), 12 * scale, FontStyle.Bold, Cyan, StringAlignment.Near);
            DrawText(g, FormatSpeed(r.Number("windspeedmph"), units, 0) + "  " + FormatDirection(r.Number("winddir")), new RectangleF(13 * scale, 104 * scale, actual.Width - 26 * scale, 23 * scale), 13 * scale, FontStyle.Bold, Muted, StringAlignment.Near);
            if (!string.IsNullOrEmpty(state.Error)) DrawText(g, "STALE", new RectangleF(actual.Width - 55 * scale, 117 * scale, 45 * scale, 16 * scale), 8 * scale, FontStyle.Bold, Red, StringAlignment.Far);
        }

        private static void DrawHeader(Graphics g, WeatherState state, float width)
        {
            DrawText(g, state.StationName.ToUpperInvariant(), new RectangleF(20, 15, 560, 30), 22, FontStyle.Bold, Text, StringAlignment.Near);
            DrawText(g, state.StationLocation, new RectangleF(22, 44, 480, 20), 12, FontStyle.Regular, Muted, StringAlignment.Near);
            DrawStatus(g, new RectangleF(width - 262, 20, 242, 28), state);
        }

        private static void DrawStatus(Graphics g, RectangleF rect, WeatherState state)
        {
            bool fresh = string.IsNullOrEmpty(state.Error) && state.LastSuccessfulRefresh > DateTime.Now.AddMinutes(-5);
            using (var brush = new SolidBrush(fresh ? Green : Red)) g.FillEllipse(brush, rect.X, rect.Y + 5, 10, 10);
            string label = fresh ? "LIVE · " + state.LastSuccessfulRefresh.ToString("h:mm tt") : "STALE · " + state.LastSuccessfulRefresh.ToString("h:mm tt");
            DrawText(g, label, new RectangleF(rect.X + 17, rect.Y, rect.Width - 17, rect.Height), rect.Width < 150 ? 9 : 12, FontStyle.Bold, fresh ? Muted : Red, StringAlignment.Near);
        }

        private static void DrawMetricPair(Graphics g, float x, float y, string label1, string value1, string label2, string value2)
        {
            DrawText(g, label1, new RectangleF(x, y, 118, 18), 10, FontStyle.Bold, Muted, StringAlignment.Near);
            DrawText(g, value1, new RectangleF(x, y + 21, 118, 30), 20, FontStyle.Bold, Text, StringAlignment.Near);
            DrawText(g, label2, new RectangleF(x + 142, y, 124, 18), 10, FontStyle.Bold, Muted, StringAlignment.Near);
            DrawText(g, value2, new RectangleF(x + 142, y + 21, 124, 30), 20, FontStyle.Bold, Text, StringAlignment.Near);
        }

        private static void DrawDetailRow(Graphics g, float x, float y, string label, string value, string secondary)
        {
            using (var pen = new Pen(Color.FromArgb(35, 147, 183, 195), 1)) g.DrawLine(pen, x, y + 40, x + 286, y + 40);
            DrawText(g, label, new RectangleF(x, y, 116, 17), 10, FontStyle.Bold, Muted, StringAlignment.Near);
            DrawText(g, value, new RectangleF(x + 120, y - 2, 166, 23), 16, FontStyle.Bold, Text, StringAlignment.Far);
            DrawText(g, secondary, new RectangleF(x, y + 19, 286, 18), 10, FontStyle.Bold, Cyan, StringAlignment.Far);
        }

        private static void DrawBatteryRow(Graphics g, float x, float y, WeatherReading r)
        {
            double? battery = r.Number("battout");
            string text = !battery.HasValue ? "SENSOR BATTERY  —" : (battery.Value > .5 ? "SENSOR BATTERY  OK" : "SENSOR BATTERY  LOW");
            DrawText(g, text, new RectangleF(x, y, 286, 22), 10, FontStyle.Bold, battery.HasValue && battery.Value <= .5 ? Red : Muted, StringAlignment.Far);
        }

        private static void DrawWindCompass(Graphics g, PointF center, float radius, double? direction)
        {
            using (var pen = new Pen(Color.FromArgb(80, 120, 190, 207), 2)) g.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            string[] labels = { "N", "E", "S", "W" };
            PointF[] points = { new PointF(center.X, center.Y - radius + 6), new PointF(center.X + radius - 15, center.Y - 8), new PointF(center.X, center.Y + radius - 23), new PointF(center.X - radius + 6, center.Y - 8) };
            for (int i = 0; i < labels.Length; i++) DrawText(g, labels[i], new RectangleF(points[i].X - 10, points[i].Y, 20, 18), 10, FontStyle.Bold, Muted, StringAlignment.Center);
            if (!direction.HasValue) return;
            double angle = (direction.Value - 90) * Math.PI / 180.0;
            PointF tip = new PointF(center.X + (float)Math.Cos(angle) * (radius - 30), center.Y + (float)Math.Sin(angle) * (radius - 30));
            PointF tail = new PointF(center.X - (float)Math.Cos(angle) * 24, center.Y - (float)Math.Sin(angle) * 24);
            using (var pen = new Pen(Cyan, 7) { StartCap = LineCap.Round, EndCap = LineCap.ArrowAnchor }) g.DrawLine(pen, tail, tip);
            using (var brush = new SolidBrush(Text)) g.FillEllipse(brush, center.X - 7, center.Y - 7, 14, 14);
            DrawText(g, CardinalDirection(direction), new RectangleF(center.X - 46, center.Y + 32, 92, 28), 18, FontStyle.Bold, Text, StringAlignment.Center);
        }

        private static void DrawSparkline(Graphics g, RectangleF rect, List<WeatherReading> history, string key, Color color, bool convertTemp)
        {
            if (history == null) return;
            var values = history.Select(delegate(WeatherReading item) { return item.Number(key); }).Where(delegate(double? n) { return n.HasValue; }).Select(delegate(double? n) { return convertTemp ? (n.Value - 32) * 5 / 9 : n.Value; }).ToList();
            if (values.Count < 2)
            {
                DrawText(g, "Trend data will appear after the first history refresh", rect, 10, FontStyle.Regular, Muted, StringAlignment.Center);
                return;
            }
            double min = values.Min();
            double max = values.Max();
            if (Math.Abs(max - min) < .001) max = min + 1;
            var points = new PointF[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                float x = rect.Left + rect.Width * i / (values.Count - 1f);
                float y = rect.Bottom - (float)((values[i] - min) / (max - min)) * rect.Height;
                points[i] = new PointF(x, y);
            }
            using (var pen = new Pen(Color.FromArgb(35, 120, 185, 200), 1))
            {
                g.DrawLine(pen, rect.Left, rect.Bottom, rect.Right, rect.Bottom);
                g.DrawLine(pen, rect.Left, rect.Top, rect.Right, rect.Top);
            }
            using (var pen = new Pen(color, 3)) g.DrawLines(pen, points);
        }

        private static void DrawWeatherGlyph(Graphics g, RectangleF rect, bool raining, bool daylight)
        {
            float unit = Math.Min(rect.Width, rect.Height) / 100f;
            if (daylight)
            {
                using (var sun = new SolidBrush(Yellow)) g.FillEllipse(sun, rect.X + 48 * unit, rect.Y + 4 * unit, 42 * unit, 42 * unit);
                using (var pen = new Pen(Color.FromArgb(180, Yellow), 3 * unit))
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        g.DrawLine(pen, rect.X + 69 * unit + (float)Math.Cos(a) * 27 * unit, rect.Y + 25 * unit + (float)Math.Sin(a) * 27 * unit,
                            rect.X + 69 * unit + (float)Math.Cos(a) * 34 * unit, rect.Y + 25 * unit + (float)Math.Sin(a) * 34 * unit);
                    }
            }
            using (var cloud = new SolidBrush(Color.FromArgb(232, 226, 239, 243)))
            {
                g.FillEllipse(cloud, rect.X + 8 * unit, rect.Y + 40 * unit, 45 * unit, 39 * unit);
                g.FillEllipse(cloud, rect.X + 34 * unit, rect.Y + 27 * unit, 53 * unit, 54 * unit);
                g.FillRectangle(cloud, rect.X + 25 * unit, rect.Y + 54 * unit, 67 * unit, 28 * unit);
            }
            if (raining)
            {
                using (var pen = new Pen(Blue, 4 * unit))
                    for (int i = 0; i < 3; i++) g.DrawLine(pen, rect.X + (30 + i * 22) * unit, rect.Y + 88 * unit, rect.X + (25 + i * 22) * unit, rect.Y + 100 * unit);
            }
        }

        private static void DrawCard(Graphics g, RectangleF rect)
        {
            using (GraphicsPath path = RoundedRect(rect, 16))
            using (var brush = new SolidBrush(Card))
            using (var pen = new Pen(CardBorder, 1))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
        }

        private static void DrawPill(Graphics g, RectangleF rect, string value, Color color)
        {
            using (GraphicsPath path = RoundedRect(rect, rect.Height / 2))
            using (var brush = new SolidBrush(Color.FromArgb(32, color)))
            using (var pen = new Pen(Color.FromArgb(145, color), 1))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
            DrawText(g, value, rect, Math.Max(9, rect.Height * .4f), FontStyle.Bold, color, StringAlignment.Center);
        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void DrawCentered(Graphics g, string value, RectangleF rect, float size, FontStyle style, Color color) { DrawText(g, value, rect, size, style, color, StringAlignment.Center); }

        private static void DrawText(Graphics g, string value, RectangleF rect, float size, FontStyle style, Color color, StringAlignment alignment)
        {
            if (string.IsNullOrEmpty(value)) value = "—";
            using (var font = new Font("Segoe UI", Math.Max(6, size), style, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Alignment = alignment, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(value, font, brush, rect, format);
        }

        private static bool IsRaining(WeatherReading r) { return r != null && r.Number("hourlyrainin").GetValueOrDefault() > .001; }
        private static bool IsDaylight(WeatherReading r)
        {
            double? solar = r == null ? null : r.Number("solarradiation");
            return solar.HasValue ? solar.Value > 5 : DateTime.Now.Hour >= 7 && DateTime.Now.Hour < 19;
        }

        private static string ConditionText(WeatherReading r)
        {
            if (IsRaining(r)) return "Rain detected";
            if (r.Number("windspeedmph").GetValueOrDefault() >= 20) return "Windy";
            if (r.Number("uv").GetValueOrDefault() >= 6) return "High UV";
            return "Live conditions";
        }

        private static string FormatTemp(double? fahrenheit, DisplayUnits units, int decimals)
        {
            if (!fahrenheit.HasValue) return "—";
            double value = units == DisplayUnits.Metric ? (fahrenheit.Value - 32) * 5 / 9 : fahrenheit.Value;
            return value.ToString(decimals == 0 ? "0" : "0." + new string('0', decimals), CultureInfo.InvariantCulture) + "°" + (units == DisplayUnits.Metric ? "C" : "F");
        }

        private static string FormatSpeed(double? mph, DisplayUnits units, int decimals)
        {
            if (!mph.HasValue) return "—";
            double value = units == DisplayUnits.Metric ? mph.Value * 1.609344 : mph.Value;
            return value.ToString(decimals == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) + (units == DisplayUnits.Metric ? " km/h" : " mph");
        }

        private static string FormatPressure(double? inHg, DisplayUnits units)
        {
            if (!inHg.HasValue) return "—";
            return units == DisplayUnits.Metric ? (inHg.Value * 33.8638867).ToString("0", CultureInfo.InvariantCulture) + " hPa" : inHg.Value.ToString("0.00", CultureInfo.InvariantCulture) + " inHg";
        }

        private static string FormatRain(double? inches, DisplayUnits units)
        {
            if (!inches.HasValue) return "—";
            return units == DisplayUnits.Metric ? (inches.Value * 25.4).ToString("0.0", CultureInfo.InvariantCulture) + " mm" : inches.Value.ToString("0.00", CultureInfo.InvariantCulture) + " in";
        }

        private static string FormatNumber(double? value, string format, string suffix) { return value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) + suffix : "—"; }

        private static string FormatDirection(double? degrees)
        {
            if (!degrees.HasValue) return "—";
            return CardinalDirection(degrees) + " " + degrees.Value.ToString("0", CultureInfo.InvariantCulture) + "°";
        }

        private static string CardinalDirection(double? degrees)
        {
            if (!degrees.HasValue) return "—";
            string[] points = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
            int index = (int)Math.Round(((degrees.Value % 360) / 22.5), MidpointRounding.AwayFromZero) % 16;
            return points[index];
        }

        internal static double? HistoryExtreme(List<WeatherReading> history, string key, bool max)
        {
            if (history == null) return null;
            var values = history.Select(delegate(WeatherReading item) { return item.Number(key); }).Where(delegate(double? v) { return v.HasValue; }).Select(delegate(double? v) { return v.Value; }).ToList();
            return values.Count == 0 ? (double?)null : (max ? values.Max() : values.Min());
        }

        internal static List<WeatherReading> IncludeCurrentReading(List<WeatherReading> history, WeatherReading current)
        {
            var readings = history == null ? new List<WeatherReading>() : new List<WeatherReading>(history);
            if (current == null) return readings;

            DateTime? currentTimestamp = current.Timestamp;
            bool alreadyIncluded = currentTimestamp.HasValue && readings.Any(delegate(WeatherReading reading)
            {
                return reading.Timestamp.HasValue && reading.Timestamp.Value == currentTimestamp.Value;
            });
            if (!alreadyIncluded) readings.Add(current);
            return readings;
        }

        private static string PressureTrend(List<WeatherReading> history)
        {
            if (history == null || history.Count < 2) return "TREND —";
            double? first = history[Math.Max(0, history.Count - 13)].Number("baromrelin");
            double? last = history[history.Count - 1].Number("baromrelin");
            if (!first.HasValue || !last.HasValue) return "TREND —";
            double delta = last.Value - first.Value;
            if (delta > .02) return "RISING ↑";
            if (delta < -.02) return "FALLING ↓";
            return "STEADY →";
        }

        private static void GetSpecialReading(WeatherReading r, out string label, out string value, out string secondary)
        {
            if (r.Number("lightning_distance").HasValue || r.Number("lightning_day").HasValue)
            {
                label = "LIGHTNING";
                value = FormatNumber(r.Number("lightning_distance"), "0.#", " mi");
                secondary = "TODAY  " + FormatNumber(r.Number("lightning_day"), "0", " strikes");
            }
            else if (r.Number("pm25").HasValue || r.Number("pm25_in").HasValue)
            {
                label = "PM2.5";
                value = FormatNumber(r.Number("pm25") ?? r.Number("pm25_in"), "0.#", " µg/m³");
                secondary = "AIR QUALITY SENSOR";
            }
            else
            {
                label = "DAILY GUST";
                value = FormatNumber(r.Number("maxdailygust"), "0.#", " mph");
                secondary = "MAXIMUM TODAY";
            }
        }
    }
}
