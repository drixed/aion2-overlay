// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.IO;
using System.Media;
using System.Windows.Media;
using AionDpsMeter.Timers.Feed;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>Plays an alert's sound on the UI thread: a Windows sound or the user's .wav/.mp3. A file that is gone
    /// or cannot be played falls back to the standard sound, so an alert is never silent by mistake.</summary>
    public sealed class AlertSoundPlayer(ILogger<AlertSoundPlayer> logger)
    {
        /// <summary>Kept in a field: a MediaPlayer that is collected stops mid-sound.</summary>
        private MediaPlayer? media;

        public void Play(AlertSound sound)
        {
            try
            {
                if (sound.File is { } file)
                {
                    if (File.Exists(file))
                    {
                        media?.Close();
                        media = new MediaPlayer();
                        media.MediaFailed += (_, e) =>
                        {
                            logger.LogWarning(e.ErrorException, "Alert sound {File} could not be played", file);
                            SystemSounds.Exclamation.Play();
                        };
                        media.Open(new Uri(file, UriKind.Absolute));
                        media.Play();
                        return;
                    }
                    logger.LogWarning("Alert sound {File} is missing; playing the standard one", file);
                }
                Of(sound.Windows ?? WindowsSound.Exclamation).Play();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Alert sound failed");
            }
        }

        private static SystemSound Of(WindowsSound sound) => sound switch
        {
            WindowsSound.Asterisk => SystemSounds.Asterisk,
            WindowsSound.Beep => SystemSounds.Beep,
            WindowsSound.Hand => SystemSounds.Hand,
            WindowsSound.Question => SystemSounds.Question,
            _ => SystemSounds.Exclamation,
        };
    }
}
