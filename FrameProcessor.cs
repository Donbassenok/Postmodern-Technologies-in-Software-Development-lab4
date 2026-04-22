using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Responses; 

public class FrameProcessor
{
    private readonly ILogger<FrameProcessor> _logger;
    private readonly OpenAIClient _openAiClient;
    private readonly string _systemPrompt;
    private readonly string _model;

    public FrameProcessor(ILogger<FrameProcessor> logger, IConfiguration configuration)
    {
        _logger = logger;
        string apiKey = configuration["OpenAI:ApiKey"] 
            ?? throw new ArgumentNullException("OpenAI:ApiKey відсутній у конфігурації.");
        _openAiClient = new(apiKey);

        _systemPrompt = configuration["OpenAI:SystemPrompt"] 
            ?? "Ти — технічний експерт з аналізу зображень. Твоя задача: детально, але лаконічно описувати об'єкти на фотографіях, які тобі надсилають. Відповідай українською мовою.";
        
        _model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
    }

    public async Task<string> AnalyzeImageAsync(string fileUrl, string? userText = null)
    {
        _logger.LogInformation("Починаю аналіз зображення через OpenAI Responses API...");

        try
        {
            ResponsesClient responsesClient = _openAiClient.GetResponsesClient();

            string textPrompt = string.IsNullOrWhiteSpace(userText) 
                ? "Що зображено на цьому фото?" 
                : userText;

            var contentParts = new List<ResponseContentPart>
            {
                ResponseContentPart.CreateInputTextPart(textPrompt),
                ResponseContentPart.CreateInputImagePart(new Uri(fileUrl), ResponseImageDetailLevel.High)
            };

            ResponseItem userMessage = ResponseItem.CreateUserMessageItem(contentParts);

            CreateResponseOptions options = new() 
            { 
                Model = _model,
                Instructions = _systemPrompt,
                InputItems = { userMessage } 
            };
            ResponseResult response = await responsesClient.CreateResponseAsync(options);
            return response.GetOutputText() ?? "Відповідь порожня";
        }
        catch (Exception ex)
        {
            _logger.LogError($"Помилка під час звернення до OpenAI API: {ex.Message}");
            return "❌ Вибачте, сталася помилка під час аналізу зображення. Перевірте логі сервера.";
        }
    }
}