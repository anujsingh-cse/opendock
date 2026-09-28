using System.Runtime.InteropServices;

namespace OpenDock.Interop;

/// <summary>
/// Master volume control via the CoreAudio MMDevice API, hand-declared COM.
/// Only the vtable entries we actually call are declared, in exact order.
/// Everything is best-effort: no audio device, no problem.
/// </summary>
public sealed class VolumeControl
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator;

    private enum EDataFlow
    {
        Render = 0,
    }

    private enum ERole
    {
        Multimedia = 1,
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, uint dwStateMask, out nint ppDevices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate([MarshalAs(UnmanagedType.LPStruct)] Guid iid, uint dwClsCtx, nint pActivationParams,
            [MarshalAs(UnmanagedType.Interface)] out IAudioEndpointVolume ppInterface);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74070229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(nint pNotify);
        int UnregisterControlChangeNotify(nint pNotify);
        int GetChannelCount(out uint pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, nint pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, nint pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, nint pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, nint pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, nint pguidEventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    private static readonly Guid IID_IAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74070229A");
    private const uint CLSCTX_ALL = 23;

    private static IAudioEndpointVolume? GetEndpoint()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device) != 0)
                return null;
            if (device.Activate(IID_IAudioEndpointVolume, CLSCTX_ALL, nint.Zero, out var volume) != 0)
                return null;
            return volume;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Master volume 0..100, or null when unavailable.</summary>
    public static int? GetVolume()
    {
        try
        {
            var endpoint = GetEndpoint();
            if (endpoint is null)
                return null;
            return endpoint.GetMasterVolumeLevelScalar(out float level) != 0
                ? null
                : (int)Math.Round(level * 100);
        }
        catch
        {
            return null;
        }
    }

    public static void SetVolume(int percent)
    {
        try
        {
            GetEndpoint()?.SetMasterVolumeLevelScalar(
                Math.Clamp(percent, 0, 100) / 100f, nint.Zero);
        }
        catch { /* best effort */ }
    }

    public static bool? GetMute()
    {
        try
        {
            var endpoint = GetEndpoint();
            if (endpoint is null)
                return null;
            return endpoint.GetMute(out bool mute) != 0 ? null : mute;
        }
        catch
        {
            return null;
        }
    }

    public static void SetMute(bool mute)
    {
        try { GetEndpoint()?.SetMute(mute, nint.Zero); }
        catch { /* best effort */ }
    }
}
