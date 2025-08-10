using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AiVox
{
    internal static class ServerResponse
    {
        private const int SampleRate = 24000;
        private const int Channels = 1;
        private const int FramesPerBuffer = 512;
        private const int BufferSeconds = 100; // 最多缓冲100秒数据

        // audio bytes and float buffer
        private static readonly object BufferLock = new();
        private static readonly System.Collections.Generic.List<float> Buffer =
            new(capacity: SampleRate * BufferSeconds);
        private static readonly System.Collections.Generic.List<byte> Audio =
            new(capacity: SampleRate * BufferSeconds * 4);

        internal static async Task RealtimeAPIOutputAudioAsync(CancellationToken ct, ClientWebSocket ws)
        {
            // start player (stub for now; parity gap recorded)
            var playerTask = StartPlayerAsync(ct);

            while (!ct.IsCancellationRequested)
            {
                Console.WriteLine("Waiting for message...");
                Message msg;
                try
                {
                    msg = await ReceiveMessageAsync(ws, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Receive message error: {ex}");
                    break;
                }

                switch (msg.Type)
                {
                    case MsgType.MsgTypeFullServer:
                        Console.WriteLine($"Receive text message (event={msg.Event}, session_id={msg.SessionID}): {Encoding.UTF8.GetString(msg.Payload ?? Array.Empty<byte>())}");
                        // session finished event
                        if (msg.Event == 152 || msg.Event == 153)
                        {
                            await playerTask.ConfigureAwait(false);
                            return;
                        }
                        // asr info event, clear audio buffer
                        if (msg.Event == 450)
                        {
                            // 清空本地音频缓存，等待接收下一轮的音频
                            lock (BufferLock)
                            {
                                Audio.Clear();
                                Buffer.Clear();
                            }
                            // 用户说话了，不需要触发连续SayHello引导用户交互了
                            Program.SignalQuery();
                            Program.SetIsUserQuerying(true);
                        }
                        // 发送ChatTTSText请求事件之后，收到tts_type为chat_tts_text的事件，清空本地缓存的S2S模型闲聊音频数据
                        if (msg.Event == 350 && Program.IsSendingChatTTSText())
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(msg.Payload ?? Array.Empty<byte>());
                                if (doc.RootElement.TryGetProperty("tts_type", out var ttsTypeElem) &&
                                    ttsTypeElem.ValueKind == JsonValueKind.String &&
                                    string.Equals(ttsTypeElem.GetString(), "chat_tts_text", StringComparison.Ordinal))
                                {
                                    lock (BufferLock)
                                    {
                                        Audio.Clear();
                                        Buffer.Clear();
                                    }
                                    Program.SetIsSendingChatTTSText(false);
                                }
                            }
                            catch { /* ignore malformed json */ }
                        }
                        if (msg.Event == 459)
                        {
                            Program.SetIsUserQuerying(false);
                        }
                        // 概率触发发送ChatTTSText请求
                        if (msg.Event == 459 && Random.Shared.Next(2) == 0)
                        {
                            _ = Task.Run(async () =>
                            {
                                Program.SetIsSendingChatTTSText(true);
                                Console.WriteLine("hit ChatTTSText event, start sending...");
                                try
                                {
                                    await ClientRequest.ChatTTSTextAsync(ws, msg.SessionID, new ChatTTSTextPayload
                                    {
                                        Start = true,
                                        End = false,
                                        Content = "这是第一轮TTS的开始和中间包事件，这两个合而为一了。"
                                    }, ct).ConfigureAwait(false);

                                    await ClientRequest.ChatTTSTextAsync(ws, msg.SessionID, new ChatTTSTextPayload
                                    {
                                        Start = false,
                                        End = true,
                                        Content = "这是第一轮TTS的结束事件。"
                                    }, ct).ConfigureAwait(false);

                                    await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);

                                    await ClientRequest.ChatTTSTextAsync(ws, msg.SessionID, new ChatTTSTextPayload
                                    {
                                        Start = true,
                                        End = false,
                                        Content = "这是第二轮TTS的开始和中间包事件，这两个合而为一了。"
                                    }, ct).ConfigureAwait(false);

                                    await ClientRequest.ChatTTSTextAsync(ws, msg.SessionID, new ChatTTSTextPayload
                                    {
                                        Start = false,
                                        End = true,
                                        Content = "这是第二轮TTS的结束事件。"
                                    }, ct).ConfigureAwait(false);
                                }
                                catch (OperationCanceledException) { }
                                catch (Exception ex)
                                {
                                    Console.Error.WriteLine($"ChatTTSText sequence error: {ex}");
                                }
                            }, ct);
                        }
                        break;

                    case MsgType.MsgTypeAudioOnlyServer:
                        Console.WriteLine($"Receive audio message (event={msg.Event}): session_id={msg.SessionID}");
                        HandleIncomingAudio(msg.Payload ?? Array.Empty<byte>());
                        lock (BufferLock)
                        {
                            Audio.AddRange(msg.Payload ?? Array.Empty<byte>());
                        }
                        break;

                    case MsgType.MsgTypeError:
                        Console.Error.WriteLine($"Receive Error message (code={msg.ErrorCode}): {Encoding.UTF8.GetString(msg.Payload ?? Array.Empty<byte>())}");
                        await playerTask.ConfigureAwait(false);
                        return;

                    default:
                        Console.Error.WriteLine($"Received unexpected message type: {msg.Type}");
                        await playerTask.ConfigureAwait(false);
                        return;
                }
            }

            await playerTask.ConfigureAwait(false);
        }

        private static async Task<Message> ReceiveMessageAsync(ClientWebSocket ws, CancellationToken ct)
        {
            // 读取一整帧并解析（与 ClientRequest.ReceiveFrameAsync 相似，但这里直接反序列化）
            var buffer = new ArrayBufferWriter<byte>(8192);
            var seg = new ArraySegment<byte>(new byte[4096]);
            WebSocketReceiveResult? result;
            do
            {
                result = await ws.ReceiveAsync(seg, ct).ConfigureAwait(false);
                if (result.Count > 0)
                {
                    buffer.Write(seg.AsSpan(0, result.Count));
                }
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new WebSocketException("WebSocket closed");
                }
            } while (!result.EndOfMessage);

            var data = buffer.WrittenSpan.ToArray();
            var prefix = data.Length > 100 ? data[..100] : data;
            Console.WriteLine($"Receive frame prefix: [{string.Join(", ", prefix)}]");

            var (msg, _) = BinaryProtocol.Unmarshal(data, ProtocolHelpers.ContainsSequence);
            return msg;
        }

        private static Task StartPlayerAsync(CancellationToken ct)
        {
            // NOTE: Go uses PortAudio output stream to play float32 samples.
            // .NET parity gap: PortAudioSharp integration will be implemented later to mirror:
            //  - SampleRate=24000, Channels=1, FramesPerBuffer=512
            //  - Callback: pull from Buffer under lock into out[] float
            // For now: stub that waits for cancellation, then saves PCM buffer to file (like Go).
            return Task.Run(async () =>
            {
                Console.WriteLine("PortAudio output stream stub started for playback. TODO: integrate PortAudioSharp.");
                try
                {
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                SaveAudioToPCMFile("output.pcm");
                Console.WriteLine("PortAudio output stream stub stopped.");
            }, ct);
        }

        private static void HandleIncomingAudio(byte[] data)
        {
            if (Program.IsSendingChatTTSText())
            {
                return;
            }
            Console.WriteLine($"Received audio byte len: {data.Length}, float32 len: {data.Length / 4}");
            int sampleCount = data.Length / 4;
            lock (BufferLock)
            {
                // ensure capacity up to 100 seconds worth
                for (int i = 0; i < sampleCount; i++)
                {
                    // little-endian float32
                    uint bits = BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(data, i * 4, 4));
                    float sample = BitConverter.Int32BitsToSingle((int)bits);
                    Buffer.Add(sample);
                }
                int max = SampleRate * BufferSeconds;
                if (Buffer.Count > max)
                {
                    Buffer.RemoveRange(0, Buffer.Count - max);
                }
            }
        }

        private static void SaveAudioToPCMFile(string filename)
        {
            lock (BufferLock)
            {
                if (Audio.Count == 0)
                {
                    Console.WriteLine("No audio data to save.");
                    return;
                }
                var pcmPath = Path.Combine("./", filename);
                File.WriteAllBytes(pcmPath, Audio.ToArray());
            }
        }
    }
}
