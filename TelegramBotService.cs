using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Polling;

public class TelegramBotService : IHostedService
{
    private readonly ILogger<TelegramBotService> _logger;
    private readonly TelegramBotClient _botClient;
    private readonly string _botToken;

    public TelegramBotService(ILogger<TelegramBotService> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        _botToken = configuration["BotConfiguration:BotToken"] 
            ?? throw new ArgumentNullException("BotToken is missing");
            
        _botClient = new TelegramBotClient(_botToken);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Запуск Telegram бота...");

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cancellationToken
        );

        var me = await _botClient.GetMe(cancellationToken);
        _logger.LogInformation($"Бот @{me.Username} успішно запущений та слухає повідомлення.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Зупинка Telegram бота...");
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
            return;

        var chatId = message.Chat.Id;

        if (message.Type == MessageType.Text)
        {
            _logger.LogInformation($"Отримано текст: {message.Text}");
            await botClient.SendMessage(
                chatId: chatId,
                text: "Привіт! Я бот для аналізу зображень 👁️. Будь ласка, надішліть мені фотографію.",
                cancellationToken: cancellationToken);
            return;
        }

        if (message.Type == MessageType.Photo)
        {
            _logger.LogInformation("Отримано фотографію");

            var photo = message.Photo.Last();
            var fileId = photo.FileId;

            var fileInfo = await botClient.GetFile(fileId, cancellationToken);
            
            var fileUrl = $"https://api.telegram.org/file/bot{_botToken}/{fileInfo.FilePath}";

            _logger.LogInformation($"Згенеровано URL: {fileUrl}");

            await botClient.SendMessage(
                chatId: chatId,
                text: $"Я отримав ваше фото! Ось пряме посилання на нього:\n{fileUrl}\n\n(Пізніше ми передамо його ШІ для аналізу).",
                cancellationToken: cancellationToken);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        _logger.LogError($"Помилка Telegram API: {exception.Message}");
        return Task.CompletedTask;
    }
}