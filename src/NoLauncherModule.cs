namespace Fahrenheit.Mods.Qol;

/// <summary>
///     Stops the engine reopening FFX&amp;X-2_LAUNCHER.exe when the game window is closed.
/// </summary>
[FhLoad(FhGameId.FFX)]
public unsafe sealed class NoLauncherModule : FhModule
{
    // ShellExecuteW returns an HINSTANCE; anything above 32 means success.
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    private delegate nint d_shell_execute_w(
        nint hwnd,
        [MarshalAs(UnmanagedType.LPWStr)] string? operation,
        [MarshalAs(UnmanagedType.LPWStr)] string? file,
        [MarshalAs(UnmanagedType.LPWStr)] string? parameters,
        [MarshalAs(UnmanagedType.LPWStr)] string? directory,
        int show_cmd);

    private long _suppressed;

    public override bool init(FhModContext mod_context, FileStream global_state_file)
    {
        if (!new FhMethodHandle<d_shell_execute_w>(new FhMethodLocation("shell32.dll", "ShellExecuteW")).hook(this, h_shell_execute_w))
        {
            _logger.Error("[QoL] Could not hook ShellExecuteW; the launcher will reopen on close.");
            return false;
        }

        _logger.Info("[QoL] Launcher relaunch suppression active.");
        return true;
    }

    private nint h_shell_execute_w(nint hwnd, string? operation, string? file, string? parameters, string? directory, int show_cmd)
    {
        if (file != null && file.EndsWith("LAUNCHER.exe", StringComparison.OrdinalIgnoreCase))
        {
            _suppressed++;
            _logger.Info($"[QoL] Suppressed launcher relaunch (count={_suppressed}).");

            // 33 is the canonical fake-success value for a call deliberately not made.
            return 33;
        }

        return new FhMethodHandle<d_shell_execute_w>(new FhMethodLocation("shell32.dll", "ShellExecuteW"))
            .chain_from(h_shell_execute_w).fnptr!(hwnd, operation, file, parameters, directory, show_cmd);
    }
}
