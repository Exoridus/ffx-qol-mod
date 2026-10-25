namespace Fahrenheit.Mods.Qol;

/// <summary>Restores the Farplane background, effect preparation and movie teardown.</summary>
[FhLoad(FhGameId.FFX)]
public sealed unsafe class FarplaneModule : FhModule
{
    private readonly PatchJournal _patches = new();

    public override bool init(FhModContext mod_context, FileStream global_state_file)
    {
        if (!NativeBuild.TryValidate(out string error))
        {
            _logger.Error(error);
            return false;
        }
        using var process = Process.GetCurrentProcess();
        var image = process.MainModule!;
        var plan = FarplanePatchPlan.Create(unchecked((uint)(nuint)image.BaseAddress));
        if (!plan.Verify(new ReadOnlySpan<byte>((void*)image.BaseAddress, image.ModuleMemorySize), out int mismatch))
        {
            _logger.Error($"[QoL] Farplane repair refused: unexpected bytes at RVA 0x{mismatch:X}.");
            return false;
        }
        try
        {
            // The loaded image is already relocated; PE relocation/header edits are unnecessary here.
            foreach (var patch in plan.Patches) _patches.Write(patch.Rva, patch.Replacement);
        }
        catch (Exception exception)
        {
            _patches.RestoreAll();
            _logger.Error($"[QoL] Farplane repair rolled back: {exception.Message}");
            return false;
        }
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _patches.RestoreAll();
        _logger.Info("[QoL] Farplane repair active: background start, sorted effects and teardown cleanup; eight in-memory patches.");
        return true;
    }
}
