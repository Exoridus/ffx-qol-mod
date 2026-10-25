using Fahrenheit.Mods.Qol;
using Xunit;

public sealed class NativeBuildTests
{
    [Fact]
    public void AcceptsExactExecutableIdentity()
    {
        Assert.True(NativeBuild.IsSupportedIdentity(10687744,
            "0537b2a1047f3266e73495cd4e35f63f0777f4231d417699f979954686da686d"));
        Assert.True(NativeBuild.IsSupportedIdentity(NativeBuild.ImageLength, NativeBuild.ImageSha256.ToUpperInvariant()));
    }

    [Theory]
    [InlineData(10687743, "0537b2a1047f3266e73495cd4e35f63f0777f4231d417699f979954686da686d")]
    [InlineData(10687744, "1537b2a1047f3266e73495cd4e35f63f0777f4231d417699f979954686da686d")]
    [InlineData(10687744, "")]
    public void RejectsUnknownExecutableIdentity(long length, string hash)
        => Assert.False(NativeBuild.IsSupportedIdentity(length, hash));

    [Fact]
    public void RejectsTestHostBeforeNativeIntegration()
    {
        Assert.False(NativeBuild.TryValidate(out string error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void UsesReviewedCurrentBuildBindings()
    {
        Assert.Equal((nint)0xF30288, EngineAddresses.PadRecordBase);
        Assert.Equal((nint)0x472EF0, EngineAddresses.AtelEventSetUp);
        Assert.Equal((nint)0x479760, EngineAddresses.AtelGetEventName);
        Assert.Equal((nint)0x4893E0, EngineAddresses.TkScanControler);
        Assert.Equal((nint)0x2F7340, EngineAddresses.SpdCtrlScaleElapsedTime);
        Assert.Equal((nint)0x45D4E0, EngineAddresses.AtelPressedButtons);
    }
}
