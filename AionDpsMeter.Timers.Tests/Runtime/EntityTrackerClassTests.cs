using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.Services.Entity;

namespace AionDpsMeter.Timers.Tests.Runtime;

public class EntityTrackerClassTests
{
    private static readonly CharacterClass Gladiator = new() { Id = 11, Name = "Gladiator" };
    private static readonly CharacterClass Chanter = new() { Id = 18, Name = "Chanter" };
    private static readonly CharacterClass Templar = new() { Id = 12, Name = "Templar" };

    [Fact]
    public void Another_classes_buff_hitting_for_a_player_does_not_recolour_their_row()
    {
        // 24.11: rows went "like a rainbow": a Chanter's Protection Circle or a Templar's seal ticking under a Gladiator's id
        // brought the Chanter's or Templar's skill code, and the last skill code named the class
        var entities = new EntityTracker();
        for (var i = 0; i < 20; i++) entities.GetOrCreateSessionPlayer(500, Gladiator);
        entities.GetOrCreateSessionPlayer(500, Chanter);
        Assert.Equal(11, entities.GetPlayerEntity(500)!.CharacterClass!.Id);
        entities.GetOrCreateSessionPlayer(500, Templar);
        entities.GetOrCreateSessionPlayer(500, Chanter);
        Assert.Equal(11, entities.GetPlayerEntity(500)!.CharacterClass!.Id);
    }

    [Fact]
    public void The_class_used_most_wins_even_when_a_wrong_one_came_first()
    {
        var entities = new EntityTracker();
        entities.GetOrCreateSessionPlayer(501, Chanter);    // a buff ticked before the player's own first hit
        entities.GetOrCreateSessionPlayer(501, Gladiator);
        entities.GetOrCreateSessionPlayer(501, Gladiator);
        Assert.Equal(11, entities.GetPlayerEntity(501)!.CharacterClass!.Id);
    }
}
