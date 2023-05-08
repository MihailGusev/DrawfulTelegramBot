using Telegram.Bot;
using Telegram.Bot.Types;

namespace DrawfulTelegramBot;

internal class BotState
{
    private const long ADMIN_ID = 1312251262;
    private const string HARD_RESET_COMMAND = "/hardreset";
    private const string NEW_ROOM_COMMAND = "/newroom";
    private const string LEAVE_ROOM_COMMAND = "/leaveroom";
    private const string START_GAME_COMMAND = "/startgame";
    private const string DRAWING_FINISHED_COMMAND = "/drawingfinished";
    private const string CHANGE_USERNAME_COMMAND = "/changeusername";
    private const string START_COMMAND = "/start";
    private const string HELP_COMMAND = "/help";
    private const string HELP_TEXT = "Имитация игры Drawful, но при этом главный экран не нужен, а рисовать можно где угодно. " +
                                     "Вопросы и пожелания можете писать сюда https://t.me/gusev256";

    private readonly Dictionary<int, Room> rooms = new();
    private readonly Dictionary<long, Player> players = new();

    public async Task HardReset(ITelegramBotClient botClient, string? errorMessage = null) {
        rooms.Clear();
        players.Clear();
        RoomIdPool.ReleaseAllIds();
        var message = errorMessage == null
            ? "Бот сброшен"
            : $"Возникло исключение: {errorMessage}. Бот сброшен";
        await botClient.SendTextMessageAsync(ADMIN_ID, message);
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

        Console.WriteLine($"Получено сообщение от {user.Username}: {message.Text}");

        var messageText = message.Text.ToLower();

        if (messageText == HARD_RESET_COMMAND && user.Id == ADMIN_ID) {
            await HardReset(botClient);
            return;
        }

        if (messageText == HELP_COMMAND) {
            await botClient.SendTextMessageAsync(message.Chat.Id, HELP_TEXT, cancellationToken: cancellationToken);
            return;
        }

        if (!players.TryGetValue(user.Id, out var existingPlayer)) {
            await HandleNewPlayer(botClient, user, message.Chat, messageText, cancellationToken);
            return;
        }

        if (messageText == LEAVE_ROOM_COMMAND) {
            var room = existingPlayer.room;

            if (room.state != RoomState.WaitingForPlayers && room.state != RoomState.Finished) {
                rooms.Remove(room.id);
                room.playerList.ForEach(p => players.Remove(p.userId));
                RoomIdPool.ReleaseId(room.id);
                await SendBroadcastMessage(botClient, room, "Один из игроков покинул комнату. Продолжение невозможно. Создайте новую комнату", cancellationToken);
            }

            players.Remove(user.Id);
            room.playerList.Remove(existingPlayer);
            if (room.playerList.Count == 0) {
                rooms.Remove(room.id);
                RoomIdPool.ReleaseId(room.id);
                return;
            }

            if (existingPlayer.IsOwner) {
                var newOwner = room.playerList.First();
                room.owner = newOwner;
                await SendBroadcastMessage(botClient, room, $"Создатель комнаты покинул её. {newOwner.username} назначен вместо него", cancellationToken);
            }
            else {
                await SendBroadcastMessage(botClient, room, $"Игрок {existingPlayer.username} покинул комнату", cancellationToken);
            }
            return;
        }

        if (messageText.StartsWith(CHANGE_USERNAME_COMMAND)) {
            if (messageText.Length - 1 > CHANGE_USERNAME_COMMAND.Length) {
                // Используем оригинальную переменную, потому что эту привели к нижнему регистру
                var newUsername = message.Text[(CHANGE_USERNAME_COMMAND.Length + 1)..];
                existingPlayer.username = newUsername;
                await botClient.SendTextMessageAsync(existingPlayer.chatId, "Имя успешно изменено", cancellationToken: cancellationToken);
            }
            else {
                await botClient.SendTextMessageAsync(existingPlayer.chatId, "Неверный формат. Новое имя должно следовать за командой через пробел", cancellationToken: cancellationToken);
            }
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
        if (messageText == START_COMMAND) {
            await botClient.SendTextMessageAsync(chat.Id, "Добро пожаловать в DrawfulBot! Введите номер комнаты, к которой хотите присоединиться или создайте новую", cancellationToken: cancellationToken);
            return;
        }

        var username = user.FirstName ?? user.Username ?? "Без имени";

        if (messageText == NEW_ROOM_COMMAND) {
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
        await botClient.SendTextMessageAsync(chat.Id, "Вы успешно подключились. Ожидайте начала игры", cancellationToken: cancellationToken);

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
        if (!player.IsOwner) {
            await botClient.SendTextMessageAsync(player.chatId, "Подождите, пока создатель комнаты начнёт игру", cancellationToken: cancellationToken);
            return;
        }

        if (messageText != START_GAME_COMMAND) {
            await botClient.SendTextMessageAsync(player.chatId, "Никакие действия, кроме старта игры, не доступны", cancellationToken: cancellationToken);
            return;
        }

        var room = player.room;
        if (room.playerList.Count <= 2) {
            await botClient.SendTextMessageAsync(player.chatId, "В комнате должно быть как минимум 3 человека", cancellationToken: cancellationToken);
            return;
        }

        room.MoveToDrawingState();
        room.AssignTasks();
        await SendBroadcastMessage(botClient, room, (Player p) => $"Раунд {room.RoundIndex}/{room.RoundCount}\nВаше задание: {p.drawingTask.text}", cancellationToken);
    }

    async Task HandleDrawingState(ITelegramBotClient botClient, Player player, string messageText, CancellationToken cancellationToken) {
        var room = player.room;
        if (messageText == DRAWING_FINISHED_COMMAND && player.IsOwner) {
            room.MoveToGuessingState();
            var message = $"Все закончили. Угадываем, что нарисовал(а) {room.BeingGuessedPlayer.username}";
            await SendBroadcastMessage(botClient, room, message, cancellationToken);
        }
        else {
            await botClient.SendTextMessageAsync(player.chatId, "Ждём, пока все дорисуют", cancellationToken: cancellationToken);
        }
    }

    async Task HandleGuessingState(ITelegramBotClient botClient, Player player, string messageText, CancellationToken cancellationToken) {
        var room = player.room;
        var beingGuessedPlayer = room.BeingGuessedPlayer;

        if (player == beingGuessedPlayer) {
            await botClient.SendTextMessageAsync(player.chatId, "Угадывается ваш рисунок. Ответ вводить не надо", cancellationToken: cancellationToken);
            return;
        }

        messageText = messageText.Trim().ToLower();
        var drawingTask = beingGuessedPlayer.drawingTask;
        var guessOptions = drawingTask.guessOptions;

        foreach (var answer in guessOptions) {
            if (answer.author == player) {
                await botClient.SendTextMessageAsync(player.chatId, "Вы уже дали ответ", cancellationToken: cancellationToken);
                return;
            }
            if (answer.text == messageText) {
                await botClient.SendTextMessageAsync(player.chatId, "Ваш ответ совпадает с чьим-то другим. Пожалуйста, введите другой", cancellationToken: cancellationToken);
                return;
            }
        }

        guessOptions.Add(new DrawingTaskGuessOption(messageText, player));

        if (drawingTask.guessOptions.Count < room.playerList.Count) {
            await botClient.SendTextMessageAsync(player.chatId, "Ждём ответов остальных игроков", cancellationToken: cancellationToken);
            return;
        }

        player.room.MoveToVotingState();

        var question = $"Что нарисовал(а) {beingGuessedPlayer.username}?";
        guessOptions.Shuffle();

        // Каждому игроку, рисунок которого сейчас не отгадывается, отправляет опрос во всеми опциями за исключением опции этого же игрока
        await Task.WhenAll(room.VotingPlayers.Select(p => {
            return botClient.SendPollAsync(p.chatId, question, guessOptions.Where(o => o.author != p).Select(o => o.text), isAnonymous: false, cancellationToken: cancellationToken);
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
        Console.WriteLine($"Получен голос от {pollAnswer.User.Username}");

        if (!players.TryGetValue(pollAnswer.User.Id, out var player)) {
            return;
        }

        var room = player.room;
        if (room.state != RoomState.Voting) {
            return;
        }

        var beingGuessedPlayer = room.BeingGuessedPlayer;
        var drawingTask = beingGuessedPlayer.drawingTask;

        var alreadyGuessed = drawingTask.guessOptions.SelectMany(o => o.voted).ToList();
        if (alreadyGuessed.Any(p => p == player)) {
            await botClient.SendTextMessageAsync(player.chatId, "Менять свой ответ не разрешается", cancellationToken: cancellationToken);
            return;
        }

        // Множественный выбор не разрешается, поэтому просто берём первую опцию
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
            await botClient.SendTextMessageAsync(player.chatId, "Ждём ответов остальных игроков", cancellationToken: cancellationToken);
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
        beingGuessedPlayer.WasCorrectlyGuessed(voters.Length);

        var correctPlayers = voters.Any() ? $"Угадали: {string.Join(", ", voters.Select(v => v.username))}" : "Никто не угадал :(";
        var correctGuessSummary = $"Правильный ответ: {correctOption!.text}. {correctPlayers}";
        await SendBroadcastMessageWithDelay(botClient, room, correctGuessSummary, cancellationToken);

        var scoreSummary = string.Join("\n", room.playerList.OrderByDescending(p => p.Score).Select(p => $"{p.username}: {p.Score}"));
        await SendBroadcastMessageWithDelay(botClient, room, scoreSummary, cancellationToken);

        if (room.HasNextBeingGuessedPlayer) {
            room.MoveToGuessingState(true);
            beingGuessedPlayer = room.BeingGuessedPlayer;
            await SendBroadcastMessage(botClient, room, $"Угадываем, что нарисовал(а) {beingGuessedPlayer.username}", cancellationToken);
            return;
        }

        if (room.HasMoreRounds) {
            room.MoveToDrawingState();
            room.PrepareForNewRound();
            await SendBroadcastMessage(botClient, room, (Player p) => $"Раунд {room.RoundIndex}/{room.RoundCount}\nВаше задание: {p.drawingTask.text}", cancellationToken);
            return;
        }

        var orderedByScore = room.playerList.OrderByDescending(p => p.Score).ToArray();
        var winners = orderedByScore.TakeWhile(p => p.Score == orderedByScore[0].Score);
        var winnerNames = string.Join(", ", winners.Select(p => p.username));

        await SendBroadcastMessage(botClient, room, $"Игра закончена. Победители: {winnerNames} 👑", cancellationToken);
        room.Reset();

        room.MoveToFinishedState();
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
