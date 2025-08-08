package main

import (
	"context"
	"flag"
	"math/rand"
	"net/http"
	"net/url"
	"os"
	"os/signal"
	"sync"
	"syscall"
	"time"

	"github.com/golang/glog"
	"github.com/google/uuid"
	"github.com/gordonklaus/portaudio"
	"github.com/gorilla/websocket"
)

var (
	// 客户接入需要修改的参数
	appid       = ""
	accessToken = ""

	// 无需修改的参数
	wsURL       = url.URL{Scheme: "wss", Host: "openspeech.bytedance.com", Path: "/api/v3/realtime/dialogue"}
	protocol    = NewBinaryProtocol()
	dialogID    = ""
	wsWriteLock sync.Mutex
	queryChan   = make(chan struct{}, 10)
)

func init() {
	protocol.SetVersion(Version1)
	protocol.SetHeaderSize(HeaderSize4)
	protocol.SetSerialization(SerializationJSON)
	protocol.SetCompression(CompressionNone, nil)
	protocol.containsSequence = ContainsSequence
	rand.New(rand.NewSource(time.Now().UnixNano()))
}

// 流式合成
func realTimeDialog(ctx context.Context, c *websocket.Conn, sessionID string) {
	err := startConnection(c)
	if err != nil {
		glog.Errorf("realTimeDialog startConnection error: %v", err)
		return
	}
	err = startSession(c, sessionID, &StartSessionPayload{
		TTS: TTSPayload{
			AudioConfig: AudioConfig{
				Channel:    1,
				Format:     "pcm",
				SampleRate: 24000,
			},
		},
		Dialog: DialogPayload{
			BotName:       "豆包",
			SystemRole:    "你使用活泼灵动的女声，性格开朗，热爱生活。",
			SpeakingStyle: "你的说话风格简洁明了，语速适中，语调自然。",
			Extra: map[string]interface{}{
				"strict_audit":   false,
				"audit_response": "抱歉这个问题我无法回答，你可以换个其他话题，我会尽力为你提供帮助。",
			},
		},
	})
	if err != nil {
		glog.Errorf("realTimeDialog startSession error: %v", err)
		return
	}
	// 模拟发送问候语
	err = sayHello(c, sessionID, &SayHelloPayload{
		Content: "你好，我是豆包，有什么可以帮助你的吗？",
	})
	if err != nil {
		glog.Errorf("realTimeDialog sayHello error: %v", err)
		return
	}
	go func() {
		for {
			select {
			case <-ctx.Done():
				return
			case <-queryChan:
				glog.Info("Received user query signal, starting real-time dialog...")
			case <-time.After(30 * time.Second):
				glog.Info("Timeout waiting for user query, start new SayHello request...")
				err = sayHello(c, sessionID, &SayHelloPayload{
					Content: "你还在吗？还想聊点什么吗？我超乐意继续陪你。",
				})
			}
		}
	}()
	// 模拟发送音频流到服务端
	sendAudio(ctx, c, sessionID)

	// 接收服务端返回数据
	realtimeAPIOutputAudio(ctx, c)

	// 结束对话，断开websocket连接
	err = finishConnection(c)
	if err != nil {
		glog.Errorf("Failed to finish connection: %v", err)
	}
	glog.Info("realTimeDialog finished.")
}

func main() {
	_ = flag.Set("logtostderr", "true")
	flag.Parse()

	if err := portaudio.Initialize(); err != nil {
		glog.Fatalf("portaudio initialize error: %v", err)
		return
	}
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer func() {
		err := portaudio.Terminate()
		if err != nil {
			glog.Errorf("Failed to terminate portaudio: %v", err)
		}
		stop()
	}()

	conn, resp, err := websocket.DefaultDialer.DialContext(ctx, wsURL.String(), http.Header{
		"X-Api-Resource-Id": []string{"volc.speech.dialog"},
		"X-Api-Access-Key":  []string{accessToken},
		"X-Api-App-Key":     []string{"PlgvMymc7f3tQnJ6"},
		"X-Api-App-ID":      []string{appid},
		"X-Api-Connect-Id":  []string{uuid.New().String()},
	})
	if err != nil {
		glog.Errorf("Websocket dial error: %v", err)
		return
	}
	defer func() {
		if resp != nil {
			glog.Infof("Websocket dial response logid: %s", resp.Header.Get("X-Tt-Logid"))
		}
		close(queryChan)
		glog.Infof("Websocket response dialogID: %s", dialogID)
		_ = conn.Close()
	}()

	realTimeDialog(ctx, conn, uuid.New().String())
}
