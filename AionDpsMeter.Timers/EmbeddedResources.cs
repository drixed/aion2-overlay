namespace AionDpsMeter.Timers;

internal static class EmbeddedResources
{
    public static string Read(string name)
    {
        using var stream = typeof(EmbeddedResources).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"{name} is not embedded in AionDpsMeter.Timers");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
