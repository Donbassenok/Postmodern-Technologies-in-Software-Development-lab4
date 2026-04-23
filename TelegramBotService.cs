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
    private readonly FrameProcessor _frameProcessor; 

    // Оновлюємо конструктор
    public TelegramBotService(ILogger<TelegramBotService> logger, IConfiguration configuration, FrameProcessor frameProcessor)
    {
        _logger = logger;
        _frameProcessor = frameProcessor; 
        
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

            string replyText = message.Text switch
            {
                "/start" => "Привіт! Я ШІ-експерт з аналізу зображень 👁️. Надішліть мені будь-яке фото (можна додати питання у підпис), і я розповім, що на ньому.",
                "/help" => "⚙️ Як користуватися:\n1. Надішліть фото.\n2. (Опційно) Додайте текст до фотографії, щоб запитати щось конкретне.\n3. Зачекайте кілька секунд на відповідь.",
                _ => "Я розумію лише фотографії 📸. Надішліть мені зображення, яке потрібно проаналізувати."
            };

            await botClient.SendMessage(
                chatId: chatId,
                text: replyText,
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

            string? userCaption = message.Caption;

            var waitMessage = await botClient.SendMessage(
                chatId: chatId,
                text: "⏳ Отримав зображення. Аналізую...",
                cancellationToken: cancellationToken);

            var aiResponse = await _frameProcessor.AnalyzeImageAsync(fileUrl, userCaption);

            await botClient.SendMessage(
                chatId: chatId,
                text: aiResponse,
                cancellationToken: cancellationToken);
        }

        if (message.Type != MessageType.Text && message.Type != MessageType.Photo)
        {
            _logger.LogWarning($"Отримано непідтримуваний тип повідомлення: {message.Type}");
            await botClient.SendMessage(
                chatId: chatId,
                text: "🤷‍♂️ Вибачте, але я вмію аналізувати лише стиснуті фотографії. Будь ласка, надішліть зображення як звичайне фото, а не як документ чи файл.",
                cancellationToken: cancellationToken);
        }
    }
    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        _logger.LogError($"Помилка Telegram API: {exception.Message}");
        return Task.CompletedTask;
    }
}