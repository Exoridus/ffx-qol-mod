namespace Fahrenheit.Mods.Qol;

internal static class MenuInputState
{
    internal static bool ShouldDiscard(int result) => result < 0;
    internal static bool IsPovUp(uint pov) => pov is 0 or 4500 or 31500;

    internal static void NeutralizeDevice(Span<byte> state)
    {
        if (state.Length < 0x3a8) throw new ArgumentException("Incomplete physical pad state.", nameof(state));
        foreach (int offset in new[] { 0x370, 0x374, 0x37c, 0x380 }) state.Slice(offset, 4).Clear();
        state.Slice(0x398, 16).Clear();
    }
}
