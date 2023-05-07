namespace DrawfulTelegramBot;

internal class DrawingTask
{
    private static readonly Queue<string> _tasks;

    static DrawingTask() {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tasks.txt");
        var lines = File.ReadAllLines(path).Shuffle();
        _tasks = new Queue<string>(lines);
    }

    public string text;
    public List<DrawTaskAnswer> answers = new();
    public DrawingTask() {
        text = GetTask();
    }

    private static string GetTask() {
        var task = _tasks.Dequeue();
        _tasks.Enqueue(task);
        return task;
    }
}

internal class DrawTaskAnswer
{
    public string text;
    public Player player;
}
