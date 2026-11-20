using AionDpsMeter.Core.GameData.Repositories;

namespace AionDpsMeter.Timers.Tests.Character;

public sealed class SkillNamesRuTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "skills-ru-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Russian_names_next_to_skills_json_replace_the_english_ones()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "skills.json"), """{ "11010000": "Rending Blow", "11020000": "Shield Smash" }""");
        File.WriteAllText(Path.Combine(dir, "skills.ru.json"), """{ "11010000": "Ожесточенный удар" }""");

        var repo = new SkillRepository();
        repo.Load(Path.Combine(dir, "skills.json"));

        Assert.Contains(repo.Skills.Values, s => s.Name == "Ожесточенный удар");
        Assert.Contains(repo.Skills.Values, s => s.Name == "Shield Smash");   // no Russian name: stays English
        Assert.DoesNotContain(repo.Skills.Values, s => s.Name == "Rending Blow");
    }

    [Fact]
    public void Without_the_russian_table_the_names_stay_as_they_are()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "skills.json"), """{ "11010000": "Rending Blow" }""");
        var repo = new SkillRepository();
        repo.Load(Path.Combine(dir, "skills.json"));
        Assert.Equal("Rending Blow", Assert.Single(repo.Skills.Values).Name);
    }

    public void Dispose()
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
    }
}
