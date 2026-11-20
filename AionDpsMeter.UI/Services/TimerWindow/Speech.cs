// aion2-overlay fork: new file (see FORK_CHANGES.md).
namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>Windows' own voices (SAPI, no extra package): "назвать вслух" for the buff trackers. Voices are added in
    /// Windows Settings → Time and language → Speech; a Russian one is "Microsoft Irina".</summary>
    public static class Speech
    {
        private static dynamic? voice;

        public static IReadOnlyList<string> Voices()
        {
            try
            {
                var tokens = (voice ??= Create()).GetVoices();
                var names = new List<string>();
                for (var i = 0; i < (int)tokens.Count; i++) names.Add((string)tokens.Item(i).GetDescription());
                return names;
            }
            catch (Exception)
            {
                return [];
            }
        }

        /// <summary>Says the text without waiting, cutting off what is still being said.</summary>
        public static void Say(string text, string? voiceName, int volume)
        {
            try
            {
                voice ??= Create();
                if (!string.IsNullOrEmpty(voiceName))
                {
                    var tokens = voice.GetVoices();
                    for (var i = 0; i < (int)tokens.Count; i++)
                        if ((string)tokens.Item(i).GetDescription() == voiceName) { voice.Voice = tokens.Item(i); break; }
                }
                voice.Volume = Math.Clamp(volume, 0, 100);
                voice.Speak(text, 1 | 2); // SVSFlagsAsync | SVSFPurgeBeforeSpeak
            }
            catch (Exception)
            {
                // no voice on this Windows: the sound still plays
            }
        }

        private static dynamic Create() =>
            Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpVoice") ?? throw new InvalidOperationException("No SAPI"))!;
    }
}
