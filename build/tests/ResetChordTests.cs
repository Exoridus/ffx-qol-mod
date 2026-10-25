using Fahrenheit.Mods.Qol;
using Xunit;

public sealed class ResetChordTests
{
    [Fact]
    public void OnlyFreshStartWithBothShouldersTriggersOnce()
    {
        var chord = new ResetChord();
        Assert.False(chord.Update(true, true, true, true, false, false));
        Assert.True(chord.Update(true, true, true, true, true, false));
        Assert.False(chord.Update(true, true, true, true, true, false));
        Assert.False(chord.Update(true, true, true, true, false, false));
        Assert.True(chord.Update(true, true, true, true, true, false));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void BothShouldersMustBeDownWhenStartIsPressed(bool l1, bool r1)
    {
        var chord = new ResetChord();
        chord.Update(true, true, false, false, false, false);
        Assert.False(chord.Update(true, true, l1, r1, true, false));
        Assert.False(chord.Update(true, true, true, true, true, false));
    }

    [Fact]
    public void HeldStartDoesNotTriggerOnStartupRefocusOrReenable()
    {
        var chord = new ResetChord();
        Assert.False(chord.Update(true, true, true, true, true, false));
        chord.Update(true, false, true, true, false, false);
        Assert.False(chord.Update(true, true, true, true, true, false));
        chord.Update(false, true, true, true, false, false);
        Assert.False(chord.Update(true, true, true, true, true, false));
        chord.Update(true, true, true, true, false, false);
        Assert.True(chord.Update(true, true, true, true, true, false));
    }

    [Fact]
    public void DoesNotDuplicateAFrameworkResetForTheSamePress()
    {
        var chord = new ResetChord();
        chord.Update(true, true, true, true, false, false);
        Assert.False(chord.Update(true, true, true, true, true, true));
        Assert.False(chord.Update(true, true, true, true, true, false));
    }
}
