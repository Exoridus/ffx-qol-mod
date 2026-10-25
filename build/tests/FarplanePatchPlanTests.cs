using Fahrenheit.Mods.Qol;
using Xunit;

public sealed class FarplanePatchPlanTests
{
    [Fact]
    public void IncludesStartEffectsAndBothTeardownRepairs()
    {
        var plan = FarplanePatchPlan.Create(0x400000);
        Assert.Equal(8, plan.Patches.Count);
        Assert.Equal(new[] { 0x36ead0, 0x70ba80, 0x70bb70, 0x48e14d, 0x509a71, 0x543950, 0x253dba, 0x36ebb4 },
            plan.Patches.Select(p => p.Rva));
        Assert.All(plan.Patches, p => Assert.Equal(p.Expected.Length, p.Replacement.Length));
        Assert.Equal(new byte[] { 0x74 }, plan.Patches[^1].Expected);
        Assert.Equal(new byte[] { 0x75 }, plan.Patches[^1].Replacement);
    }

    [Theory]
    [InlineData(0x400000u)]
    [InlineData(0x1000000u)]
    [InlineData(0x50000000u)]
    public void RelocatesOriginalGuardsButKeepsReplacementPositionIndependent(uint imageBase)
    {
        var stock = FarplanePatchPlan.Create(0x400000);
        var plan = FarplanePatchPlan.Create(imageBase);
        var shutdown = plan.Patches.Single(p => p.Rva == 0x36ead0);
        var original = stock.Patches.Single(p => p.Rva == shutdown.Rva);
        foreach (int offset in new[] { 1, 17, 33, 49, 65, 81 })
            Assert.Equal(unchecked(BitConverter.ToUInt32(original.Expected, offset) + imageBase - 0x400000),
                BitConverter.ToUInt32(shutdown.Expected, offset));
        Assert.Equal(unchecked(0xb0c098u + imageBase - 0x400000),
            BitConverter.ToUInt32(plan.Patches.Single(p => p.Rva == 0x543950).Expected, 2));
        for (int i = 0; i < plan.Patches.Count; i++)
            Assert.Equal(stock.Patches[i].Replacement, plan.Patches[i].Replacement);
    }

    [Fact]
    public void EffectsThunkPreservesThisAndSortsBeforeOriginalDraw()
    {
        var plan = FarplanePatchPlan.Create(0x400000);
        var thunk = plan.Patches.Single(p => p.Rva == 0x70bb70);
        Assert.Equal(0x51, thunk.Replacement[0]);
        Assert.Equal(0x59, thunk.Replacement[6]);
        Assert.Equal(0xe8, thunk.Replacement[1]);
        Assert.Equal(0xe9, thunk.Replacement[7]);
        Assert.Equal(0x254650, thunk.Rva + 6 + BitConverter.ToInt32(thunk.Replacement, 2));
        Assert.Equal(0x250670, thunk.Rva + 12 + BitConverter.ToInt32(thunk.Replacement, 8));
    }

    [Fact]
    public void PreflightRejectsAnyChangedRangeBeforeWriting()
    {
        var plan = FarplanePatchPlan.Create(0x1000000);
        Assert.NotEmpty(plan.Patches);
        var image = new byte[0x70c000];
        foreach (var patch in plan.Patches) patch.Expected.CopyTo(image, patch.Rva);
        Assert.True(plan.Verify(image, out _));
        foreach (var patch in plan.Patches)
        {
            image[patch.Rva] ^= 1;
            Assert.False(plan.Verify(image, out int mismatch));
            Assert.Equal(patch.Rva, mismatch);
            image[patch.Rva] ^= 1;
        }
        Assert.False(plan.Verify(image.AsSpan(0, 100), out _));
    }
}
