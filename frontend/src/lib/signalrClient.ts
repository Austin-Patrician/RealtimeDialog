import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import {
  StartConversationRequest,
  AudioDataRequest,
  ConnectionConfirmResponse,
  AudioDataResponse,
  TranscriptionResponse,
  StatusUpdateResponse,
  ErrorResponse
} from '../types/signalr';

export class ConversationClient {
  private connection: HubConnection | null = null;
  private serverUrl: string;
  private isConnected = false;

  // 事件回调
  public onConnectionConfirm: ((response: ConnectionConfirmResponse) => void) | null = null;
  public onAudioDataReceived: ((response: AudioDataResponse) => void) | null = null;
  public onTranscriptionReceived: ((response: TranscriptionResponse) => void) | null = null;
  public onStatusUpdate: ((response: StatusUpdateResponse) => void) | null = null;
  public onError: ((response: ErrorResponse) => void) | null = null;
  public onConnectionStateChanged: ((connected: boolean) => void) | null = null;

  constructor(serverUrl: string) {
    this.serverUrl = serverUrl;
  }

  // 初始化连接
  async initialize(): Promise<void> {
    try {
      this.connection = new HubConnectionBuilder()
        .withUrl(`${this.serverUrl}/conversationHub`)
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // 重连延迟策略：0s, 2s, 10s, 30s, 然后每30s重试一次
            if (retryContext.previousRetryCount < 3) {
              return [0, 2000, 10000][retryContext.previousRetryCount];
            }
            return 30000;
          }
        })
        .configureLogging(LogLevel.Information)
        .build();

      this.setupEventHandlers();
      await this.connection.start();
      
      this.isConnected = true;
      this.onConnectionStateChanged?.(true);
      console.log('SignalR连接已建立');
    } catch (error) {
      console.error('SignalR连接失败:', error);
      this.isConnected = false;
      this.onConnectionStateChanged?.(false);
      throw error;
    }
  }

  // 设置事件处理器
  private setupEventHandlers(): void {
    if (!this.connection) return;

    // 连接确认
    this.connection.on('ConnectionConfirm', (response: ConnectionConfirmResponse) => {
      console.log('收到连接确认:', response);
      this.onConnectionConfirm?.(response);
    });

    // 接收音频数据
    this.connection.on('ReceiveAudioData', (response: AudioDataResponse) => {
      console.log('收到音频数据:', response.sequenceNumber);
      this.onAudioDataReceived?.(response);
    });

    // 接收转录文本
    this.connection.on('ReceiveTranscription', (response: TranscriptionResponse) => {
      console.log('收到转录文本:', response.text);
      this.onTranscriptionReceived?.(response);
    });

    // 状态更新
    this.connection.on('StatusUpdate', (response: StatusUpdateResponse) => {
      console.log('状态更新:', response.status);
      this.onStatusUpdate?.(response);
    });

    // 错误通知
    this.connection.on('ErrorNotification', (response: ErrorResponse) => {
      console.error('收到错误通知:', response);
      this.onError?.(response);
    });

    // 连接状态变化
    this.connection.onclose((error) => {
      console.log('SignalR连接已关闭:', error);
      this.isConnected = false;
      this.onConnectionStateChanged?.(false);
    });

    this.connection.onreconnecting((error) => {
      console.log('SignalR正在重连:', error);
      this.isConnected = false;
      this.onConnectionStateChanged?.(false);
    });

    this.connection.onreconnected((connectionId) => {
      console.log('SignalR重连成功:', connectionId);
      this.isConnected = true;
      this.onConnectionStateChanged?.(true);
    });
  }

  // 开始对话
  async startConversation(request: StartConversationRequest): Promise<void> {
    if (!this.connection || !this.isConnected) {
      throw new Error('SignalR连接未建立');
    }

    try {
      await this.connection.invoke('StartConversation', request);
      console.log('开始对话请求已发送:', request.sessionId);
    } catch (error) {
      console.error('发送开始对话请求失败:', error);
      throw error;
    }
  }

  // 发送音频数据
  async sendAudioData(request: AudioDataRequest): Promise<void> {
    if (!this.connection || !this.isConnected) {
      throw new Error('SignalR连接未建立');
    }

    try {
      // 将ArrayBuffer转换为Base64字符串以便传输
      const audioDataBase64 = this.arrayBufferToBase64(request.audioData);
      const requestWithBase64 = {
        ...request,
        audioData: audioDataBase64
      };
      
      await this.connection.invoke('SendAudioData', requestWithBase64);
    } catch (error) {
      console.error('发送音频数据失败:', error);
      throw error;
    }
  }

  // 停止对话
  async stopConversation(sessionId: string): Promise<void> {
    if (!this.connection || !this.isConnected) {
      throw new Error('SignalR连接未建立');
    }

    try {
      await this.connection.invoke('StopConversation', sessionId);
      console.log('停止对话请求已发送:', sessionId);
    } catch (error) {
      console.error('发送停止对话请求失败:', error);
      throw error;
    }
  }

  // 获取会话状态
  async getSessionStatus(sessionId: string): Promise<void> {
    if (!this.connection || !this.isConnected) {
      throw new Error('SignalR连接未建立');
    }

    try {
      await this.connection.invoke('GetSessionStatus', sessionId);
      console.log('获取会话状态请求已发送:', sessionId);
    } catch (error) {
      console.error('获取会话状态失败:', error);
      throw error;
    }
  }

  // 断开连接
  async disconnect(): Promise<void> {
    if (this.connection) {
      try {
        await this.connection.stop();
        console.log('SignalR连接已断开');
      } catch (error) {
        console.error('断开SignalR连接失败:', error);
      } finally {
        this.connection = null;
        this.isConnected = false;
        this.onConnectionStateChanged?.(false);
      }
    }
  }

  // 获取连接状态
  getConnectionStatus(): boolean {
    return this.isConnected;
  }

  // ArrayBuffer转Base64
  private arrayBufferToBase64(buffer: ArrayBuffer): string {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) {
      binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
  }

  // Base64转ArrayBuffer
  public base64ToArrayBuffer(base64: string): ArrayBuffer {
    const binaryString = atob(base64);
    const bytes = new Uint8Array(binaryString.length);
    for (let i = 0; i < binaryString.length; i++) {
      bytes[i] = binaryString.charCodeAt(i);
    }
    return bytes.buffer;
  }
}