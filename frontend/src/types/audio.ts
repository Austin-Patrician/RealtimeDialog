// 音频配置接口
export interface AudioConfig {
  sampleRate: number;
  channels: number;
  format: 'float32' | 'int16';
  bufferSize: number;
}

// 会话状态枚举
export enum SessionStatus {
  Initializing = 'initializing',
  Active = 'active',
  Listening = 'listening',
  Processing = 'processing',
  Speaking = 'speaking',
  Idle = 'idle',
  Ended = 'ended',
  Error = 'error'
}

// 对话会话接口
export interface ConversationSession {
  sessionId: string;
  userId?: string;
  startTime: Date;
  lastActivity: Date;
  status: SessionStatus;
  audioConfig: AudioConfig;
}

// 音频数据接口
export interface AudioData {
  sessionId: string;
  audioData: ArrayBuffer;
  timestamp: number;
  sequenceNumber: number;
}

// 转录文本接口
export interface TranscriptionData {
  sessionId: string;
  text: string;
  isUser: boolean;
  timestamp: number;
}

// 对话消息接口
export interface ConversationMessage {
  id: string;
  text: string;
  isUser: boolean;
  timestamp: number;
  status?: 'sending' | 'sent' | 'error';
}