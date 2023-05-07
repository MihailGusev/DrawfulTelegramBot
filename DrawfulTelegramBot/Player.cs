namespace DrawfulTelegramBot;

internal class Player
{
    public readonly long userId;
    public readonly long chatId;
    public readonly string username;
    public readonly Room room;
    public DrawingTask drawingTask;

    public bool IsHost => room.host == this;
    public int Score { get; set; }

    public Player(long userId, long chatId, string username, Room room) {
        this.userId = userId;
        this.chatId = chatId;
        this.username = username;
        this.room = room;
    }
}
