// aion2-overlay fork: new file (see FORK_CHANGES.md).
namespace AionDpsMeter.UI.Pages
{
    /// <summary>Icons and colours of the event row in the main window (our own drawings, 24×24, currentColor). The size is
    /// in the SVG itself: markup inserted as a string gets none of the component's scoped CSS.</summary>
    public static class EventIcons
    {
        public static string Color(string icon) => icon switch
        {
            "rift" => "#b48cff",
            "shugo" => "#7cc8f0",
            "raid" => "#f5c542",
            "arena" => "#f07070",
            "reset" => "#7fdc9c",
            "boss" => "#ff9b6b",
            _ => "#c9d1d9",
        };

        /// <summary>A short name for the pill of the nearest event.</summary>
        public static string ShortName(string icon, string name) => icon switch
        {
            "rift" => "Разлом",
            "shugo" => "Шуго",
            "raid" => "Рейд",
            "arena" => "Арена",
            "reset" => "Сброс",
            _ => name,
        };

        public static string Svg(string icon) => icon switch
        {
            // a spiral of growing half-circles
            "rift" => Stroke("M11 12a1 1 0 0 1 2 0a2 2 0 0 1-4 0a3 3 0 0 1 6 0a4 4 0 0 1-8 0a5 5 0 0 1 10 0a6 6 0 0 1-12 0a7 7 0 0 1 14 0", 1.7),
            // a round face with pointed ears; the eyes are cut out
            "shugo" => Fill("M5 9.2 5.8 3.5l4.3 3.4Q12 6.4 13.9 6.9l4.3-3.4.8 5.7Q21 12 19.6 15.9 17.3 20.5 12 20.5 6.7 20.5 4.4 15.9 3 12 5 9.2ZM8.2 13a1.4 1.4 0 1 0 2.8 0 1.4 1.4 0 1 0-2.8 0ZM13 13a1.4 1.4 0 1 0 2.8 0 1.4 1.4 0 1 0-2.8 0Z"),
            // spread wings around a narrow body
            "raid" => Fill("M12 5.5 13.4 11 12 19 10.6 11ZM10.2 10.6Q6.4 5.6 1.8 6.6 4.6 8.6 4.8 12.2 7.4 11 9.9 13.4ZM13.8 10.6Q17.6 5.6 22.2 6.6 19.4 8.6 19.2 12.2 16.6 11 14.1 13.4Z"),
            // two crossed swords with guards
            "arena" => Stroke("M4.5 4.5 15 15M12.6 17.2l4.6-4.6M15.8 15.8l3.4 3.4M19.5 4.5 9 15M6.8 12.6l4.6 4.6M8.2 15.8l-3.4 3.4", 2),
            // a circular arrow
            "reset" => Stroke("M19 12a7 7 0 1 1-2.05-4.95M19 4.2v3.6h-3.6", 2.2),
            // a horned skull: a field boss
            "boss" => Fill("M12 4C7.6 4 5 7 5 10.6c0 2 .9 3.6 2.3 4.6V18a1 1 0 0 0 1 1h1.2v-1.6h1.3V19h2.4v-1.6h1.3V19h1.2a1 1 0 0 0 1-1v-2.8c1.4-1 2.3-2.6 2.3-4.6C19 7 16.4 4 12 4ZM9.2 13.4a1.6 1.6 0 1 1 0-3.2 1.6 1.6 0 0 1 0 3.2Zm5.6 0a1.6 1.6 0 1 1 0-3.2 1.6 1.6 0 0 1 0 3.2ZM3.5 3.5l2.8 3.3-1 1.2L2.7 5Zm17 0L21.3 5l-2.6 3-1-1.2Z"),
            _ => Fill("M12 3l2.6 5.6 6.1.7-4.5 4.2 1.2 6L12 16.6l-5.4 2.9 1.2-6-4.5-4.2 6.1-.7Z"),
        };

        private static string Fill(string d) =>
            $"<svg width=\"15\" height=\"15\" viewBox=\"0 0 24 24\" fill=\"currentColor\" fill-rule=\"evenodd\" aria-hidden=\"true\"><path d=\"{d}\"/></svg>";

        private static string Stroke(string d, double width) =>
            $"<svg width=\"15\" height=\"15\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"{width.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"{d}\"/></svg>";
    }
}
