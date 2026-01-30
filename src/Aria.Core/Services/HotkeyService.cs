using System.Runtime.InteropServices;

namespace Aria.Core;

/// <summary>
/// 全局热键服务
/// </summary>
public class HotkeyService : IDisposable
{
    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private int _currentId = 0;
    private IntPtr _windowHandle;
    private bool _disposed;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// 修饰键枚举
    /// </summary>
    [Flags]
    public enum ModifierKeys : uint
    {
        None = 0x0000,
        Alt = 0x0001,
        Ctrl = 0x0002,
        Shift = 0x0004,
        Win = 0x0008
    }

    /// <summary>
    /// 常用虚拟键码
    /// </summary>
    public static class VirtualKeys
    {
        public const uint VK_S = 0x53;      // S键
        public const uint VK_W = 0x57;      // W键
        public const uint VK_M = 0x4D;      // M键
        public const uint VK_F1 = 0x70;     // F1
        public const uint VK_F2 = 0x71;     // F2
        public const uint VK_F9 = 0x78;     // F9
        public const uint VK_F10 = 0x79;    // F10
        public const uint VK_F11 = 0x7A;    // F11
        public const uint VK_F12 = 0x7B;    // F12
    }

    /// <summary>
    /// 初始化服务，需要传入窗口句柄
    /// </summary>
    public void Initialize(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    /// <summary>
    /// 注册热键
    /// </summary>
    public int RegisterHotkey(ModifierKeys modifiers, uint key, Action callback)
    {
        if (_windowHandle == IntPtr.Zero)
            throw new InvalidOperationException("Service not initialized. Call Initialize first.");

        var id = ++_currentId;
        if (RegisterHotKey(_windowHandle, id, (uint)modifiers, key))
        {
            _hotkeyActions[id] = callback;
            return id;
        }

        return -1; // 注册失败
    }

    /// <summary>
    /// 取消注册热键
    /// </summary>
    public bool UnregisterHotkey(int id)
    {
        if (_hotkeyActions.ContainsKey(id))
        {
            UnregisterHotKey(_windowHandle, id);
            _hotkeyActions.Remove(id);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 处理Windows消息，在窗口过程中调用
    /// </summary>
    public void ProcessHotkeyMessage(int hotkeyId)
    {
        if (_hotkeyActions.TryGetValue(hotkeyId, out var action))
        {
            action?.Invoke();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var id in _hotkeyActions.Keys.ToList())
        {
            UnregisterHotKey(_windowHandle, id);
        }
        _hotkeyActions.Clear();
    }
}
