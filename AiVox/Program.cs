using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AiVox
{
    internal class Program
    {
        // 客户接入需要修改的参数（占位，保持与 Go 一致的字段命名语义）
        internal static string AppId = "7482136989";
        internal static string AccessToken = "4akGrrTRlikgCCxBVSi0f3gXQ2uGR8bt";

        // 无需修改的参数
        private static readonly Uri WsUri = new("wss://openspeech.bytedance.com/api/v3/realtime/dialogue");
        internal static readonly BinaryProtocol Protocol = new BinaryProtocol();
        internal static string DialogId = "";
        internal static readonly object WsWriteLock = new object();
        internal static readonly Channel<bool> QueryChan = Channel.CreateBounded<bool>(new BoundedChannelOptions(10)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        // sync/atomic.Bool 等价实现
        private static int _isSendingChatTTSText; // 0=false,1=true
        private static int _isUserQuerying;       // 0=false,1=true

        static Program()
        {
            // 等价于 Go 的 init()
            Protocol.SetVersion(VersionBits.Version1);
            Protocol.SetHeaderSize(HeaderSizeBits.HeaderSize4);
            Protocol.SetSerialization(SerializationBits.SerializationJSON);
            Protocol.SetCompression(CompressionBits.CompressionNone, null);
            Protocol.containsSequence = ProtocolHelpers.ContainsSequence;
            // 随机种子：.NET Random.Shared 已初始化；无需显式设定
        }

        internal static bool IsSendingChatTTSText() => Interlocked.CompareExchange(ref _isSendingChatTTSText, 0, 0) != 0;
        internal static void SetIsSendingChatTTSText(bool v) => Interlocked.Exchange(ref _isSendingChatTTSText, v ? 1 : 0);

        internal static bool IsUserQuerying() => Interlocked.CompareExchange(ref _isUserQuerying, 0, 0) != 0;
        internal static void SetIsUserQuerying(bool v) => Interlocked.Exchange(ref _isUserQuerying, v ? 1 : 0);

        internal static void SignalQuery()
        {
            // 非阻塞写入，等价 Go 的带缓冲 channel
            QueryChan.Writer.TryWrite(true);
        }

        public static async Task Main(string[] args)
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            // PortAudio 初始化/释放：与 Go 一致，但绑定差异保留为后续等价实现（记录差异）
            try
            {
                // TODO: PortAudioSharp.PortAudio.Initialize();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"PortAudio initialize error (recorded gap): {ex.Message}");
            }

            using var ws = new ClientWebSocket();
            // 对应 Go 的握手头
            ws.Options.SetRequestHeader("X-Api-Resource-Id", "volc.speech.dialog");
            ws.Options.SetRequestHeader("X-Api-Access-Key", AccessToken);
            ws.Options.SetRequestHeader("X-Api-App-Key", "PlgvMymc7f3tQnJ6");
            ws.Options.SetRequestHeader("X-Api-App-ID", AppId);
            ws.Options.SetRequestHeader("X-Api-Connect-Id", Guid.NewGuid().ToString());

            try
            {
                await ws.ConnectAsync(WsUri, cts.Token);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Websocket dial error: {ex}");
                return;
            }

            // 说明：ClientWebSocket 不暴露响应头（Go 版本打印 X-Tt-Logid），该能力缺失已记录为差异点。
            var sessionId = Guid.NewGuid().ToString();

            try
            {
                await RealTimeDialog(cts.Token, ws, sessionId);
            }
            finally
            {
                try
                {
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None);
                }
                catch { /* ignore */ }
                QueryChan.Writer.TryComplete();
                Console.WriteLine($"Websocket response dialogID: {DialogId}");
            }

            try
            {
                // TODO: PortAudioSharp.PortAudio.Terminate();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to terminate portaudio: {ex.Message}");
            }
        }

        private static async Task RealTimeDialog(CancellationToken ct, ClientWebSocket ws, string sessionId)
        {
            try
            {
                await ClientRequest.StartConnectionAsync(ws, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"realTimeDialog startConnection error: {ex}");
                return;
            }

            try
            {
                var startReq = new StartSessionPayload
                {
                    TTS = new TTSPayload
                    {
                        AudioConfig = new AudioConfig
                        {
                            Channel = 1,
                            Format = "pcm",
                            SampleRate = 24000
                        }
                    },
                    Dialog = new DialogPayload
                    {
                        DialogID = "",
                        BotName = "豆包",
                        SystemRole = "你使用活泼灵动的女声，性格开朗，热爱生活。",
                        SpeakingStyle = "你的说话风格简洁明了，语速适中，语调自然。",
                        Extra = new Dictionary<string, object?>
                        {
                            ["strict_audit"] = false,
                            ["audit_response"] = "抱歉这个问题我无法回答，你可以换个其他话题，我会尽力为你提供帮助。",
                        }
                    }
                };
                await ClientRequest.StartSessionAsync(ws, sessionId, startReq, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"realTimeDialog startSession error: {ex}");
                return;
            }

            try
            {
                await ClientRequest.SayHelloAsync(ws, sessionId, new SayHelloPayload
                {
                    Content = "你好，我是豆包，有什么可以帮助你的吗？"
                }, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"realTimeDialog sayHello error: {ex}");
                return;
            }

            // 背景循环：等待 queryChan 或 30s 超时后再次 SayHello
            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    var delayTask = Task.Delay(TimeSpan.FromSeconds(30), ct);
                    var readTask = QueryChan.Reader.ReadAsync(ct).AsTask();
                    var completed = await Task.WhenAny(delayTask, readTask).ConfigureAwait(false);
                    if (completed == readTask)
                    {
                        Console.WriteLine("Received user query signal, starting real-time dialog...");
                    }
                    else
                    {
                        Console.WriteLine("Timeout waiting for user query, start new SayHello request...");
                        try
                        {
                            await ClientRequest.SayHelloAsync(ws, sessionId, new SayHelloPayload
                            {
                                Content = "你还在吗？还想聊点什么吗？我超乐意继续陪你。"
                            }, ct).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"realTimeDialog sayHello error: {ex.Message}");
                        }
                    }
                }
            }, ct);

            // 模拟发送音频流到服务端（当前为 PortAudioSharp 待实现占位）
            ClientRequest.SendAudio(ct, ws, sessionId);

            // 接收服务端返回数据（含音频播放的待实现占位）
            await ServerResponse.RealtimeAPIOutputAudioAsync(ct, ws);

            // 结束对话，断开连接
            try
            {
                await ClientRequest.FinishConnectionAsync(ws, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to finish connection: {ex}");
            }
            Console.WriteLine("realTimeDialog finished.");
        }
    }
}
