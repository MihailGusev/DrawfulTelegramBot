namespace DrawfulTelegramBot;

internal static class RoomIdPool
{
    private static readonly Queue<int> availableIds = new();
    private static HashSet<int> unavailableIds = new();

    static RoomIdPool() {
        var nums = Enumerable.Range(100, 900).ToArray().Shuffle();
        availableIds = new Queue<int>(nums);
    }

    public static int GetNewId() {
        var id = availableIds.Dequeue();
        unavailableIds.Add(id);
        return id;
    }

    public static void ReleaseId(int id) => availableIds.Enqueue(id);

    public static void ReleaseAllIds() {
        foreach (var id in unavailableIds) {
            availableIds.Enqueue(id);
        }
        unavailableIds.Clear();
    }
}
