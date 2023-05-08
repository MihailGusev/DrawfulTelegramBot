namespace DrawfulTelegramBot;

internal class DrawingTask
{
    public readonly string text;
    public readonly List<DrawingTaskGuessOption> guessOptions = new();

    public DrawingTask() {
        var text = DrawingTaskPool.GetTask();
        guessOptions.Add(new DrawingTaskGuessOption(text, null));
    }

    public void ShuffleOptions() {
        guessOptions.Shuffle();
    }
}
