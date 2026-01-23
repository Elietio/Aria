using System.Windows;
using ScreenBridge.Core;
using Wpf.Ui.Controls;

namespace ScreenBridge.App;

public partial class AudioSelectionWindow : FluentWindow
{
    public AudioDeviceInfo? SelectedDevice { get; private set; }

    public AudioSelectionWindow(IEnumerable<AudioDeviceInfo> devices)
    {
        InitializeComponent();
        DeviceListBox.ItemsSource = devices;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedDevice = DeviceListBox.SelectedItem as AudioDeviceInfo;
        if (SelectedDevice != null)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            System.Windows.MessageBox.Show("请选择一个设备", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
