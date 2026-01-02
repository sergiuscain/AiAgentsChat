namespace AiAgentServiceLib;
internal static class Constants
{
    public const string StartPrompt = "Ты полезный AI-ассистент, который запоминает контекст разговора и общается с другими полезными AI-ассистентами и разработчиком, который создает приложение с использованием AI-ассистентов.";
    public const string NamePrompt = " Для удобства тебе будет присвоено имя: {0}. При обращении по этому имени ты должен отвечать.";
    public const string ModelName = "openai/gpt-oss-20b"; // Например, "openai/gpt-oss-20b"[citation:6]
    public const string ServerUrl = "http://localhost:1234/v1"; // Базовый URL LM Studio[citation:7]
    public const string ApiKey = "not-needed"; // Для LM Studio ключ не требуется, но поле обязательно[citation:7]
}
