namespace DrawfulTelegramBot;

internal class DrawingTask
{
    public readonly string text;
    public readonly List<DrawingTaskGuessOption> guessOptions = new();

    public DrawingTask() {
        text = DrawingTaskPool.GetTask();
        guessOptions.Add(new DrawingTaskGuessOption(text, null));
    }
}
