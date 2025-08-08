using Microsoft.Extensions.Logging;
using RealtimeDialog.Core.Audio;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RealtimeDialog.Core;

public class AudioRecordingTest
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== NAudio录音测试程序 ===");
        Console.WriteLine("按任意键开始录音，按Ctrl+C停止...");
        Console.ReadKey();
        
        // 创建日志工厂
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var logger = loggerFactory.CreateLogger<AudioRecorder>();
        
        // 创建录音器
        var recorder = new AudioRecorder(logger);
        
        // 订阅音频数据事件
        int audioDataCount = 0;
        recorder.AudioDataReceived += (audioData) =>
        {
            audioDataCount++;
            Console.WriteLine($"收到音频数据 #{audioDataCount}: {audioData.Length} 样本");
            
            // 计算音频强度（简单的RMS计算）
            double sum = 0;
            foreach (var sample in audioData)
            {
                sum += sample * sample;
            }
            var rms = Math.Sqrt(sum / audioData.Length);
            var db = 20 * Math.Log10(rms / 32768.0); // 转换为分贝
            
            Console.WriteLine($"  音频强度: {rms:F2} (约 {db:F1} dB)");
        };
        
        try
        {
            // 初始化录音器
            Console.WriteLine("初始化录音器...");
            var initialized = await recorder.InitializeAsync();
            if (!initialized)
            {
                Console.WriteLine("❌ 录音器初始化失败");
                return;
            }
            Console.WriteLine("✅ 录音器初始化成功");
            
            // 开始录音
            Console.WriteLine("开始录音...");
            var started = await recorder.StartRecordingAsync();
            if (!started)
            {
                Console.WriteLine("❌ 录音启动失败");
                return;
            }
            Console.WriteLine("✅ 录音已启动，请对着麦克风说话...");
            
            // 设置取消令牌
            using var cts = new CancellationToken