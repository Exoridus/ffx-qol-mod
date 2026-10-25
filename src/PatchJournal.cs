namespace Fahrenheit.Mods.Qol;

internal sealed unsafe class PatchJournal
{
    private readonly List<(nint Address, byte[] Original)> _entries = [];

    internal void Write(nint rva, ReadOnlySpan<byte> replacement)
    {
        nint address = (nint)FhUtil.ptr_at<byte>(rva);
        byte[] original = new ReadOnlySpan<byte>((void*)address, replacement.Length).ToArray();
        _entries.Add((address, original));
        WriteBytes(address, replacement);
    }

    internal void RestoreAll()
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
            WriteBytes(_entries[i].Address, _entries[i].Original);
        _entries.Clear();
    }

    private static void WriteBytes(nint address, ReadOnlySpan<byte> bytes)
    {
        if (!VirtualProtect(address, (nuint)bytes.Length, 0x40, out uint previous))
            throw new InvalidOperationException($"Cannot make code writable at 0x{address:X}.");
        try
        {
            bytes.CopyTo(new Span<byte>((void*)address, bytes.Length));
            if (!FlushInstructionCache((nint)(-1), address, (nuint)bytes.Length))
                throw new InvalidOperationException($"Cannot flush patched code at 0x{address:X}.");
        }
        finally
        {
            if (!VirtualProtect(address, (nuint)bytes.Length, previous, out _))
                throw new InvalidOperationException($"Cannot restore code protection at 0x{address:X}.");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualProtect(nint address, nuint size, uint protection, out uint previous);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushInstructionCache(nint process, nint address, nuint size);
}
