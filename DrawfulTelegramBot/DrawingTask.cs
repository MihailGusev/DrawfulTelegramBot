using System.Text;

namespace DrawfulTelegramBot;

internal class DrawingTask
{
    const string TASK_FILE_PATH = "D:\\tasks.txt";
    private static readonly Queue<string> _tasks;
    public readonly string text;

    public List<DrawingTaskGuessOption> guessOptions = new();

    static DrawingTask() {
        var lines = File.ReadLines(TASK_FILE_PATH, Encoding.UTF8).Select(l => l.Trim().ToLower()).ToArray().Shuffle();
        _tasks = new Queue<string>(lines);
    }

    public DrawingTask() {
        text = _tasks.Dequeue();
        _tasks.Enqueue(text);
        guessOptions.Add(new DrawingTaskGuessOption(text, null));
    }

    public void ShuffleOptions() {
        guessOptions.Shuffle();
    }
}
