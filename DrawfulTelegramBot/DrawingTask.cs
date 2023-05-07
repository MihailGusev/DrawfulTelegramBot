namespace DrawfulTelegramBot;

internal class DrawingTask
{
    private static readonly Queue<string> _tasks;
    public readonly string text;

    static DrawingTask() {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tasks.txt");
        var lines = File.ReadLines(path).Select(l => l.Trim().ToLower()).ToList().Shuffle();
        _tasks = new Queue<string>(lines);
    }

    public bool isFinished;
    public List<DrawingTaskGuessOption> guessOptions = new();

    public DrawingTask() {
        text = _tasks.Dequeue();
        _tasks.Enqueue(text);
        guessOptions.Add(new DrawingTaskGuessOption(text, null));
    }
}
