using Telegram.Bot;
using Telegram.Bot.Types;

namespace DrawfulTelegramBot;

internal class BotState
{
    readonly Dictionary<long, Player> players = new();
    readonly Dictionary<int, Room> rooms = new();

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken) {
        if (update.Message is { } message) {
            await HandleMessage(botClient, message, cancellationToken);
        }
    }

    async Task HandleMessage(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken) {
        if (string.IsNullOrEmpty(message.Text)) {
            return;
        }

        var chat = message.Chat;

        if (!players.TryGetValue(chat.Id, out var existingPlayer)) {
            await HandleNewPlayer(botClient, chat, message.Text, cancellationToken);
            return;
        }

        switch (existingPlayer.room.RoomState) {
            case RoomState.WaitingForPlayers:
                await HandleWaitingForPlayersState(botClient, existingPlayer, message.Text, cancellationToken);
                break;
            case RoomState.Drawing:
                await HandleDrawingState(botClient, existingPlayer, message.Text, cancellationToken);
                break;
            case RoomState.Guessing:
                await HandleGuessingState(botClient, existingPlayer, message.Text, cancellationToken);
                break;
        }
    }

    async Task HandleNewPlayer(ITelegramBotClient botClient, Chat chat, string messageText, CancellationToken cancellationToken) {
        var username = chat.Username ?? chat.FirstName;
        if (string.IsNullOrEmpty(username)) {
            await botClient.SendTextMessageAsync(chat.Id, "Нельзя пользоваться ботом, не имея ника или имени", cancellationToken: cancellationToken);
            return;
        }

        if (messageText == "/newroom") {
            var newRoomId = InitRoom(chat);
            await botClient.SendTextMessageAsync(chat.Id, $"Комната {newRoomId} создана", cancellationToken: cancellationToken);
            return;
        }

        if (messageText.StartsWith("/join")) {
            await botClient.SendTextMessageAsync(chat.Id, "Введите номер комнаты, к которой хотите присоединиться", cancellationToken: cancellationToken);
            return;
        }

        if (int.TryParse(messageText, out var existingRoomId) && rooms.TryGetValue(existingRoomId, out var room)) {
            if (room.RoomState == RoomState.WaitingForPlayers) {
                var newPlayer = new Player(chat, room);

                foreach (var player in room.playerList) {
                    await botClient.SendTextMessageAsync(player.chatId, $"Игрок {player.username} зашёл в комнату", cancellationToken: cancellationToken);
                }
                room.playerList.Add(newPlayer);
            }
            else {
                await botClient.SendTextMessageAsync(chat.Id, "К этой комнате нельзя присоединиться", cancellationToken: cancellationToken);
            }
        }
        await botClient.SendTextMessageAsync(chat.Id, "Комнаты с таким номером не существует", cancellationToken: cancellationToken);
    }

    int InitRoom(Chat chat) {
        var room = new Room();
        rooms.Add(room.id, room);

        var player = new Player(chat, room);
        players.Add(chat.Id, player);

        room.playerList.Add(player);
        room.host = player;

        return room.id;
    }

    async Task HandleWaitingForPlayersState(ITelegramBotClient botClient, Player player, string messageText, CancellationToken cancellationToken) {
        if (messageText == "/startgame") {
            if (player.IsHost) {
                player.room.MoveToDrawingState();
                foreach (var p in player.room.playerList) {
                    await botClient.SendTextMessageAsync(p.chatId, $"Задание: \"{p.task}\"", cancellationToken: cancellationToken);
                }
            }
            else {
                await botClient.SendTextMessageAsync(player.chatId, "Игру может начать только создатель комнаты", cancellationToken: cancellationToken);
            }
        }
    }

    async Task HandleDrawingState(ITelegramBotClient botClient, Player player, string messageText, CancellationToken cancellationToken) {
        if (messageText == "/drawingfinished") {
            player.finishedDrawing = true;
            if (player.room.playerList.All(p => p.finishedDrawing)) {
                var nextPlayer = player.room.MoveToGuessingState();
                foreach (var p in player.room.playerList) {
                    await botClient.SendTextMessageAsync(player.chatId, $"Все закончили. Угадываем, что нарисовал(а) {player.username}", cancellationToken: cancellationToken);
                }
            }
            else {
                await botClient.SendTextMessageAsync(player.chatId, "Спасибо! Ждём, пока все закончат", cancellationToken: cancellationToken);
            }
        }
    }

    async Task HandleGuessingState(ITelegramBotClient botClient, Player player, string messageText, CancellationToken cancellationToken) {
        if (!string.IsNullOrEmpty(player.answer)) {
            await botClient.SendTextMessageAsync(player.chatId, "Вы уже дали ответ", cancellationToken: cancellationToken);
            return;
        }

        if (player.room.playerList.Any(p => p.answer == messageText)) {
            await botClient.SendTextMessageAsync(player.chatId, "Ваш ответ совпадает с чьим-то другим. Пожалуйста, введите другой", cancellationToken: cancellationToken);
            return;
        }

        player.answer = messageText;

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
