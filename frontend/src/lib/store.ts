import { create } from 'zustand';
import { AudioConfig, SessionStatus, ConversationMessage } from '../types/audio';
import { WebAudioManager } from './audioManager';
import { ConversationClient } from './signalrClient';

interface ConversationState {
  // 连接状态
  isConnected: boolean;
  isConnecting: boolean;
  connectionError: string | null;
  
  // 会话状态
  sessionId: string | null;
  sessionStatus: SessionStatus;
  audioConfig: AudioConfig;
  
  // 音频状态
  isRecording: boolean;
  isPlaying: boolean;
  audioError: string | null;
  
  // 对话消息
  messages: ConversationMessage[];
  
  // 服务实例
  audioManager: WebAudioManager | null;
  signalrClient: ConversationClient | null;
  
  // 序列号计数器
  sequenceNumber: number;
}

interface ConversationActions {
  // 初始化
  initialize: (serverUrl: string) => Promise<void>;
  
  // 连接管理
  connect: () => Promise<void>;
  disconnect: () => Promise<void>;
  
  // 会话管理
  startConversation: () => Promise<void>;
  stopConversation: () => Promise<void>;
  
  // 音频控制
  startRecording: () => Promise<void>;
  stopRecording: () => void;
  
  // 消息管理
  addMessage: (message: Omit<ConversationMessage, 'id'>) => void;
  clearMessages: () => void;
  
  // 状态更新
  setConnectionStatus: (connected: boolean) => void;
  setSessionStatus: (status: SessionStatus) => void;
  setAudioConfig: (config: AudioConfig) => void;
  setError: (error: string | null) => void;
  
  // 清理
  cleanup: () => void;
}

type ConversationStore = ConversationState & ConversationActions;

