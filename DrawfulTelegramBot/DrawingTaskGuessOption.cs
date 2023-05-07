namespace DrawfulTelegramBot;

internal class DrawingTaskGuessOption
{
    public string text;
    public Player? author;
    public bool IsCorrect => author == null;
    public List<Player> voted = new();

    public DrawingTaskGuessOption(string text, Player? author) {
        this.text = text;
        this.author = author;
    }
}
