using Telegram.Bot;
using Telegram.Bot.Types;

namespace DrawfulTelegramBot;

internal class BotState
{
    const long adminId = 1312251262;

    readonly Dictionary<int, Room> rooms = new();
    readonly Dictionary<long, Player> players = new();

    public async Task HardReset(ITelegramBotClient botClient, string? errorMessage = null) {
        rooms.Clear();
        players.Clear();
        RoomIdPool.ReleaseAllIds();
        var message = errorMessage == null
            ? "Бот сброшен"
            : $"Возникло исключение: {errorMessage}. Бот сброшен";
        await botClient.SendTextMessageAsync(adminId, message);
    }

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

        var messageText = message.Text.ToLower();

        if (messageText == "/hardreset" && user.Id == adminId) {
            await HardReset(botClient);
            return;
        }

        if (!players.TryGetValue(user.Id, out var existingPlayer)) {
            await HandleNewPlayer(botClient, user, message.Chat, messageText, cancellationToken);
            return;
        }

        if (messageText == "/leaveroom") {
            var room = existingPlayer.room;
            rooms.Remove(room.id);
            room.playerList.ForEach(p => players.Remove(p.userId));
            RoomIdPool.ReleaseId(room.id);
            await SendBroadcastMessage(botClient, room, "Один из игроков покинул комнату. Продолжение невозможно", cancellationToken);
            return;
        }

