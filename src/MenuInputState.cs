using System.Buffers.Binary;

namespace Fahrenheit.Mods.Qol;

internal static class MenuInputState
{
    internal static bool ShouldDiscard(int result) => result < 0;
    internal static bool IsPovUp(uint pov) => pov is 0 or 4500 or 31500;

    internal static bool MatchesPublishedState(ReadOnlySpan<byte> physical, ReadOnlySpan<byte> device)
    {
        if (physical.Length < 0x110 || device.Length < 0x3a8)
            throw new ArgumentException("Incomplete physical pad state.");
        if (!physical.Slice(0, 4).SequenceEqual(device.Slice(0x370, 4))
            || !physical.Slice(4, 4).SequenceEqual(device.Slice(0x37c, 4))
            || !physical.Slice(8, 4).SequenceEqual(device.Slice(0x374, 4))
            || !physical.Slice(0x14, 4).SequenceEqual(device.Slice(0x380, 4))) return false;
        for (int button = 0; button < 12; button++)
            if (device[0x398 + button] != physical[0x30 + button] >> 7) return false;
        uint directions = BinaryPrimitives.ReadUInt32LittleEndian(physical.Slice(0x20)) switch
        {
            0 => 0x00000001, 4500 => 0x00000101, 9000 => 0x00000100,
            13500 => 0x00010100, 18000 => 0x00010000, 22500 => 0x01010000,
            27000 => 0x01000000, 31500 => 0x01000001, _ => 0
        };
        return BinaryPrimitives.ReadUInt32LittleEndian(device.Slice(0x3a4)) == directions;
    }

    internal static void NeutralizeDevice(Span<byte> state)
    {
        if (state.Length < 0x3a8) throw new ArgumentException("Incomplete physical pad state.", nameof(state));
        foreach (int offset in new[] { 0x370, 0x374, 0x37c, 0x380 }) state.Slice(offset, 4).Clear();
        state.Slice(0x398, 16).Clear();
    }
}
