using System;
using System.Collections.Generic;
using System.Drawing;
using WigiDashWidgetFramework;
using WigiDashWidgetFramework.WidgetUtility;

namespace AmbientWeatherWigiDash
{
    public sealed class AmbientWeatherWidget : IWidgetObject
    {
        public Guid Guid { get { return new Guid(GetType().Assembly.GetName().Name); } }
        public string Name { get { return "Ambient Weather Station"; } }
        public string Author { get { return "MorangeR18"; } }
        public string Website { get { return "https://github.com/MorangeR18/AmbientWeather-WigiDash"; } }
        public string Description { get { return "Live Ambient Weather station dashboard with 24-hour trends."; } }
        public Version Version { get { return GetType().Assembly.GetName().Version; } }
        public SdkVersion TargetSdk { get { return WidgetUtility.CurrentSdkVersion; } }
        public string LastErrorMessage { get; set; }
        public IWidgetManager WidgetManager { get; set; }
        internal string ResourcePath { get; private set; }

        public List<WidgetSize> SupportedSizes
        {
            get
            {
                return new List<WidgetSize>
                {
                    new WidgetSize(1, 1),
                    new WidgetSize(2, 1),
                    new WidgetSize(2, 2),
                    new WidgetSize(3, 2),
                    new WidgetSize(5, 4)
                };
            }
        }

        public Bitmap PreviewImage { get { return GetWidgetPreview(new WidgetSize(5, 4)); } }
        public Bitmap WidgetThumbnail { get { return GetWidgetPreview(new WidgetSize(1, 1)); } }

        public WidgetError Load(string resourcePath)
        {
            ResourcePath = resourcePath;
            return WidgetError.NO_ERROR;
        }

        public WidgetError Unload() { return WidgetError.NO_ERROR; }

        public IWidgetInstance CreateWidgetInstance(WidgetSize widgetSize, Guid instanceGuid)
        {
            return new AmbientWeatherWidgetInstance(this, widgetSize, instanceGuid);
        }

        public bool RemoveWidgetInstance(Guid instanceGuid) { return true; }
        public Bitmap GetWidgetPreview(WidgetSize widgetSize) { return WeatherRenderer.RenderPreview(widgetSize.ToSize()); }
    }
}
