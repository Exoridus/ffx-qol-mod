namespace Fahrenheit.Mods.Qol;

internal sealed record FarplanePatch(int Rva, byte[] Expected, byte[] Replacement);

internal sealed class FarplanePatchPlan
{
    internal IReadOnlyList<FarplanePatch> Patches { get; }

    private FarplanePatchPlan(FarplanePatch[] patches) => Patches = patches;

    internal static FarplanePatchPlan Create(uint imageBase)
    {
        // Install code bodies before publishing their callers, and enable playback last.
        FarplanePatch[] patches =
        [
            new(0x0036ead0, Convert.FromHexString("686829B500E8260AECFF83C40433C0C368D829B500E8160AECFF83C40433C0C368482AB500E8060AECFF83C40433C0C368B82AB500E8F609ECFF59C3CCCCCCCC68302BB500E8E609ECFF83C40433C0C368A82BB500E8D609ECFF83C40433C0C3"), Convert.FromHexString("E8000000005883B837B59B0000743D56E8FB010000BE90010000E80132EDFF85C0751BE8E831EDFF85C07512E89F88F6FF85C0741683B8F006000000740D6A05E81B15CCFF83C4044E75CF5EC3CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC")),
            new(0x0070ba80, new byte[225], Convert.FromHexString("60E8000000005B8B45048D8B38C5E3FF39C8740E8D8B72BFE3FF39C80F85B20000008BB32A59150085F60F84A40000008B7D088B6D0C01FD8D860C1C000050FF93CE0600006A008B86641C00008D8E581C000039C8746985C074658B088B500401CA39F9722A39EA7726FF700C29CA015424048B48088B500C89510C894A088B8E4C1C0000890889864C1C000058EBBD39F9731239FA762329FA01142489FA29CA895004EB1539E973115289EA29CA015424045A29EA89500489288B400CEB8D582986001C00008D860C1C000050FF93D206000061E80000000058FFA03E050000")),
            new(0x0070bb70, new byte[12], Convert.FromHexString("51E8DA8AB4FF59E9F44AB4FF")),
            new(0x0048e14d, Convert.FromHexString("E88E0BEEFF"), Convert.FromHexString("E87E09EEFF")),
            new(0x00509a71, Convert.FromHexString("E86A52E6FF"), Convert.FromHexString("E85A50E6FF")),
            new(0x00543950, Convert.FromHexString("FF1598C0B000"), Convert.FromHexString("E82B811C0090")),
            new(0x00253dba, Convert.FromHexString("E8B1C8FFFF"), Convert.FromHexString("E8B17D4B00")),
            new(0x0036ebb4, Convert.FromHexString("74"), Convert.FromHexString("75")),
        ];
        uint delta = unchecked(imageBase - 0x400000u);
        foreach (int offset in new[] { 1, 17, 33, 49, 65, 81 }) Relocate(patches[0].Expected, offset, delta);
        Relocate(patches[5].Expected, 2, delta);
        return new(patches);
    }

    internal bool Verify(ReadOnlySpan<byte> image, out int mismatch)
    {
        foreach (var patch in Patches)
        {
            if (patch.Rva > image.Length || patch.Expected.Length > image.Length - patch.Rva
                || !image.Slice(patch.Rva, patch.Expected.Length).SequenceEqual(patch.Expected))
            {
                mismatch = patch.Rva;
                return false;
            }
        }
        mismatch = 0;
        return true;
    }

    private static void Relocate(byte[] bytes, int offset, uint delta)
        => BitConverter.TryWriteBytes(bytes.AsSpan(offset, 4), unchecked(BitConverter.ToUInt32(bytes, offset) + delta));
}
