using DrawfulTelegramBot;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

const string BotKey = "6241933089:AAFXgehY7TnvPaqhpfLXZrwqmY0wlIq4_04";
var botClient = new TelegramBotClient(BotKey);
var botState = new BotState();

using (CancellationTokenSource cts = new()) {

    botClient.StartReceiving(
       updateHandler: botState.HandleUpdateAsync,
       pollingErrorHandler: HandlePollingErrorAsync,
       receiverOptions: new() { AllowedUpdates = Array.Empty<UpdateType>() }, // receive all update types
       cancellationToken: cts.Token
    );

    var me = await botClient.GetMeAsync();

    Console.WriteLine($"Start listening for @{me.Username}");
    Console.ReadLine();

    // Send cancellation request to stop bot
    cts.Cancel();
}

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken) {
    var ErrorMessage = exception switch {
        ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}