export const useConversationStore = create<ConversationStore>((set, get) => ({
  // 初始状态
  isConnected: false,
  isConnecting: false,
  connectionError: null,
  sessionId: null,
  sessionStatus: SessionStatus.Idle,
  audioConfig: WebAudioManager.getDefaultConfig(),
  isRecording: false,
  isPlaying: false,
  audioError: null,
  messages: [],
  audioManager: null,
  signalrClient: null,
  sequenceNumber: 0,

  // 初始化
  initialize: async (serverUrl: string) => {
    const state = get();
    
    // 检查浏览器支持
    if (!WebAudioManager.isSupported()) {
      set({ audioError: '浏览器不支持Web Audio API' });
      return;
    }
    
    try {
      // 创建音频管理器
      const audioManager = new WebAudioManager();
      
      // 创建SignalR客户端
      const signalrClient = new ConversationClient(serverUrl);
      
      // 设置SignalR事件处理器
      signalrClient.onConnectionConfirm = (response) => {
        console.log('连接确认:', response);
        if (response.status === 'connected') {
          set({ 
            sessionStatus: SessionStatus.Active,
            audioConfig: response.audioConfig 
          });
        } else {
          set({ 
            connectionError: response.errorMessage || '连接失败',
            sessionStatus: SessionStatus.Error 
          });
        }
      };
      
      signalrClient.onAudioDataReceived = (response) => {
        // 播放接收到的音频数据
        const audioData = new Float32Array(signalrClient.base64ToArrayBuffer(response.audioData as any));
        audioManager.playAudio(audioData, state.audioConfig).catch(console.error);
      };
      
      signalrClient.onTranscriptionReceived = (response) => {
        // 添加转录文本到消息列表
        get().addMessage({
          text: response.text,
          isUser: response.isUser,
          timestamp: response.timestamp
        });
      };
      
      signalrClient.onStatusUpdate = (response) => {
        set({ sessionStatus: response.status });
      };
      
      signalrClient.onError = (response) => {
        set({ 
          connectionError: response.errorMessage,
          sessionStatus: SessionStatus.Error 
        });
      };
      
      signalrClient.onConnectionStateChanged = (connected) => {
        set({ 
          isConnected: connected,
          isConnecting: false,
          connectionError: connected ? null : '连接已断开'
        });
      };
      
      set({ 
        audioManager,
        signalrClient,
        connectionError: null
      });
      
    } catch (error) {
      console.error('初始化失败:', error);
      set({ 
        connectionError: error instanceof Error ? error.message : '初始化失败',
        isConnecting: false
      });
    }
  },

  // 连接到服务器
  connect: async () => {
    const { signalrClient } = get();
    if (!signalrClient) {
      set({ connectionError: 'SignalR客户端未初始化' });
      return;
    }
    
    set({ isConnecting: true, connectionError: null });
    
    try {
      await signalrClient.initialize();
    } catch (error) {
      console.error('连接失败:', error);
      set({ 
        connectionError: error instanceof Error ? error.message : '连接失败',
        isConnecting: false
      });
    }
  },

  // 断开连接
  disconnect: async () => {
    const { signalrClient } = get();
    if (signalrClient) {
      await signalrClient.disconnect();
    }
    set({ 
      isConnected: false,
      sessionId: null,
      sessionStatus: SessionStatus.Idle
    });
  },

  // 开始对话
  startConversation: async () => {
    const { signalrClient, audioConfig } = get();
    if (!signalrClient) {
      set({ connectionError: 'SignalR客户端未初始化' });
      return;
    }
    
    const sessionId = `session_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    
    try {
      await signalrClient.startConversation({
        sessionId,
        audioConfig,
        userId: undefined
      });
      
      set({ 
        sessionId,
        sessionStatus: SessionStatus.Initializing,
        sequenceNumber: 0
      });
    } catch (error) {
      console.error('开始对话失败:', error);
      set({ 
        connectionError: error instanceof Error ? error.message : '开始对话失败',
        sessionStatus: SessionStatus.Error
      });
    }
  },

  // 停止对话
  stopConversation: async () => {
    const { signalrClient, sessionId, audioManager } = get();
    
    // 停止录音
    if (audioManager) {
      audioManager.stopRecording();
    }
    
    // 发送停止对话请求
    if (signalrClient && sessionId) {
      try {
        await signalrClient.stopConversation(sessionId);
      } catch (error) {
        console.error('停止对话失败:', error);
      }
    }
    
    set({ 
      sessionId: null,
      sessionStatus: SessionStatus.Idle,
      isRecording: false,
      sequenceNumber: 0
    });
  },

  // 开始录音
  startRecording: async () => {
    const { audioManager, signalrClient, sessionId, audioConfig } = get();
    
    if (!audioManager || !signalrClient || !sessionId) {
      set({ audioError: '服务未初始化或会话未开始' });
      return;
    }
    
    try {
      await audioManager.startRecording(audioConfig, (audioData) => {
        // 发送音频数据到服务器
        const currentSequence = get().sequenceNumber;
        signalrClient.sendAudioData({
          sessionId,
          audioData: audioData.buffer,
          timestamp: Date.now(),
          sequenceNumber: currentSequence
        }).catch(console.error);
        
        // 更新序列号
        set({ sequenceNumber: currentSequence + 1 });
      });
      
      set({ 
        isRecording: true,
        audioError: null,
        sessionStatus: SessionStatus.Listening
      });
    } catch (error) {
      console.error('开始录音失败:', error);
      set({ 
        audioError: error instanceof Error ? error.message : '开始录音失败',
        isRecording: false
      });
    }
  },

  // 停止录音
  stopRecording: () => {
    const { audioManager } = get();
    if (audioManager) {
      audioManager.stopRecording();
    }
    set({ 
      isRecording: false,
      sessionStatus: SessionStatus.Processing
    });
  },

  // 添加消息
  addMessage: (message) => {
    const newMessage: ConversationMessage = {
      ...message,
      id: `msg_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`
    };
    
    set((state) => ({
      messages: [...state.messages, newMessage]
    }));
  },

  // 清空消息
  clearMessages: () => {
    set({ messages: [] });
  },

  // 设置连接状态
  setConnectionStatus: (connected) => {
    set({ isConnected: connected });
  },

  // 设置会话状态
  setSessionStatus: (status) => {
    set({ sessionStatus: status });
  },

  // 设置音频配置
  setAudioConfig: (config) => {
    set({ audioConfig: config });
  },

  // 设置错误
  setError: (error) => {
    set({ connectionError: error });
  },

  // 清理资源
  cleanup: () => {
    const { audioManager, signalrClient } = get();
    
    if (audioManager) {
      audioManager.dispose();
    }
    
    if (signalrClient) {
      signalrClient.disconnect().catch(console.error);
    }
    
    set({
      audioManager: null,
      signalrClient: null,
      isConnected: false,
      sessionId: null,
      sessionStatus: SessionStatus.Idle,
      isRecording: false,
      messages: [],
      connectionError: null,
      audioError: null
    });
  }
}));

// 清理函数，在应用卸载时调用
export const cleanupConversationStore = () => {
  useConversationStore.getState().cleanup();
};