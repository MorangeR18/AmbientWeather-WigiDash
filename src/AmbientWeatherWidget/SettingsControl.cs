using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AmbientWeatherWigiDash
{
    internal sealed class SettingsControl : UserControl
    {
        private readonly AmbientWeatherWidgetInstance parent;
        private readonly PasswordBox apiKey = new PasswordBox();
        private readonly PasswordBox applicationKey = new PasswordBox();
        private readonly ComboBox station = new ComboBox();
        private readonly ComboBox units = new ComboBox();
        private readonly Slider refresh = new Slider();
        private readonly TextBlock refreshValue = new TextBlock();
        private readonly TextBlock status = new TextBlock();
        private readonly Button testButton = new Button();
        private readonly Button saveButton = new Button();
        private List<WeatherDevice> devices = new List<WeatherDevice>();

        public SettingsControl(AmbientWeatherWidgetInstance widgetInstance)
        {
            parent = widgetInstance;
            Margin = new Thickness(10, 20, 10, 10);
            BuildUi();
            LoadValues();
        }

        private void BuildUi()
        {
            var panel = new StackPanel();
            panel.Children.Add(Heading("Ambient Weather Station"));
            panel.Children.Add(Help("Your credentials stay in the WigiDash profile and are encrypted for your Windows account."));
            panel.Children.Add(Label("API key"));
            apiKey.Margin = new Thickness(0, 2, 0, 8);
            panel.Children.Add(apiKey);
            panel.Children.Add(Label("Application key"));
            applicationKey.Margin = new Thickness(0, 2, 0, 8);
            panel.Children.Add(applicationKey);

            testButton.Content = "Test connection / load stations";
            testButton.Padding = new Thickness(8, 5, 8, 5);
            testButton.Margin = new Thickness(0, 4, 0, 12);
            testButton.Click += TestButtonClick;
            panel.Children.Add(testButton);

            panel.Children.Add(Label("Station"));
            station.DisplayMemberPath = "DisplayName";
            station.Margin = new Thickness(0, 2, 0, 10);
            panel.Children.Add(station);

            panel.Children.Add(Label("Units"));
            units.Items.Add("US · °F, mph, inHg, inches");
            units.Items.Add("Metric · °C, km/h, hPa, mm");
            units.Margin = new Thickness(0, 2, 0, 10);
            panel.Children.Add(units);

            var refreshHeader = new DockPanel();
            refreshHeader.Children.Add(Label("Refresh interval"));
            refreshValue.HorizontalAlignment = HorizontalAlignment.Right;
            refreshValue.Foreground = Brushes.LightGray;
            refreshValue.Margin = new Thickness(0, 5, 0, 0);
            DockPanel.SetDock(refreshValue, Dock.Right);
            refreshHeader.Children.Add(refreshValue);
            panel.Children.Add(refreshHeader);
            refresh.Minimum = 30;
            refresh.Maximum = 300;
            refresh.SmallChange = 30;
            refresh.LargeChange = 60;
            refresh.TickFrequency = 30;
            refresh.IsSnapToTickEnabled = true;
            refresh.ValueChanged += delegate { refreshValue.Text = ((int)refresh.Value) + " seconds"; };
            panel.Children.Add(refresh);

            status.Margin = new Thickness(0, 12, 0, 8);
            status.TextWrapping = TextWrapping.Wrap;
            status.Foreground = Brushes.LightGray;
            panel.Children.Add(status);

            saveButton.Content = "Save and refresh";
            saveButton.Padding = new Thickness(8, 6, 8, 6);
            saveButton.Click += SaveButtonClick;
            panel.Children.Add(saveButton);
            Content = panel;
        }

        private void LoadValues()
        {
            apiKey.Password = parent.ApiKey;
            applicationKey.Password = parent.ApplicationKey;
            refresh.Value = parent.RefreshSeconds;
            units.SelectedIndex = parent.Units == DisplayUnits.Metric ? 1 : 0;
            devices = parent.CurrentDevices;
            PopulateStations(parent.SelectedMacAddress);
            status.Text = parent.HasCredentials ? "Credentials saved. Test the connection to refresh the station list." : "Enter both keys, then test the connection.";
        }

        private async void TestButtonClick(object sender, RoutedEventArgs e)
        {
            SetBusy(true, "Connecting to Ambient Weather…");
            try
            {
                devices = await parent.TestCredentialsAsync(apiKey.Password, applicationKey.Password);
                if (devices.Count == 0) throw new ConnectionException(ConnectionErrorKind.NoStations);
                PopulateStations(parent.SelectedMacAddress);
                status.Foreground = Brushes.LightGreen;
                status.Text = "Connected. Found " + devices.Count + (devices.Count == 1 ? " station." : " stations.");
            }
            catch (Exception ex)
            {
                status.Foreground = Brushes.Salmon;
                status.Text = ConnectionErrors.GetMessage(ex);
            }
            finally { SetBusy(false, null); }
        }

        private void SaveButtonClick(object sender, RoutedEventArgs e)
        {
            WeatherDevice selected = station.SelectedItem as WeatherDevice;
            string mac = selected != null ? selected.MacAddress : parent.SelectedMacAddress;
            if (string.IsNullOrWhiteSpace(apiKey.Password) || string.IsNullOrWhiteSpace(applicationKey.Password))
            {
                status.Foreground = Brushes.Salmon;
                status.Text = "Both API keys are required.";
                return;
            }
            parent.ApplySettings(apiKey.Password, applicationKey.Password, mac, (int)refresh.Value,
                units.SelectedIndex == 1 ? DisplayUnits.Metric : DisplayUnits.Imperial);
            status.Foreground = Brushes.LightGreen;
            status.Text = "Saved. The dashboard is refreshing now.";
        }

        private void PopulateStations(string selectedMac)
        {
            station.Items.Clear();
            WeatherDevice selected = null;
            foreach (WeatherDevice device in devices)
            {
                station.Items.Add(device);
                if (string.Equals(device.MacAddress, selectedMac, StringComparison.OrdinalIgnoreCase)) selected = device;
            }
            if (selected != null) station.SelectedItem = selected;
            else if (station.Items.Count > 0) station.SelectedIndex = 0;
        }

        private void SetBusy(bool busy, string message)
        {
            testButton.IsEnabled = !busy;
            saveButton.IsEnabled = !busy;
            if (!string.IsNullOrEmpty(message))
            {
                status.Foreground = Brushes.LightGray;
                status.Text = message;
            }
        }

        private static TextBlock Heading(string value)
        {
            return new TextBlock { Text = value, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
        }

        private static TextBlock Label(string value)
        {
            return new TextBlock { Text = value, FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 0, 0) };
        }

        private static TextBlock Help(string value)
        {
            return new TextBlock { Text = value, FontSize = 10, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
        }
    }
}
