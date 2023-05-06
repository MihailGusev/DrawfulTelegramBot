using Telegram.Bot.Types;

namespace DrawfulTelegramBot
{
    internal class Player
    {
        long chatId;
        string username;
        Room room;

        public Player(Chat chat, Room room) {
            chatId = chat.Id;
            username = chat.Username ?? chat.FirstName ?? "Unnamed";
            this.room = room;
        }
    }
}
