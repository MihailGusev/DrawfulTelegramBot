namespace DrawfulTelegramBot;

internal static class Extensions
{
    public static IList<T> Shuffle<T>(this IList<T> items) {
        var rnd = new Random();
        for (int i = 0; i < items.Count; i++) {
            int randomIndex = rnd.Next(items.Count);
            (items[i], items[randomIndex]) = (items[randomIndex], items[i]);
        }
        return items;
    }

    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> items, Action<T> action) {
        foreach (var item in items) {
            action(item);
        }
        return items;
    }
}
