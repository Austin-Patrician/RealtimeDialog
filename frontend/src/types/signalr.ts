import { AudioConfig, SessionStatus } from './audio';

// SignalR请求消息类型
export interface StartConversationRequest {
  sessionId: string;
  audioConfig: AudioConfig;
  userId?: string;
}

export interface AudioDataRequest {
  sessionId: string;
  audioData: ArrayBuffer;
  timestamp: number;
  sequenceNumber: number;
}

// SignalR响应消息类型
export interface ConnectionConfirmResponse {
  sessionId: string;
  status: 'connected' | 'error';
  errorMessage?: string;
  audioConfig: AudioConfig;
}

export interface AudioDataResponse {
  sessionId: string;
  audioData: ArrayBuffer;
  timestamp: number;
  sequenceNumber: number;
}

export interface TranscriptionResponse {
  sessionId: string;
  text: string;
  isUser: boolean;
  timestamp: number;
}

export interface StatusUpdateResponse {
  sessionId: string;
  status: SessionStatus;
  metadata?: Record<string, any>;
}

export interface ErrorResponse {
  sessionId: string;
  errorCode: string;
  errorMessage: string;
  timestamp: number;
}

// SignalR客户端接口
export interface IConversationClient {
  ConnectionConfirm(response: ConnectionConfirmResponse): void;
  ReceiveAudioData(response: AudioDataResponse): void;
  ReceiveTranscription(response: TranscriptionResponse): void;
  StatusUpdate(response: StatusUpdateResponse): void;
  ErrorNotification(response: ErrorResponse): void;
}