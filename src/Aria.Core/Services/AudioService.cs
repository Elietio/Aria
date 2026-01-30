using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;

namespace Aria.Core;

/// <summary>
/// 音频设备信息
/// </summary>
public class AudioDeviceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public bool IsDefault { get; init; }
}

/// <summary>
/// 音频设备切换服务 - 使用 AudioSwitcher.AudioApi
/// </summary>
public class AudioService : IDisposable
{
    private readonly CoreAudioController _controller;
    private bool _disposed;

    public AudioService()
    {
        _controller = new CoreAudioController();
    }

    /// <summary>
    /// 获取所有可用的音频输出设备
    /// </summary>
    public async Task<IEnumerable<AudioDeviceInfo>> GetPlaybackDevicesAsync()
    {
        var devices = await _controller.GetPlaybackDevicesAsync(DeviceState.Active);
        var defaultDevice = _controller.DefaultPlaybackDevice;

        return devices.Select(d => new AudioDeviceInfo
        {
            Id = d.Id.ToString(),
            Name = d.Name,
            FullName = d.FullName,
            IsDefault = defaultDevice?.Id == d.Id
        });
    }

    /// <summary>
    /// 获取当前默认的音频输出设备
    /// </summary>
    public Task<AudioDeviceInfo?> GetDefaultPlaybackDeviceAsync()
    {
        var device = _controller.DefaultPlaybackDevice;
        if (device == null)
            return Task.FromResult<AudioDeviceInfo?>(null);

        return Task.FromResult<AudioDeviceInfo?>(new AudioDeviceInfo
        {
            Id = device.Id.ToString(),
            Name = device.Name,
            FullName = device.FullName,
            IsDefault = true
        });
    }

    /// <summary>
    /// 切换到指定的音频输出设备
    /// </summary>
    public async Task<bool> SetDefaultPlaybackDeviceAsync(string deviceId)
    {
        try
        {
            if (Guid.TryParse(deviceId, out var guid))
            {
                var device = await _controller.GetDeviceAsync(guid);
                if (device != null)
                {
                    return await device.SetAsDefaultAsync();
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 根据设备名称关键字切换音频设备
    /// </summary>
    public async Task<bool> SwitchToDeviceByNameAsync(string nameKeyword)
    {
        var devices = await GetPlaybackDevicesAsync();
        var targetDevice = devices.FirstOrDefault(d => 
            d.Name.Contains(nameKeyword, StringComparison.OrdinalIgnoreCase) ||
            d.FullName.Contains(nameKeyword, StringComparison.OrdinalIgnoreCase));

        if (targetDevice == null)
            return false;

        return await SetDefaultPlaybackDeviceAsync(targetDevice.Id);
    }

    /// <summary>
    /// 获取当前默认设备的音量 (0-100)
    /// </summary>
    public Task<double> GetVolumeAsync()
    {
        var device = _controller.DefaultPlaybackDevice;
        if (device == null) return Task.FromResult(0.0);
        return Task.FromResult(device.Volume);
    }

    /// <summary>
    /// 设置当前默认设备的音量 (0-100)
    /// </summary>
    public Task SetVolumeAsync(double volume)
    {
        var device = _controller.DefaultPlaybackDevice;
        if (device != null)
        {
            device.Volume = volume;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 当音频设备变化时触发
    /// </summary>
    public event EventHandler<AudioDeviceInfo>? DefaultDeviceChanged;

    public void Dispose()
    {
        if (_disposed) return;
        _controller.Dispose();
        _disposed = true;
    }
}
