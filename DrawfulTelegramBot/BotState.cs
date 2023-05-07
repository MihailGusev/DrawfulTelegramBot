using Telegram.Bot;
using Telegram.Bot.Types;

namespace DrawfulTelegramBot;

internal class BotState
{
    const int FOOLED_SOMEONE = 500;
    const int CORRECT_GUESS = 500;
    const int CORRECT_GUESS_AUTHOR = 1000;

    readonly Dictionary<long, Player> players = new();
    readonly Dictionary<int, Room> rooms = new();

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken) {
        if (update.Message is { } message) {
            await HandleMessage(botClient, message, cancellationToken);
        }
        else if (update.PollAnswer is { } pollAnswer) {
            await HandlePollAnswer(botClient, pollAnswer, cancellationToken);
        }
    }


    async Task HandleMessage(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(message.Text)) {
            return;
        }

        var user = message.From;
        if (user == null) {
            return;
        }

        if (message.Text == "/hardreset") {
            players.Clear();
            rooms.Clear();
            await botClient.SendTextMessageAsync(message.Chat.Id, "Бот сброшен к начальным настройкам", cancellationToken: cancellationToken);
            return;
        }

        if (!players.TryGetValue(user.Id, out var existingPlayer)) {
            await HandleNewPlayer(botClient, user, message.Chat, message.Text, cancellationToken);
            return;
        }

        switch (existingPlayer.room.roomState) {
            case RoomState.WaitingForPlayers:
                await HandleWaitingForPlayersState(botClient, existingPlayer, message.Text, cancellationToken);
                break;
            case RoomState.Drawing:
                await HandleDrawingState(botClient, existingPlayer, message.Text, cancellationToken);
                break;
            case RoomState.Guessing:
                await HandleGuessingState(botClient, existingPlayer, message.Text, cancellationToken);
                break;
            case RoomState.Voting:
                await HandleVotingState(botClient, existingPlayer, cancellationToken);
                break;
            case RoomState.ShowingResults:
                await HandleShowingResultsState(botClient, existingPlayer, cancellationToken);
                break;
            case RoomState.Finished:
                await HandleFinishedState(botClient, existingPlayer, message.Text, cancellationToken);
                break;
        }
    }

    async Task HandleNewPlayer(ITelegramBotClient botClient, User user, Chat chat, string messageText, CancellationToken cancellationToken) {
        var username = user.FirstName ?? user.Username;
        if (string.IsNullOrEmpty(username)) {
            await botClient.SendTextMessageAsync(chat.Id, "Нельзя войти в комнату, не имея имени или ника", cancellationToken: cancellationToken);
            return;
        }

        if (messageText == "/newroom") {
            var newRoomId = CreateRoom(user.Id, chat.Id, username);
            await botClient.SendTextMessageAsync(chat.Id, $"Комната {newRoomId} создана", cancellationToken: cancellationToken);
            return;
        }

        if (messageText.StartsWith("/join")) {
            await botClient.SendTextMessageAsync(chat.Id, "Введите номер комнаты, к которой хотите присоединиться", cancellationToken: cancellationToken);
            return;
        }

        if (int.TryParse(messageText, out var existingRoomId) && rooms.TryGetValue(existingRoomId, out var room)) {
            if (room.roomState == RoomState.WaitingForPlayers) {
                var newPlayer = new Player(user.Id, chat.Id, username, room);

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

    int CreateRoom(long userId, long chatId, string username) {
        var room = new Room();
        rooms.Add(room.id, room);

        var player = new Player(userId, chatId, username, room);
        players.Add(userId, player);

        room.playerList.Add(player);
        room.host = player;

        return room.id;
    }

    async Task HandleWaitingForPlayersState(ITelegramBotClient botClient, Player player, string messageText, CancellationToken cancellationToken) {
        if (messageText == "/startgame") {
            if (player.IsHost) {
                player.room.MoveToDrawingState();
                player.room.playerList.Shuffle();
                foreach (var p in player.room.playerList) {
                    p.drawingTask = new DrawingTask();
                    await botClient.SendTextMessageAsync(p.chatId, $"Задание: \"{p.drawingTask.text}\"", cancellationToken: cancellationToken);
                }
            }
            else {
                await botClient.SendTextMessageAsync(player.chatId, "Игру может начать только создатель комнаты", cancellationToken: cancellationToken);
            }
        }
        else {
            await botClient.SendTextMessageAsync(player.chatId, "Ждём игроков. Отправка сообщений ограничена", cancellationToken: cancellationToken);
        }
    }

    async Task HandleDrawingState(ITelegramBotClient botClient, Player player, string messageText, CancellationToken cancellationToken) {
        var room = player.room;
        if (messageText == "/drawingfinished") {
            player.drawingTask.isFinished = true;
            if (room.playerList.All(p => p.drawingTask.isFinished)) {
                var playerToGuess = room.MoveToGuessingState();
                foreach (var p in room.playerList) {
                    var message = p == playerToGuess
                        ? "Все закончили. Ваш рисунок угадывается первым"
                        : $"Все закончили. Угадываем, что нарисовал(а) {player.username}";
                    await botClient.SendTextMessageAsync(player.chatId, message, cancellationToken: cancellationToken);
                }
            }
            else {
                await botClient.SendTextMessageAsync(player.chatId, "Ждём, пока все дорисуют", cancellationToken: cancellationToken);
            }
        }
    }

    async Task HandleGuessingState(ITelegramBotClient botClient, Player player, string messageText, CancellationToken cancellationToken) {
        var playerToGuess = player.room.PlayerToGuess;

        if (player == playerToGuess) {
            await botClient.SendTextMessageAsync(player.chatId, "Угадывается ваш рисунок. Ответ вводить не надо", cancellationToken: cancellationToken);
            return;
        }

        messageText = messageText.Trim().ToLower();
        var drawingTask = playerToGuess.drawingTask;

        foreach (var answer in drawingTask.guessOptions) {
            if (answer.author == player) {
                await botClient.SendTextMessageAsync(player.chatId, "Вы уже дали ответ", cancellationToken: cancellationToken);
                return;
            }
            if (answer.text == messageText) {
                await botClient.SendTextMessageAsync(player.chatId, "Ваш ответ совпадает с чьим-то другим", cancellationToken: cancellationToken);
                return;
            }
        }

        var newAnswer = new DrawingTaskGuessOption(messageText, player);
        drawingTask.guessOptions.Add(newAnswer);

        if (drawingTask.guessOptions.Count < player.room.playerList.Count - 1) {
            await botClient.SendTextMessageAsync(player.chatId, "Ждём ответов остальных игроков", cancellationToken: cancellationToken);
            return;
        }

        player.room.MoveToVotingState();
        var options = drawingTask.guessOptions.Select(a => a.text).ToList().Shuffle();

        foreach (var p in player.room.playerList.Where(p => p != playerToGuess)) {
            var pollMessage = await botClient.SendPollAsync(
                    chatId: p.chatId,
                    question: $"Что нарисовал(а) {playerToGuess.username}?",
                    options: options,
                    cancellationToken: cancellationToken,
                    isAnonymous: false);
        }
    }

    async Task HandleVotingState(ITelegramBotClient botClient, Player player, CancellationToken cancellationToken) {
        await botClient.SendTextMessageAsync(player.chatId, "Идёт голосование. Отправка сообщений ограничена", cancellationToken: cancellationToken);
    }

    async Task HandleShowingResultsState(ITelegramBotClient botClient, Player player, CancellationToken cancellationToken) {
        await botClient.SendTextMessageAsync(player.chatId, "Отправка сообщений ограничена", cancellationToken: cancellationToken);
    }

    async Task HandleFinishedState(ITelegramBotClient botClient, Player player, string messageText, CancellationToken cancellationToken) {
        if (messageText == "/startgame") {
            if (player.IsHost) {
                player.room.MoveToDrawingState();
                foreach (var p in player.room.playerList) {
                    p.drawingTask = new DrawingTask();
                    await botClient.SendTextMessageAsync(p.chatId, $"Задание: \"{p.drawingTask.text}\"", cancellationToken: cancellationToken);
                }
            }
            else {
                await botClient.SendTextMessageAsync(player.chatId, "Игру может начать только создатель комнаты", cancellationToken: cancellationToken);
            }
        }
        else {
            await botClient.SendTextMessageAsync(player.chatId, "Отправка сообщений ограничена", cancellationToken: cancellationToken);
        }
    }

    async Task HandlePollAnswer(ITelegramBotClient botClient, PollAnswer pollAnswer, CancellationToken cancellationToken) {
        var player = players[pollAnswer.User.Id];
        var room = player.room;
        if (room.roomState != RoomState.Voting) {
            return;
        }

        var playerToGuess = player.room.PlayerToGuess;
        var drawingTask = playerToGuess.drawingTask;

        var guessedPlayers = drawingTask.guessOptions.SelectMany(o => o.voted).ToList();
        if (guessedPlayers.Any(p => p == player)) {
            await botClient.SendTextMessageAsync(player.chatId, "Менять свой ответ не разрешается", cancellationToken: cancellationToken);
            return;
        }

        var optionId = pollAnswer.OptionIds[0];
        var guessOptions = drawingTask.guessOptions;
        guessOptions[optionId].voted.Add(player);

        if (guessedPlayers.Count < player.room.playerList.Count - 1) {
            return;
        }

        room.MoveToShowingResultsState();

        var correctOption = guessOptions.Find(o => o.IsCorrect);
        guessOptions.Remove(correctOption!);

        foreach (var guessOption in guessOptions.Where(o => o.voted.Any()).OrderBy(o => o.voted.Count)) {
            var author = guessOption.author!;
            var fooled = guessOption.voted.Select(v => v.username).ToArray();
            author.Score += fooled.Length * FOOLED_SOMEONE;

            var guessSummary = $"{guessOption.text}. Автор: {author.username}. Обманул: {string.Join(", ", fooled)}";
            foreach (var p in player.room.playerList) {
                await botClient.SendTextMessageAsync(player.chatId, guessSummary, cancellationToken: cancellationToken);
            }

            await Task.Delay(2000, cancellationToken);
        }

        var voters = correctOption!.voted.ToArray();
        foreach (var voter in voters) {
            voter.Score += CORRECT_GUESS;
        }
        playerToGuess.Score += voters.Length * CORRECT_GUESS_AUTHOR;

        var correctGuessSummary = $"{correctOption!.text}. Угадали: {string.Join(", ", voters.Select(v => v.username))}";

        foreach (var p in player.room.playerList) {
            await botClient.SendTextMessageAsync(player.chatId, correctGuessSummary, cancellationToken: cancellationToken);
        }

        await Task.Delay(2000, cancellationToken);

        var scoreSummary = string.Join("\n", player.room.playerList.Select(p => $"{p.username}: {p.Score}"));
        foreach (var p in room.playerList) {
            await botClient.SendTextMessageAsync(player.chatId, scoreSummary, cancellationToken: cancellationToken);
        }

        await Task.Delay(2000, cancellationToken);

        if (room.HasNextPlayerToGuess) {
            room.MoveToGuessingState();
            playerToGuess = room.PlayerToGuess;
            foreach (var p in player.room.playerList) {
                await botClient.SendTextMessageAsync(player.chatId, $"Угадываем, что нарисовал(а) {playerToGuess.username}", cancellationToken: cancellationToken);
            }
        }
        else {
            var winner = player.room.playerList.OrderByDescending(p => p.Score).First();
            foreach (var p in player.room.playerList) {
                await botClient.SendTextMessageAsync(player.chatId, $"Игра закончена. Победил(а) {winner.username}", cancellationToken: cancellationToken);
            }
            room.MoveToFinishedState();
        }
    }
}
