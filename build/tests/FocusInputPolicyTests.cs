using Fahrenheit.Mods.Qol;
using Xunit;

public sealed class FocusInputPolicyTests
{
    [Fact]
    public void RejectsUnreadyAndUnknownVersionWithoutObservingFocus()
    {
        var module = new FocusInputModule();
        Assert.False(module.TryGetVrrInputPolicy(1, out long generation, out bool neutralize));
        Assert.Equal(0, generation);
        Assert.False(neutralize);
        module.Ready();
        Assert.False(module.TryGetVrrInputPolicy(2, out generation, out neutralize));
        Assert.Equal(0, generation);
        Assert.False(neutralize);
        Assert.Equal(0, module.FocusReads);
    }

    [Fact]
    public void QueryDoesNotAdvanceGraceAndObservesOutAndBackFocus()
    {
        var module = new FocusInputModule();
        module.Ready();
        Assert.True(module.TryGetVrrInputPolicy(1, out long first, out bool neutralize));
        Assert.False(neutralize);
        module.Focused = false;
        Assert.True(module.TryGetVrrInputPolicy(1, out long lost, out neutralize));
        Assert.True(neutralize);
        Assert.True(lost > first);
        module.Focused = true;
        Assert.True(module.TryGetVrrInputPolicy(1, out long regained, out neutralize));
        Assert.False(neutralize);
        Assert.True(regained > lost);
        module.Focused = false;
        Assert.True(module.Scan());
        Assert.Equal(3, module.Grace);
        module.Focused = true;
        for (int i = 0; i < 5; i++)
        {
            Assert.True(module.TryGetVrrInputPolicy(1, out _, out neutralize));
            Assert.True(neutralize);
            Assert.Equal(3, module.Grace);
        }
    }

    [Fact]
    public void ScanPreservesDecrementBeforeNeutralizationAndGeneration()
    {
        var module = new FocusInputModule { Focused = false };
        module.Ready();
        Assert.True(module.Scan());
        Assert.Equal(3, module.Grace);
        module.Focused = true;
        Assert.True(module.Scan());
        Assert.Equal(2, module.Grace);
        module.TryGetVrrInputPolicy(1, out long atTwo, out _);
        Assert.True(module.Scan());
        Assert.Equal(1, module.Grace);
        module.TryGetVrrInputPolicy(1, out long atOne, out _);
        Assert.True(atOne > atTwo);
        Assert.False(module.Scan());
        Assert.Equal(0, module.Grace);
        module.TryGetVrrInputPolicy(1, out long atZero, out bool neutralize);
        Assert.False(neutralize);
        Assert.True(atZero > atOne);
        Assert.False(module.Scan());
        module.TryGetVrrInputPolicy(1, out long stable, out _);
        Assert.Equal(atZero, stable);
    }

    [Fact]
    public void ToggleChangesInvalidateEvenWhileFocusedOrDuringGrace()
    {
        var module = new FocusInputModule();
        module.Ready();
        module.TryGetVrrInputPolicy(1, out long first, out _);
        module.Enabled = false;
        module.TryGetVrrInputPolicy(1, out long disabled, out bool neutralize);
        Assert.False(neutralize);
        Assert.True(disabled > first);
        module.Enabled = true;
        module.TryGetVrrInputPolicy(1, out long restored, out _);
        Assert.True(restored > disabled);
        module.Focused = false;
        Assert.True(module.Scan());
        module.Enabled = false;
        module.TryGetVrrInputPolicy(1, out long off, out neutralize);
        Assert.False(neutralize);
        Assert.Equal(3, module.Grace);
        module.Enabled = true;
        module.TryGetVrrInputPolicy(1, out long on, out neutralize);
        Assert.True(neutralize);
        Assert.True(on > off);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void QueriesOnEitherSideOfScanDetectChangedPolicy(bool queryBeforeScan)
    {
        var module = new FocusInputModule { Focused = false };
        module.Ready();
        module.Scan();
        module.Focused = true;
        module.Scan();
        module.Scan();
        Assert.Equal(1, module.Grace);
        module.TryGetVrrInputPolicy(1, out long before, out bool wasNeutral);
        Assert.True(wasNeutral);
        if (!queryBeforeScan) module.Scan();
        module.TryGetVrrInputPolicy(1, out long queried, out bool neutralize);
        if (queryBeforeScan) module.Scan();
        module.TryGetVrrInputPolicy(1, out long after, out bool afterNeutral);
        Assert.False(afterNeutral);
        Assert.True(after > before);
        Assert.Equal(queryBeforeScan, neutralize);
        Assert.Equal(queryBeforeScan ? before : after, queried);
    }
}
