using AionDpsMeter.Services.Services.Session;

namespace AionDpsMeter.UI.ViewModels.History
{
    public sealed class HistoryEntryViewModel : ViewModelBase
    {
        public HistorySessionListItem Session { get; }

        public HistoryEntryViewModel(HistorySessionListItem session)
        {
            Session = session;
        }

        public Guid SessionId => Session.SessionId;

        public string TargetName    => string.IsNullOrEmpty(Session.TargetName)
            ? $"Mob #{Session.TargetId}"
            : Session.TargetName;

        public string DateDisplay   => Session.SessionEnd.ToString("dd.MM  HH:mm:ss");
        public string Duration      => DamageFormatter.FormatDuration(Session.Duration);
        public bool   IsCompleted   => Session.State == SessionState.Completed;
        public string StateDisplay  => IsCompleted ? "?" : "?";
        // aion2-overlay fork: the dot shows how the fight ended — green killed, red not killed, grey HP unknown
        public string StateColor    => !IsCompleted ? "#4EC9B0" : AionDpsMeter.Timers.Records.FightOutcomes.Of(Session.TargetHpTotal, Session.TotalDamage) switch
        {
            AionDpsMeter.Timers.Records.FightOutcome.Killed => "#7FDC9C",
            AionDpsMeter.Timers.Records.FightOutcome.NotKilled => "#FF6B6B",
            _ => "#888888",
        };
        public string OutcomeText   => IsCompleted ? AionDpsMeter.Timers.Records.FightOutcomes.Text(Session.TargetHpTotal, Session.TotalDamage) : "Бой идёт"; // aion2-overlay fork
        public int PlayerCount => Session.PlayerCount;

        public string TotalDamageDisplay => DamageFormatter.Format(Session.TotalDamage);
    }
}
