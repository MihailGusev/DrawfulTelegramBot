using Telegram.Bot.Types;

namespace DrawfulTelegramBot;

internal class Player
{
    public readonly long chatId;
    public readonly string username;
    public readonly Room room;
    public DrawingTask task;

    public bool IsHost => room.host == this;
    public bool finishedDrawing;
    public int Score { get; private set; }

    public Player(Chat chat, Room room) {
        chatId = chat.Id;
        username = chat.FirstName ?? chat.Username ?? "Unnamed";
        this.room = room;
    }
}
