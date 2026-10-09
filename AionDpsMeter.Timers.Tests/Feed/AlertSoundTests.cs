using AionDpsMeter.Timers.Feed;

namespace AionDpsMeter.Timers.Tests.Feed;

public class AlertSoundTests
{
    private static readonly FeedItem Shugo = new("shugo-festival", "k", FeedKind.Event, "Фестиваль шуго", null, FeedStatus.Upcoming, null, true, null);
    private static readonly FeedItem Boss = new("boss:2400800", "k", FeedKind.Boss, "Гартуа", null, FeedStatus.Upcoming, null, true, 2400800);

    [Fact]
    public void Each_event_has_its_own_sound_and_all_field_bosses_share_one()
    {
        var o = new TimersOptions();
        o.SetSound("shugo-festival", AlertSound.FromFile(@"C:\Sounds\shugo.mp3"));
        o.SetSound(TimersOptions.AllFieldBosses, AlertSound.System(WindowsSound.Asterisk));

        Assert.Equal(AlertSound.FromFile(@"C:\Sounds\shugo.mp3"), o.SoundFor(Shugo));
        Assert.Equal(AlertSound.System(WindowsSound.Asterisk), o.SoundFor(Boss));
    }

    [Fact]
    public void Without_a_choice_the_sound_is_the_standard_one()
    {
        Assert.Equal(AlertSound.Default, new TimersOptions().SoundFor(Shugo));
    }

    [Fact]
    public void Choosing_the_standard_sound_again_forgets_the_old_choice()
    {
        var o = new TimersOptions();
        o.SetSound("shugo-festival", AlertSound.System(WindowsSound.Beep));
        o.SetSound("shugo-festival", AlertSound.Default);
        Assert.Empty(o.Sounds);
    }

    /// <summary>Saved as plain strings in timers-settings.json, so a hand edit or an old file still reads.</summary>
    [Theory]
    [InlineData("system:asterisk")]
    [InlineData(@"file:C:\Sounds\shugo.mp3")]
    public void A_sound_round_trips_through_its_text(string text) =>
        Assert.Equal(text, AlertSound.Parse(text).ToString());

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("system:nope")]
    [InlineData("file:")]
    public void Unreadable_text_is_the_standard_sound(string text) =>
        Assert.Equal(AlertSound.Default, AlertSound.Parse(text));
}
