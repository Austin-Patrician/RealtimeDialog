import React, { useEffect, useRef } from 'react';
import { useConversationStore } from '../lib/store';
import { SessionStatus } from '../types/audio';
import { Mic, MicOff, Phone, PhoneOff, Settings } from 'lucide-react';
import { cn } from '../lib/utils';
import { AudioWaveform } from './AudioWaveform';

export const ConversationInterface: React.FC = () => {
  const messagesEndRef = useRef<HTMLDivElement>(null);
  
  const {
    isConnected,
    isConnecting,
    connectionError,
    sessionId,
    sessionStatus,
    isRecording,
    audioError,
    messages,
    initialize,
    connect,
    disconnect,
    startConversation,
    stopConversation,
    startRecording,
    stopRecording,
    clearMessages
  } = useConversationStore();

  // 组件挂载时不需要重新初始化，因为App.tsx已经处理了初始化

  // 自动滚动到最新消息
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  // 获取状态显示文本
  const getStatusText = () => {
    if (!isConnected) return '未连接';
    if (isConnecting) return '连接中...';
    
    switch (sessionStatus) {
      case SessionStatus.Idle:
        return '空闲';
      case SessionStatus.Initializing:
        return '初始化中...';
      case SessionStatus.Active:
        return '会话活跃';
      case SessionStatus.Listening:
        return '正在听取...';
      case SessionStatus.Processing:
        return '处理中...';
      case SessionStatus.Speaking:
        return '正在说话...';
      case SessionStatus.Error:
        return '错误';
      default:
        return '未知状态';
    }
  };

  // 获取状态颜色
  const getStatusColor = () => {
    if (!isConnected || sessionStatus === SessionStatus.Error) return 'text-red-500';
    if (isConnecting || sessionStatus === SessionStatus.Initializing) return 'text-yellow-500';
    if (sessionStatus === SessionStatus.Listening) return 'text-green-500';
    if (sessionStatus === SessionStatus.Processing || sessionStatus === SessionStatus.Speaking) return 'text-blue-500';
    return 'text-gray-500';
  };

  // 处理连接/断开
  const handleConnectionToggle = async () => {
    if (isConnected) {
      await disconnect();
    } else {
      await connect();
    }
  };

  // 处理对话开始/停止
  const handleConversationToggle = async () => {
    if (sessionId) {
      await stopConversation();
    } else {
      await startConversation();
    }
  };

  // 处理录音开始/停止
  const handleRecordingToggle = async () => {
    if (isRecording) {
      stopRecording();
    } else {
      await startRecording();
    }
  };

  return (
    <div className="flex flex-col h-screen bg-gray-50">
      {/* 头部状态栏 */}
      <div className="bg-white shadow-sm border-b px-4 py-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center space-x-4">
            <h1 className="text-xl font-semibold text-gray-900">实时对话</h1>
            <div className="flex items-center space-x-2">
              <div className={cn('w-2 h-2 rounded-full', {
                'bg-green-500': isConnected && sessionStatus !== SessionStatus.Error,
                'bg-yellow-500': isConnecting,
                'bg-red-500': !isConnected || sessionStatus === SessionStatus.Error
              })} />
              <span className={cn('text-sm font-medium', getStatusColor())}>
                {getStatusText()}
              </span>
            </div>
          </div>
          
          <div className="flex items-center space-x-2">
            <button
              onClick={handleConnectionToggle}
              disabled={isConnecting}
              className={cn(
                'px-3 py-1.5 rounded-md text-sm font-medium transition-colors',
                isConnected
                  ? 'bg-red-100 text-red-700 hover:bg-red-200'
                  : 'bg-blue-100 text-blue-700 hover:bg-blue-200',
                isConnecting && 'opacity-50 cursor-not-allowed'
              )}
            >
              {isConnected ? '断开连接' : '连接服务器'}
            </button>
            
            <button className="p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-md">
              <Settings className="w-4 h-4" />
            </button>
          </div>
        </div>
        
        {/* 错误提示 */}
        {(connectionError || audioError) && (
          <div className="mt-2 p-2 bg-red-50 border border-red-200 rounded-md">
            <p className="text-sm text-red-600">
              {connectionError || audioError}
            </p>
          </div>
        )}
      </div>

      {/* 消息列表 */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {messages.length === 0 ? (
          <div className="flex items-center justify-center h-full">
            <div className="text-center text-gray-500">
              <p className="text-lg font-medium">开始你的对话</p>
              <p className="text-sm mt-1">点击下方按钮开始录音</p>
            </div>
          </div>
        ) : (
          messages.map((message) => (
            <div
              key={message.id}
              className={cn(
                'flex',
                message.isUser ? 'justify-end' : 'justify-start'
              )}
            >
              <div
                className={cn(
                  'max-w-xs lg:max-w-md px-4 py-2 rounded-lg text-sm',
                  message.isUser
                    ? 'bg-blue-500 text-white'
                    : 'bg-white text-gray-900 shadow-sm border'
                )}
              >
                <p>{message.text}</p>
                <p className={cn(
                  'text-xs mt-1',
                  message.isUser ? 'text-blue-100' : 'text-gray-500'
                )}>
                  {new Date(message.timestamp).toLocaleTimeString()}
                </p>
              </div>
            </div>
          ))
        )}
        <div ref={messagesEndRef} />
      </div>

      {/* 控制面板 */}
      <div className="bg-white border-t px-4 py-6">
        {/* 音频波形显示 */}
        <div className="flex justify-center mb-4">
          <AudioWaveform
            isRecording={isRecording}
            isPlaying={sessionStatus === SessionStatus.Speaking}
            width={300}
            height={60}
            className="bg-gray-50 rounded-lg p-2"
          />
        </div>
        
        <div className="flex items-center justify-center space-x-4">
          {/* 对话控制按钮 */}
          <button
            onClick={handleConversationToggle}
            disabled={!isConnected}
            className={cn(
              'flex items-center space-x-2 px-4 py-2 rounded-lg font-medium transition-colors',
              sessionId
                ? 'bg-red-500 text-white hover:bg-red-600'
                : 'bg-green-500 text-white hover:bg-green-600',
              !isConnected && 'opacity-50 cursor-not-allowed'
            )}
          >
            {sessionId ? (
              <>
                <PhoneOff className="w-4 h-4" />
                <span>结束对话</span>
              </>
            ) : (
              <>
                <Phone className="w-4 h-4" />
                <span>开始对话</span>
              </>
            )}
          </button>

          {/* 录音控制按钮 */}
          <button
            onClick={handleRecordingToggle}
            disabled={!sessionId}
            className={cn(
              'flex items-center justify-center w-16 h-16 rounded-full transition-all duration-200',
              isRecording
                ? 'bg-red-500 text-white hover:bg-red-600 scale-110'
                : 'bg-blue-500 text-white hover:bg-blue-600',
              !sessionId && 'opacity-50 cursor-not-allowed'
            )}
          >
            {isRecording ? (
              <MicOff className="w-6 h-6" />
            ) : (
              <Mic className="w-6 h-6" />
            )}
          </button>

          {/* 清空消息按钮 */}
          <button
            onClick={clearMessages}
            disabled={messages.length === 0}
            className={cn(
              'px-4 py-2 text-sm font-medium text-gray-600 hover:text-gray-800 hover:bg-gray-100 rounded-lg transition-colors',
              messages.length === 0 && 'opacity-50 cursor-not-allowed'
            )}
          >
            清空消息
          </button>
        </div>
        
        {/* 录音状态提示 */}
        {isRecording && (
          <div className="flex items-center justify-center mt-4">
            <div className="flex items-center space-x-2 text-red-600">
              <div className="w-2 h-2 bg-red-500 rounded-full animate-pulse" />
              <span className="text-sm font-medium">正在录音...</span>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};