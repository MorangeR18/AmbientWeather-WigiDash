using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

// These are minimal, original test contracts for the members used by the widget.
// They are not vendor SDK source and are never included in the shipped DLL.
namespace System.Windows.Controls
{
    public class UserControl { }
}

namespace WigiDashWidgetFramework
{
    public interface IWidgetInstance : IDisposable { }
    public interface IWidgetObject { IWidgetManager WidgetManager { get; } }
    public interface IWidgetManager
    {
        bool LoadSetting(IWidgetInstance instance, string key, out string value);
        void StoreSetting(IWidgetInstance instance, string key, string value, bool plainText);
        void WriteLogMessage(IWidgetInstance instance, WidgetUtility.LogLevel level, string message, string detail);
    }
    public delegate void WidgetUpdatedEventHandler(object sender, WidgetUpdatedEventArgs args);
    public sealed class WidgetUpdatedEventArgs : EventArgs
    {
        public Bitmap WidgetBitmap;
        public int WaitMax;
    }
}

namespace WigiDashWidgetFramework.WidgetUtility
{
    public enum ClickType { Single, Double, Other }
    public enum LogLevel { WARN }
    public struct WidgetSize
    {
        public WidgetSize(int width, int height) { }
        public Size ToSize() { return new Size(2, 2); }
    }
}

namespace AmbientWeatherWigiDash
{
    internal sealed class SettingsControl : System.Windows.Controls.UserControl
    {
        internal SettingsControl(AmbientWeatherWidgetInstance instance) { }
    }

    internal static class SecretProtector
    {
        // Only synthetic in-memory fixture settings reach this test double.
        internal static string Protect(string value) { return value; }
        internal static string Unprotect(string value) { return value; }
    }

    internal static class WeatherRenderer
    {
        internal static Action BeforeRender;
        internal static int Calls;
        internal static Bitmap Render(WeatherState state, Size size, DisplayUnits units)
        {
            Interlocked.Increment(ref Calls);
            Action hook = BeforeRender;
            if (hook != null) hook();
            return new Bitmap(size.Width, size.Height);
        }
    }
}
