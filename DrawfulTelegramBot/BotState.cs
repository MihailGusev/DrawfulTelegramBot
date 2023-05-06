using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace DrawfulTelegramBot
{
    internal class BotState
    {
        public Dictionary<long, Player> players = new();
        public Dictionary<string, Room> rooms = new();

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken) {
            if (update.Message is { } message) {
                await HandleMessage(botClient, message);
            }
            else if (update.Poll is { } poll) {
                ;
            }
        }

        public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken) {
            var ErrorMessage = exception switch {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        public string CreateRoom(Chat chat) {
            var room = new Room();
            rooms.Add(room.id, room);

            var player = new Player(chat, room);
            players.Add(chat.Id, player);

            return room.id;
        }

        public void LeaveRoom(Chat chat) {
            players.Remove(chat.Id);
        }

        async Task HandleMessage(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken) {
            var messageText = message.Text;

            if (string.IsNullOrEmpty(messageText)) {
                return;
            }

            var chat = message.Chat;

            if (!botState.players.ContainsKey(chat.Id)) {
                if (messageText == "newroom") {
                    var roomId = botState.CreateRoom(chat);
                    await botClient.SendTextMessageAsync(chat.Id, "Room has been created, have fun", cancellationToken: cancellationToken);
                }
                else if (messageText.StartsWith("join")) {

                }
                else {

                }
            }


            if (messageText == "newroom") {
                var createResult = botState.CreateRoom(chat);
                await botClient.SendTextMessageAsync(chat.Id, createResult, cancellationToken: cancellationToken);
            }
            else if (messageText == "joinroom") {
                await botClient.SendTextMessageAsync(chat.Id, "Enter room ID", cancellationToken: cancellationToken);
            }
        }

        async Task HandlePoll() {
            /*Console.WriteLine($"Received a '{messageText}' message in chat.");*/

            /*Message pollMessage = await botClient.SendPollAsync(
            chatId: message.Chat.Id,
            question: "Did you ever hear the tragedy of Darth Plagueis The Wise?",
            options: new[]
            {
                "Yes for the hundredth time!",
                "No, who`s that?"
            },
            cancellationToken: cancellationToken,
            type: PollType.Quiz,
            correctOptionId: 0);*/

            // Echo received message text
            //Message sentMessage = await botClient.SendTextMessageAsync(
            //    chatId: chatId,
            //    text: "Поехали на море, хуесос",
            //    cancellationToken: cancellationToken);
        }
    }
}