        switch (existingPlayer.room.state) {
            case RoomState.WaitingForPlayers:
                await HandleWaitingForPlayersState(botClient, existingPlayer, messageText, cancellationToken);
                break;
            case RoomState.Drawing:
                await HandleDrawingState(botClient, existingPlayer, messageText, cancellationToken);
                break;
            case RoomState.Guessing:
                await HandleGuessingState(botClient, existingPlayer, messageText, cancellationToken);
                break;
            case RoomState.Voting:
                await HandleVotingState(botClient, existingPlayer, cancellationToken);
                break;
            case RoomState.ShowingResults:
                await HandleShowingResultsState(botClient, existingPlayer, cancellationToken);
                break;
            case RoomState.Finished:
                await HandleFinishedState(botClient, existingPlayer, messageText, cancellationToken);
                break;
        }
    }

    async Task HandleNewPlayer(ITelegramBotClient botClient, User user, Chat chat, string messageText, CancellationToken cancellationToken) {
        var username = user.FirstName ?? user.Username;
        if (string.IsNullOrEmpty(username)) {
            await botClient.SendTextMessageAsync(chat.Id, "Нельзя пользоваться ботом, не имея имени или ника", cancellationToken: cancellationToken);
            return;
        }

        if (messageText.StartsWith("/start")) {
            await botClient.SendTextMessageAsync(chat.Id, "Введите номер комнаты, к которой хотите присоединиться или создайте новую", cancellationToken: cancellationToken);
            return;
        }

        if (messageText == "/newroom") {
            var newRoomId = CreateRoom(user.Id, chat.Id, username);
            await botClient.SendTextMessageAsync(chat.Id, $"Комната {newRoomId} создана", cancellationToken: cancellationToken);
            return;
        }

        if (!int.TryParse(messageText, out var existingRoomId) || !rooms.TryGetValue(existingRoomId, out var room)) {
            await botClient.SendTextMessageAsync(chat.Id, "Комнаты с таким номером не существует", cancellationToken: cancellationToken);
            return;
        }

        if (room.state != RoomState.WaitingForPlayers) {
            await botClient.SendTextMessageAsync(chat.Id, "К этой комнате нельзя присоединиться", cancellationToken: cancellationToken);
            return;
        }

        if (!room.CanAddPlayer) {
            await botClient.SendTextMessageAsync(chat.Id, "Достигнут лимит игроков для комнаты", cancellationToken: cancellationToken);
            return;
        }

        var newPlayer = new Player(user.Id, chat.Id, username, room);
        players.Add(user.Id, newPlayer);
        await SendBroadcastMessage(botClient, room, $"Игрок {newPlayer.username} зашёл в комнату", cancellationToken);
        room.playerList.Add(newPlayer);
    }

    int CreateRoom(long userId, long chatId, string username) {
        var room = new Room();
        rooms.Add(room.id, room);

        var player = new Player(userId, chatId, username, room);
        players.Add(userId, player);

        room.playerList.Add(player);
        room.owner = player;

        return room.id;
    }

    async Task HandleWaitingForPlayersState(ITelegramBotClient botClient, Player player, string messageText, CancellationToken cancellationToken) {
        if (!player.IsHost) {
            await botClient.SendTextMessageAsync(player.chatId, "Подождите, пока создатель комнаты начнёт игру", cancellationToken: cancellationToken);
            return;
        }

        if (messageText != "/startgame") {
            await botClient.SendTextMessageAsync(player.chatId, "Никакие действия, кроме старта игры, не доступны", cancellationToken: cancellationToken);
            return;
        }

        var room = player.room;
        if (room.playerList.Count <= 2) {
            await botClient.SendTextMessageAsync(player.chatId, "В комнате должно быть как минимум 3 человека", cancellationToken: cancellationToken);
            return;
        }

        var roundCount = room.PrepareForNewGame();
        await SendBroadcastMessage(botClient, room, (Player p) => $"Раунд 1/{roundCount}\nВаше задание: {p.drawingTask.text}", cancellationToken);
    }

    async Task HandleDrawingState(ITelegramBotClient botClient, Player player, string messageText, CancellationToken cancellationToken) {
        var room = player.room;
        if (messageText == "/drawingfinished" && player == room.owner) {
            room.MoveToGuessingState();
            var message = $"Все закончили. Угадываем, что нарисовал(а) {room.NextDrawingPlayer.username}";
            await SendBroadcastMessage(botClient, room, message, cancellationToken);
        }
        else {
            await botClient.SendTextMessageAsync(player.chatId, "Ждём, пока все дорисуют", cancellationToken: cancellationToken);
        }
    }

    async Task HandleGuessingState(ITelegramBotClient botClient, Player player, string messageText, CancellationToken cancellationToken) {
        var room = player.room;
        var playerToGuess = room.NextDrawingPlayer;

        if (player == playerToGuess) {
            await botClient.SendTextMessageAsync(player.chatId, "Угадывается ваш рисунок. Ответ вводить не надо", cancellationToken: cancellationToken);
            return;
        }

        messageText = messageText.Trim().ToLower();
        var drawingTask = playerToGuess.drawingTask;
        var guessOptions = drawingTask.guessOptions;

        foreach (var answer in guessOptions) {
            if (answer.author == player) {
                await botClient.SendTextMessageAsync(player.chatId, "Вы уже дали ответ", cancellationToken: cancellationToken);
                return;
            }
            if (answer.text == messageText) {
                await botClient.SendTextMessageAsync(player.chatId, "Ваш ответ совпадает с чьим-то другим", cancellationToken: cancellationToken);
                return;
            }
        }

        guessOptions.Add(new DrawingTaskGuessOption(messageText, player));

        if (drawingTask.guessOptions.Count < room.playerList.Count) {
            await botClient.SendTextMessageAsync(player.chatId, "Ждём ответов остальных игроков", cancellationToken: cancellationToken);
            return;
        }

        player.room.MoveToVotingState();

        var question = $"Что нарисовал(а) {playerToGuess.username}?";
        drawingTask.ShuffleOptions();
        var options = drawingTask.guessOptions.Select(a => a.text);

        await Task.WhenAll(room.VotingPlayers.Select(p => {
            return botClient.SendPollAsync(p.chatId, question, drawingTask.guessOptions.Where(o => o.author != p).Select(o => o.text), isAnonymous: false, cancellationToken: cancellationToken);
        }));
    }

    async Task HandleVotingState(ITelegramBotClient botClient, Player player, CancellationToken cancellationToken) {
        await botClient.SendTextMessageAsync(player.chatId, "Идёт голосование. Отправка сообщений ограничена", cancellationToken: cancellationToken);
    }

    async Task HandleShowingResultsState(ITelegramBotClient botClient, Player player, CancellationToken cancellationToken) {
        await botClient.SendTextMessageAsync(player.chatId, "Отправка сообщений ограничена", cancellationToken: cancellationToken);
    }

    async Task HandleFinishedState(ITelegramBotClient botClient, Player player, string messageText, CancellationToken cancellationToken) {
        await HandleWaitingForPlayersState(botClient, player, messageText, cancellationToken);
    }

    async Task HandlePollAnswer(ITelegramBotClient botClient, PollAnswer pollAnswer, CancellationToken cancellationToken) {
        if (!players.TryGetValue(pollAnswer.User.Id, out var player)) {
            return;
        }

        var room = player.room;
        if (room.state != RoomState.Voting) {
            return;
        }

        var playerToGuess = room.NextDrawingPlayer;
        var drawingTask = playerToGuess.drawingTask;

        var alreadyGuessed = drawingTask.guessOptions.SelectMany(o => o.voted).ToList();
        if (alreadyGuessed.Any(p => p == player)) {
            await botClient.SendTextMessageAsync(player.chatId, "Менять свой ответ не разрешается", cancellationToken: cancellationToken);
            return;
        }

        // Отправляем только голосования с одним вариантом ответа, поэтому просто берём первый
        var optionId = pollAnswer.OptionIds[0];
        var guessOptions = drawingTask.guessOptions;

        // Нужно сделать поправку на то, что мы не показываем игроку его же вариант ответа
        var optionIdOfPlayer = drawingTask.guessOptions.FindIndex(o => o.author == player);
        if (optionIdOfPlayer <= optionId) {
            optionId++;
        }

        // Перед отправкой в опрос опции тасуются и больше не меняют своего порядка
        guessOptions[optionId].voted.Add(player);

        // +1 за только что добавленного игрока и ещё +1 за автора рисунка
        if (alreadyGuessed.Count + 2 < room.playerList.Count) {
            return;
        }

        room.MoveToShowingResultsState();

        // Сначала отобразим неверные ответы
        var correctOption = guessOptions.Find(o => o.IsCorrect);
        guessOptions.Remove(correctOption!);

        foreach (var guessOption in guessOptions.Where(o => o.voted.Any()).OrderBy(o => o.voted.Count)) {
            var author = guessOption.author!;
            var fooled = guessOption.voted.Select(v => v.username).ToArray();
            author.FooledSomeone(fooled.Length);

            var guessSummary = $"Догадка: {guessOption.text}. Автор: {author.username}. Обмануты: {string.Join(", ", fooled)}";
            await SendBroadcastMessageWithDelay(botClient, room, guessSummary, cancellationToken);
        }

        var voters = correctOption!.voted.ToArray();
        voters.ForEach(v => v.EnteredCorrectGuess());
        playerToGuess.WasCorrectlyGuessed(voters.Length);

        var correctPlayers = voters.Any() ? $"Угадали: {string.Join(", ", voters.Select(v => v.username))}" : "Никто не угадал :(";
        var correctGuessSummary = $"{correctOption!.text} {correctPlayers}";
        await SendBroadcastMessageWithDelay(botClient, room, correctGuessSummary, cancellationToken);

        var scoreSummary = string.Join("\n", room.playerList.OrderByDescending(p => p.Score).Select(p => $"{p.username}: {p.Score}"));
        await SendBroadcastMessageWithDelay(botClient, room, scoreSummary, cancellationToken);

        if (room.HasNextDrawingPlayer) {
            room.MoveToGuessingState(true);
            playerToGuess = room.NextDrawingPlayer;
            await SendBroadcastMessage(botClient, room, $"Угадываем, что нарисовал(а) {playerToGuess.username}", cancellationToken);
        }
        else {
            var winner = room.playerList.OrderByDescending(p => p.Score).First();
            await SendBroadcastMessage(botClient, room, $"Игра закончена. Победил(а) {winner.username} 👑", cancellationToken);
            room.MoveToFinishedState();
        }
    }

    async Task SendBroadcastMessage(ITelegramBotClient botClient, Room room, string message, CancellationToken cancellationToken) {
        await Task.WhenAll(room.playerList.Select(p => botClient.SendTextMessageAsync(p.chatId, message, cancellationToken: cancellationToken)));
    }

    async Task SendBroadcastMessageWithDelay(ITelegramBotClient botClient, Room room, string message, CancellationToken cancellationToken, int delay = 3000) {
        await SendBroadcastMessage(botClient, room, message, cancellationToken);
        await Task.Delay(delay);
    }

    async Task SendBroadcastMessage(ITelegramBotClient botClient, Room room, Func<Player, string> getMessage, CancellationToken cancellationToken) {
        await Task.WhenAll(room.playerList.Select(p => botClient.SendTextMessageAsync(p.chatId, getMessage(p), cancellationToken: cancellationToken)));
    }
}
