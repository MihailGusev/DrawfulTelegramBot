using System.Text;

namespace DrawfulTelegramBot;

internal static class DrawingTaskPool
{
    private const string TASK_FILE_PATH = "D:\\tasks.txt";

    private static readonly Queue<string> _tasks;

    static DrawingTaskPool() {
        var lines = File.ReadLines(TASK_FILE_PATH, Encoding.UTF8).Select(l => l.Trim().ToLower()).ToArray().Shuffle();
        _tasks = new Queue<string>(lines);
    }

    public static string GetTask() {
        var task = _tasks.Dequeue();
        _tasks.Enqueue(task);
        return task;
    }
}
