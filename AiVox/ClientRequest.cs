using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace AiVox
{
    // DTOs 1:1 to Go structs (json tag parity)
    internal sealed class StartSessionPayload
    {
        [JsonPropertyName("tts")]
        public TTSPayload TTS { get; set; } = new TTSPayload();

        [JsonPropertyName("dialog")]
        public DialogPayload Dialog { get; set; } = new DialogPayload();
    }

    internal sealed class SayHelloPayload
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    internal sealed class ChatTTSTextPayload
    {
        [JsonPropertyName("start")]
        public bool Start { get; set; }

        [JsonPropertyName("end")]
        public bool End { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    internal sealed class TTSPayload
    {
        [JsonPropertyName("speaker")]
        public string? Speaker { get; set; }

        [JsonPropertyName("audio_config")]
        public AudioConfig AudioConfig { get; set; } = new AudioConfig();
    }

    internal sealed class AudioConfig
    {
        [JsonPropertyName("channel")]
        public int Channel { get; set; }

        [JsonPropertyName("format")]
        public string Format { get; set; } = string.Empty;

        [JsonPropertyName("sample_rate")]
        public int SampleRate { get; set; }
    }

    internal sealed class DialogPayload
    {
        [JsonPropertyName("dialog_id")]
        public string DialogID { get; set; } = string.Empty;

        [JsonPropertyName("bot_name")]
        public string BotName { get; set; } = string.Empty;

        [JsonPropertyName("system_role")]
        public string SystemRole { get; set; } = string.Empty;

        [JsonPropertyName("speaking_style")]
        public string SpeakingStyle { get; set; } = string.Empty;

        [JsonPropertyName("extra")]
        public System.Collections.Generic.Dictionary<string, object?> Extra { get; set; } = new(); // map[string]interface{} parity
    }

    internal static class ClientRequest
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = null // honor JsonPropertyName exactly
        };

        // StartConnection: event=1, payload={}
        internal static async Task StartConnectionAsync(ClientWebSocket ws, CancellationToken ct)
        {
            var msg = Message.NewMessage(MsgType.MsgTypeFullClient, MsgTypeFlagBits.MsgTypeFlagWithEvent);
            msg.Event = 1;
            msg.Payload = Encoding.UTF8.GetBytes("{}");

            var frame = Program.Protocol.Marshal(msg);
            Console.WriteLine($"StartConnection frame len: {frame.Length}");

            await SendFrameAsync(ws, frame, ct);

            // Read ConnectionStarted (type=FullServer, event=50)
            var (mt, data) = await ReceiveFrameAsync(ws, ct);
            if (mt != WebSocketMessageType.Binary && mt != WebSocketMessageType.Text)
                throw new Exception($"unexpected WebSocket message type: {mt}");

            var (resp, _) = BinaryProtocol.Unmarshal(data, ProtocolHelpers.ContainsSequence);
            if (resp.Type != MsgType.MsgTypeFullServer)
                throw new Exception($"unexpected ConnectionStarted message type: {resp.Type}");
            if (resp.Event != 50)
                throw new Exception($"unexpected response event ({resp.Event}) for StartConnection request");

            Console.WriteLine($"Connection started (event={resp.Event}) connectID: {resp.ConnectID}, payload: {Encoding.UTF8.GetString(resp.Payload ?? Array.Empty<byte>())}");
        }

        // StartSession: event=100, payload=req JSON, then expect event=150 and parse dialog_id
        internal static async Task StartSessionAsync(ClientWebSocket ws, string sessionId, StartSessionPayload req, CancellationToken ct)
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(req, JsonOpts);

            var msg = Message.NewMessage(MsgType.MsgTypeFullClient, MsgTypeFlagBits.MsgTypeFlagWithEvent);
            msg.Event = 100;
            msg.SessionID = sessionId;
            msg.Payload = payload;

            var frame = Program.Protocol.Marshal(msg);
            Console.WriteLine($"StartSession frame len: {frame.Length}");

            await SendFrameAsync(ws, frame, ct);

            var (mt, data) = await ReceiveFrameAsync(ws, ct);
            if (mt != WebSocketMessageType.Binary && mt != WebSocketMessageType.Text)
                throw new Exception($"unexpected WebSocket message type: {mt}");

            var (resp, _) = BinaryProtocol.Unmarshal(data, ProtocolHelpers.ContainsSequence);
            if (resp.Type != MsgType.MsgTypeFullServer)
                throw new Exception($"unexpected SessionStarted message type: {resp.Type}");
            if (resp.Event != 150)
                throw new Exception($"unexpected response event ({resp.Event}) for StartSession request");

            Console.WriteLine($"SessionStarted payload: {Encoding.UTF8.GetString(resp.Payload ?? Array.Empty<byte>())}");
            // parse dialog_id
            using var jsonDoc = JsonDocument.Parse(resp.Payload);
            if (jsonDoc.RootElement.TryGetProperty("dialog_id", out var dialogIdProp))
            {
                Program.DialogId = dialogIdProp.GetString() ?? string.Empty;
            }
        }

        // SayHello: event=300
        internal static async Task SayHelloAsync(ClientWebSocket ws, string sessionId, SayHelloPayload req, CancellationToken ct)
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(req, JsonOpts);
            Console.WriteLine($"SayHello payload: {Encoding.UTF8.GetString(payload)}");

            var msg = Message.NewMessage(MsgType.MsgTypeFullClient, MsgTypeFlagBits.MsgTypeFlagWithEvent);
            msg.Event = 300;
            msg.SessionID = sessionId;
            msg.Payload = payload;

            var frame = Program.Protocol.Marshal(msg);
            Console.WriteLine($"SayHello frame len: {frame.Length}");

            await SendFrameAsync(ws, frame, ct);
        }

        // ChatTTSText: event=500
        internal static async Task ChatTTSTextAsync(ClientWebSocket ws, string sessionId, ChatTTSTextPayload req, CancellationToken ct)
        {
            if (Program.IsUserQuerying())
            {
                Console.Error.WriteLine("chatTTSText cant be called while user is querying.");
                return;
            }

            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(req, JsonOpts);
            Console.WriteLine($"ChatTTSText payload: {Encoding.UTF8.GetString(payload)}");

            Program.Protocol.SetSerialization(SerializationBits.SerializationJSON);
            var msg = Message.NewMessage(MsgType.MsgTypeFullClient, MsgTypeFlagBits.MsgTypeFlagWithEvent);
            msg.Event = 500;
            msg.SessionID = sessionId;
            msg.Payload = payload;

            var frame = Program.Protocol.Marshal(msg);
            Console.WriteLine($"ChatTTSText frame len: {frame.Length}");

            await SendFrameAsync(ws, frame, ct);
        }

        // SendAudio: Goroutine-like microphone capture loop sending MsgTypeAudioOnlyClient (event=200)
        // NOTE: PortAudioSharp callback integration requires unsafe delegate and stream lifecycle.
        // For strict 1:1 parity we will implement this in a dedicated step due to native binding differences.
        // Currently this method sets serialization RAW and starts a background Task stub.
        internal static void SendAudio(CancellationToken ct, ClientWebSocket ws, string sessionId)
        {
            // Set serialization to RAW for audio frames, as in Go
            Program.Protocol.SetSerialization(SerializationBits.SerializationRaw);

            _ = Task.Run(() =>
            {
                IntPtr stream = IntPtr.Zero;
                try
                {
                    const int sampleRate = 16000;
                    const int channels = 1;
                    const int framesPerBuffer = 160;

                    // Open default input stream: int16 mono @ 16kHz
                    int rc = PortAudioNative.Pa_OpenDefaultStream(
                        out stream,
                        channels,
                        0,
                        PortAudioNative.PaSampleFormat.paInt16,
                        sampleRate,
                        (ulong)framesPerBuffer,
                        IntPtr.Zero,
                        IntPtr.Zero
                    );
                    if (rc != PortAudioNative.paNoError)
                    {
                        Console.Error.WriteLine($"Failed to open microphone input stream: {PortAudioNative.ErrorText(rc)}");
                        return;
                    }

                    rc = PortAudioNative.Pa_StartStream(stream);
                    if (rc != PortAudioNative.paNoError)
                    {
                        Console.Error.WriteLine($"Failed to start microphone input stream: {PortAudioNative.ErrorText(rc)}");
                        return;
                    }
                    Console.WriteLine("Microphone input stream started. please speak...");

                    int samplesPerBuffer = framesPerBuffer * channels;
                    var samples = new short[samplesPerBuffer];
                    var audioBytes = new byte[samplesPerBuffer * 2];

                    while (!ct.IsCancellationRequested)
                    {
                        // Blocking read one buffer of frames from PortAudio (int16)
                        var handle = GCHandle.Alloc(samples, GCHandleType.Pinned);
                        try
                        {
                            rc = PortAudioNative.Pa_ReadStream(stream, handle.AddrOfPinnedObject(), (ulong)framesPerBuffer);
                        }
                        finally
                        {
                            handle.Free();
                        }
                        if (rc != PortAudioNative.paNoError)
                        {
                            Console.Error.WriteLine($"Error reading audio: {PortAudioNative.ErrorText(rc)}");
                            break;
                        }

                        // Convert short[] to PCM S16LE bytes (low byte first), same as Go
                        for (int i = 0; i < samplesPerBuffer; i++)
                        {
                            short s = samples[i];
                            audioBytes[i * 2] = (byte)(s & 0xFF);
                            audioBytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                        }

                        var msg = Message.NewMessage(MsgType.MsgTypeAudioOnlyClient, MsgTypeFlagBits.MsgTypeFlagWithEvent);
                        msg.Event = 200;
                        msg.SessionID = sessionId;
                        msg.Payload = audioBytes;

                        var frame = Program.Protocol.Marshal(msg);

                        lock (Program.WsWriteLock)
                        {
                            ws.SendAsync(frame.AsMemory(), WebSocketMessageType.Binary, true, ct).GetAwaiter().GetResult();
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Audio loop error: {ex}");
                }
                finally
                {
                    try { if (stream != IntPtr.Zero) PortAudioNative.Pa_StopStream(stream); } catch { }
                    try { if (stream != IntPtr.Zero) PortAudioNative.Pa_CloseStream(stream); } catch { }
                    Console.WriteLine("Microphone input stream stopped.");
                    // On exit, send FinishSession (like Go)
                    _ = FinishSessionAsync(ws, sessionId, CancellationToken.None);
                }
            }, ct);
        }

        // FinishSession: event=102
        internal static async Task FinishSessionAsync(ClientWebSocket ws, string sessionId, CancellationToken ct)
        {
            var msg = Message.NewMessage(MsgType.MsgTypeFullClient, MsgTypeFlagBits.MsgTypeFlagWithEvent);
            msg.Event = 102;
            msg.SessionID = sessionId;
            msg.Payload = Encoding.UTF8.GetBytes("{}");

            var frame = Program.Protocol.Marshal(msg);
            await SendFrameAsync(ws, frame, ct);
            Console.WriteLine("FinishSession request is sent.");
        }

        // FinishConnection: event=2, expect event=52 response
        internal static async Task FinishConnectionAsync(ClientWebSocket ws, CancellationToken ct)
        {
            var msg = Message.NewMessage(MsgType.MsgTypeFullClient, MsgTypeFlagBits.MsgTypeFlagWithEvent);
            msg.Event = 2;
            msg.Payload = Encoding.UTF8.GetBytes("{}");

            var frame = Program.Protocol.Marshal(msg);
            await SendFrameAsync(ws, frame, ct);

            var (mt, data) = await ReceiveFrameAsync(ws, ct);
            if (mt != WebSocketMessageType.Binary && mt != WebSocketMessageType.Text)
                throw new Exception($"unexpected WebSocket message type: {mt}");

            var (resp, _) = BinaryProtocol.Unmarshal(data, ProtocolHelpers.ContainsSequence);
            if (resp.Type != MsgType.MsgTypeFullServer)
                throw new Exception($"unexpected ConnectionFinished message type: {resp.Type}");
            if (resp.Event != 52)
                throw new Exception($"unexpected response event ({resp.Event}) for FinishConnection request");

            Console.WriteLine($"Connection finished (event={resp.Event}).");
        }

        // Helpers
        private static Task SendFrameAsync(ClientWebSocket ws, byte[] frame, CancellationToken ct)
        {
            lock (Program.WsWriteLock)
            {
                // serialize locking to match Go's wsWriteLock
                ws.SendAsync(frame.AsMemory(), WebSocketMessageType.Binary, true, ct).GetAwaiter().GetResult();
            }
            return Task.CompletedTask;
        }

        private static async Task<(WebSocketMessageType, byte[])> ReceiveFrameAsync(ClientWebSocket ws, CancellationToken ct)
        {
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

            // Log prefix like Go (cap at 100 bytes)
            var data = buffer.WrittenSpan.ToArray();
            var prefixLen = Math.Min(100, data.Length);
            Console.WriteLine($"Receive frame prefix: [{string.Join(", ", data.AsSpan(0, prefixLen).ToArray())}]");

            return (result.MessageType, data);
        }
    }
}
