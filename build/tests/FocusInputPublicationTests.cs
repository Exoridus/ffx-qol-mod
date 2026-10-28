using Fahrenheit.Mods.Qol;
using Xunit;

public sealed class FocusInputPublicationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BindingSupportsEitherInitOrderAndForwardsAroundNative(bool readyFirst)
    {
        var module = new FocusInputModule { Focused = false };
        var calls = new List<string>();
        if (readyFirst) module.Ready();
        Assert.True(module.TryBindVrrInputPublication(1, new object(), (generation, neutralized) =>
        {
            Assert.False(module.PolicyLockHeld);
            Assert.True(generation > 0);
            Assert.True(neutralized);
            Assert.True(module.PadNeutral);
            calls.Add("before");
        }, () => { Assert.False(module.PolicyLockHeld); calls.Add("after"); }));
        module.Ready();
        module.Native = () => { Assert.False(module.PolicyLockHeld); calls.Add("native"); };
        module.Publish();
        Assert.Equal(new[] { "before", "native", "after" }, calls);
        Assert.Equal(1, module.FocusReads);
        Assert.Equal(1, module.NativeCalls);
        Assert.Equal(3, module.Grace);
    }

    [Fact]
    public void BindingIdentityIsStrictAndUnsupportedVersionRefuses()
    {
        var module = new FocusInputModule();
        object owner = new();
        Action<long, bool> before = (_, _) => { };
        Action after = () => { };
        Assert.False(module.TryBindVrrInputPublication(2, owner, before, after));
        Assert.True(module.TryBindVrrInputPublication(1, owner, before, after));
        Assert.True(module.TryBindVrrInputPublication(1, owner, before, after));
        Assert.False(module.TryBindVrrInputPublication(1, new object(), before, after));
        Assert.False(module.TryBindVrrInputPublication(1, owner, (_, _) => { }, after));
        Assert.False(module.TryBindVrrInputPublication(1, owner, before, () => { }));
    }

    [Fact]
    public void UnreadyAndAbsentBindingsForwardStandaloneWithoutCallbacks()
    {
        var module = new FocusInputModule();
        int calls = 0;
        module.Publish();
        Assert.True(module.TryBindVrrInputPublication(1, new object(), (_, _) => calls++, () => calls++));
        module.Publish();
        Assert.Equal(2, module.NativeCalls);
        Assert.Equal(0, calls);
        module.Ready();
        module.Publish();
        Assert.Equal(2, calls);
        Assert.Equal(3, module.NativeCalls);
    }

    [Fact]
    public void BeforeFailureStillForwardsAndCleansUpThenRevokesQueryReadiness()
    {
        var module = new FocusInputModule { Focused = false };
        module.Ready();
        int before = 0, after = 0;
        module.TryBindVrrInputPublication(1, new object(), (_, _) => { before++; throw new Exception("before"); }, () => after++);
        module.Publish();
        Assert.True(module.PadNeutral);
        Assert.Equal(1, module.NativeCalls);
        Assert.Equal(1, after);
        Assert.False(module.TryGetVrrInputPolicy(1, out _, out _));
        module.Publish();
        Assert.Equal(2, module.NativeCalls);
        Assert.Equal(1, before);
        Assert.Equal(1, after);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AfterFailureDoesNotMaskNativeFailureOrRetry(bool failNative)
    {
        var module = new FocusInputModule();
        module.Ready();
        var native = new InvalidOperationException("native");
        module.Native = () => { if (failNative) throw native; };
        int after = 0;
        module.TryBindVrrInputPublication(1, new object(), (_, _) => { }, () => { after++; throw new Exception("after"); });
        if (failNative) Assert.Same(native, Assert.Throws<InvalidOperationException>(() => module.Publish()));
        else module.Publish();
        Assert.Equal(1, module.NativeCalls);
        Assert.Equal(1, after);
        Assert.False(module.TryGetVrrInputPolicy(1, out _, out _));
    }

    [Fact]
    public void ReentryRevokesCooperationWithoutSkippingEitherNativeCall()
    {
        var module = new FocusInputModule();
        module.Ready();
        int before = 0, after = 0;
        module.TryBindVrrInputPublication(1, new object(), (_, _) =>
        {
            before++;
            module.Publish();
        }, () => after++);
        module.Publish();
        Assert.Equal(2, module.NativeCalls);
        Assert.Equal(1, before);
        Assert.Equal(1, after);
        Assert.False(module.TryGetVrrInputPolicy(1, out _, out _));
    }

    [Fact]
    public void NativeFailureStillInvokesAfterWithoutChangingException()
    {
        var module = new FocusInputModule();
        module.Ready();
        var failure = new InvalidOperationException("native");
        int after = 0;
        module.TryBindVrrInputPublication(1, new object(), (_, _) => { }, () => after++);
        module.Native = () => throw failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => module.Publish()));
        Assert.Equal(1, module.NativeCalls);
        Assert.Equal(1, after);
    }

    [Fact]
    public void PublishedDecisionAndGenerationComeFromActualScanNotLaterQuery()
    {
        var module = new FocusInputModule { Focused = false };
        module.Ready();
        var decisions = new List<bool>();
        var generations = new List<long>();
        module.TryBindVrrInputPublication(1, new object(), (generation, neutralized) =>
        { generations.Add(generation); decisions.Add(neutralized); }, () => { });
        module.Publish();
        module.Focused = true;
        module.Publish();
        module.Publish();
        module.Publish();
        Assert.Equal(new[] { true, true, true, false }, decisions);
        Assert.True(generations.Zip(generations.Skip(1), (left, right) => left < right).All(value => value));
        Assert.Equal(0, module.Grace);
        Assert.Equal(4, module.NativeCalls);
    }
}
