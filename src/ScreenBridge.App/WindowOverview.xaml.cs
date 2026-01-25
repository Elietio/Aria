using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using ScreenBridge.Core;
using Wpf.Ui.Controls;

namespace ScreenBridge.App;

/// <summary>
/// 窗口概览界面
/// </summary>
public partial class WindowOverview : FluentWindow
{
    private readonly WindowService _windowService;
    private readonly MonitorService _monitorService;
    private List<WindowService.WindowInfo> _allWindows = new();
    private MonitorInfo? _leftMonitor;
    private MonitorInfo? _rightMonitor;

    public class WindowViewModel
    {
        public WindowService.WindowInfo Info { get; init; }
        public string Title => Info.Title;
        public ImageSource? Icon { get; init; }
    }

    public WindowOverview(WindowService windowService, MonitorService monitorService)
    {
        InitializeComponent();
        _windowService = windowService;
        _monitorService = monitorService;

        Loaded += WindowOverview_Loaded;
    }

    private void WindowOverview_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyUIStyle();
        RefreshWindows();
    }

    /// <summary>
    /// 应用当前UI风格的背景材质
    /// </summary>
    private void ApplyUIStyle()
    {
        try
        {
            var config = AppConfig.Load();
            bool isMoe = config.Theme == AppConfig.UIStyle.MoeGlass || config.Theme == AppConfig.UIStyle.MoeClean;
            
            if (isMoe)
            {
                // 根据当前Theme选择对应的背景材质
                var backdrop = (config.Theme == AppConfig.UIStyle.MoeGlass) 
                    ? config.GlassBackdrop 
                    : config.CleanBackdrop;
                
                this.WindowBackdropType = backdrop == AppConfig.BackdropStyle.Acrylic 
                    ? WindowBackdropType.Acrylic 
                    : WindowBackdropType.Mica;
                this.Background = null;
            }
            else
            {
                this.WindowBackdropType = WindowBackdropType.None;
                this.ClearValue(Window.BackgroundProperty);
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ApplyUIStyle failed: {ex.Message}");
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void RefreshWindows()
    {
        _allWindows = _windowService.GetAllWindows();
        var monitors = _monitorService.GetAllMonitors().OrderBy(m => m.Left).ToList();
        
        _leftMonitor = monitors.FirstOrDefault();
        _rightMonitor = monitors.Skip(1).FirstOrDefault();

        // 更新显示器名称
        if (_leftMonitor != null)
        {
            string role = _leftMonitor.IsPrimary ? "主显示器" : "副显示器";
            LeftMonitorHeader.Text = $"🖥️ {role} - {_leftMonitor.FriendlyName}";
        }
        
        if (_rightMonitor != null)
        {
            string role = _rightMonitor.IsPrimary ? "主显示器" : "副显示器";
            RightMonitorHeader.Text = $"🖥️ {role} - {_rightMonitor.FriendlyName}";
        }

        // 按显示器分组窗口
        var leftWindows = new List<WindowViewModel>();
        var rightWindows = new List<WindowViewModel>();

        foreach (var window in _allWindows)
        {
            // 排除自己
            if (window.Title == "窗口概览") continue;

            var viewModel = new WindowViewModel
            {
                Info = window,
                Icon = ToImageSource(window.IconHandle)
            };

            // 判断窗口属于哪个显示器
            if (_leftMonitor != null && IsWindowInMonitor(window, _leftMonitor))
            {
                leftWindows.Add(viewModel);
            }
            else if (_rightMonitor != null && IsWindowInMonitor(window, _rightMonitor))
            {
                rightWindows.Add(viewModel);
            }
            else
            {
                // 如果窗口不在任何一个显示器范围内（比如部分重叠），根据中心点判断
                // 暂时简单归入 Left
                if (_leftMonitor != null) leftWindows.Add(viewModel);
            }
        }

        LeftMonitorList.ItemsSource = leftWindows;
        RightMonitorList.ItemsSource = rightWindows;
    }

    private bool IsWindowInMonitor(WindowService.WindowInfo window, MonitorInfo monitor)
    {
        var centerX = window.Left + window.Width / 2;
        var centerY = window.Top + window.Height / 2;

        return centerX >= monitor.Left &&
               centerX < monitor.Left + monitor.Width &&
               centerY >= monitor.Top &&
               centerY < monitor.Top + monitor.Height;
    }

    private ImageSource? ToImageSource(IntPtr hIcon)
    {
        if (hIcon == IntPtr.Zero) return null;
        try
        {
             var image = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
             // Freeze to make it cross-thread accessible if needed, though here we are on UI thread
             image.Freeze();
             return image;
        }
        catch
        {
            return null;
        }
    }

    private Point _startPoint;
    private bool _isDown;

    private void WindowCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 如果点击的是按钮，不处理拖拽/点击
        if (e.OriginalSource is DependencyObject source && FindVisualParent<Wpf.Ui.Controls.Button>(source) != null)
        {
            _isDown = false;
            return;
        }

        if (sender is FrameworkElement)
        {
            _isDown = true;
            _startPoint = e.GetPosition(null);
        }
    }

    private void WindowCard_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDown && e.LeftButton == MouseButtonState.Pressed)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _startPoint - mousePos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (sender is FrameworkElement element && element.DataContext is WindowViewModel viewModel)
                {
                    _isDown = false; // 拖拽开始，取消点击状态
                    DragDrop.DoDragDrop(element, viewModel, DragDropEffects.Move);
                }
            }
        }
    }

    private void WindowCard_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDown)
        {
            _isDown = false;
            // 这是一个点击动作 -> 激活窗口
            if (sender is FrameworkElement element && element.DataContext is WindowViewModel viewModel)
            {
                _windowService.ActivateWindow(viewModel.Info.Handle);
                Close();
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
            {
                return parent;
            }
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private void LeftMonitor_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(WindowViewModel)) is WindowViewModel viewModel && 
            _leftMonitor != null)
        {
            _windowService.ActivateAndMoveToMonitor(viewModel.Info.Handle, _leftMonitor);
            RefreshWindows();
        }
    }

    private void RightMonitor_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(WindowViewModel)) is WindowViewModel viewModel && 
            _rightMonitor != null)
        {
            _windowService.ActivateAndMoveToMonitor(viewModel.Info.Handle, _rightMonitor);
            RefreshWindows();
        }
    }

    private void MoveToRight_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is WindowViewModel viewModel && _rightMonitor != null)
        {
            _windowService.ActivateAndMoveToMonitor(viewModel.Info.Handle, _rightMonitor);
            RefreshWindows();
        }
    }

    private void MoveToLeft_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is WindowViewModel viewModel && _leftMonitor != null)
        {
            _windowService.ActivateAndMoveToMonitor(viewModel.Info.Handle, _leftMonitor);
            RefreshWindows();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
        else if (e.Key == Key.F5)
        {
            RefreshWindows();
        }
    }
}
