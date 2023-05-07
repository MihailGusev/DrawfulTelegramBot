namespace DrawfulTelegramBot;

internal class Player
{
    public readonly long userId;
    public readonly long chatId;
    public readonly string username;
    public readonly Room room;
    public DrawingTask drawingTask;

    public bool IsHost => room.owner == this;
    public int Score { get; private set; }

    public Player(long userId, long chatId, string username, Room room) {
        this.userId = userId;
        this.chatId = chatId;
        this.username = username;
        this.room = room;
    }

    public void FooledSomeone(int count) => Score += 500 * count;

    public void EnteredCorrectGuess() => Score += 500;

    public void WasCorrectlyGuessed(int count) => Score += 1000 * count;

    public void ResetScore() => Score = 0;
}
