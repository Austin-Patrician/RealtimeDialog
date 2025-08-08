namespace RealtimeDialog.Core.Configuration;

public class RealtimeDialogConfig
{
    public const string SectionName = "RealtimeDialog";
    
    public WebSocketConfig WebSocket { get; set; } = new();
    public AudioConfig Audio { get; set; } = new();
    public DialogConfig Dialog { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
}

public class WebSocketConfig
{
    public string Url { get; set; } = "wss://openspeech.bytedance.com/api/v3/realtime/dialogue";
    public string AppId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string AppKey { get; set; } = "PlgvMymc7f3tQnJ6";
    public string ResourceId { get; set; } = "volc.speech.dialog";
    public int ConnectionTimeoutMs { get; set; } = 30000;
    public int ReceiveTimeoutMs { get; set; } = 60000;
}

public class AudioConfig
{
    public InputConfig Input { get; set; } = new();
    public OutputConfig Output { get; set; } = new();
}

public class InputConfig
{
    public int SampleRate { get; set; } = 16000;
    public int Channels { get; set; } = 1;
    public int FramesPerBuffer { get; set; } = 160;
    public string Format { get; set; } = "pcm";
}

public class OutputConfig
{
    public int SampleRate { get; set; } = 24000;
    public int Channels { get; set; } = 1;
    public int FramesPerBuffer { get; set; } = 512;
    public double LatencyMs { get; set; } = 10.0;
    public int BufferSeconds { get; set; } = 100;
}

public class DialogConfig
{
    public string BotName { get; set; } = "豆包";
    public string SystemRole { get; set; } = "你使用活泼灵动的女声，性格开朗，热爱生活。";
    public string SpeakingStyle { get; set; } = "你的说话风格简洁明了，语速适中，语调自然。";
    public Dictionary<string, object> Extra { get; set; } = new()
    {
        { "strict_audit", false },
        { "audit_response", "抱歉这个问题我无法回答，你可以换个其他话题，我会尽力为你提供帮助。" }
    };
    public string DefaultGreeting { get; set; } = "你好，我是豆包，有什么可以帮助你的吗？";
    public string TimeoutGreeting { get; set; } = "你还在吗？还想聊点什么吗？我超乐意继续陪你。";
    public int GreetingTimeoutSeconds { get; set; } = 30;
}

public class LoggingConfig
{
    public string LogLevel { get; set; } = "Information";
    public bool EnableConsoleLogging { get; set; } = true;
    public bool EnableFileLogging { get; set; } = false;
    public string LogFilePath { get; set; } = "logs/realtime-dialog.log";
    public bool EnableDetailedAudioLogging { get; set; } = false;
}