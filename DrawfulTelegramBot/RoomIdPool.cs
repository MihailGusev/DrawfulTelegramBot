namespace DrawfulTelegramBot;

internal static class RoomIdPool
{
    private static readonly Queue<int> availableIds = new();

    static RoomIdPool() {
        var nums = Enumerable.Range(100, 1000).ToArray().Shuffle();
        availableIds = new Queue<int>(nums);
    }

    public static int GetNewId() => availableIds.Dequeue();

    public static void ReleaseId(int id) => availableIds.Enqueue(id);
}
