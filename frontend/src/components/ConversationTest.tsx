import React, { useState, useEffect } from 'react';
import { useConversationStore } from '../lib/store';
import { SessionStatus } from '../types/audio';
import { Play, Square, Mic, MicOff, Settings, Wifi, WifiOff } from 'lucide-react';

export const ConversationTest: React.FC = () => {
  const {
    isConnected,
    isConnecting,
    sessionId,
    sessionStatus,
    isRecording,
    messages,
    connectionError,
    audioError,
    initialize,
    connect,
    disconnect,
    startConversation,
    stopConversation,
    startRecording,
    stopRecording,
    clearMessages
  } = useConversationStore();

  const [testLog, setTestLog] = useState<string[]>([]);

  const addLog = (message: string) => {
    const timestamp = new Date().toLocaleTimeString();
    setTestLog(prev => [...prev, `[${timestamp}] ${message}`]);
  };

  useEffect(() => {
    // 初始化服务
    initialize();
    addLog('初始化音频管理器和SignalR客户端');
  }, [initialize]);

  useEffect(() => {
    if (isConnected) {
      addLog('✅ SignalR连接成功');
    } else if (connectionError) {
      addLog(`❌ 连接错误: ${connectionError}`);
    }
  }, [isConnected, connectionError]);

  useEffect(() => {
    if (sessionId) {
      addLog(`📞 会话已创建: ${sessionId}`);
    }
  }, [sessionId]);

  useEffect(() => {
    if (sessionStatus) {
      addLog(`📊 会话状态: ${sessionStatus}`);
    }
  }, [sessionStatus]);

  useEffect(() => {
    if (isRecording) {
      addLog('🎤 开始录音');
    } else {
      addLog('⏹️ 停止录音');
    }
  }, [isRecording]);

  useEffect(() => {
    if (audioError) {
      addLog(`❌ 音频错误: ${audioError}`);
    }
  }, [audioError]);

  const handleConnect = async () => {
    addLog('尝试连接到SignalR服务器...');
    await connect();
  };

  const handleDisconnect = async () => {
    addLog('断开SignalR连接...');
    await disconnect();
  };

  const handleStartConversation = async () => {
    addLog('开始对话会话...');
    await startConversation();
  };

  const handleStopConversation = async () => {
    addLog('停止对话会话...');
    await stopConversation();
  };

  const handleStartRecording = async () => {
    addLog('开始录音...');
    await startRecording();
  };

  const handleStopRecording = () => {
    addLog('停止录音...');
    stopRecording();
  };

  const handleClearLog = () => {
    setTestLog([]);
    clearMessages();
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case SessionStatus.Active:
      case SessionStatus.Listening:
        return 'text-green-600';
      case SessionStatus.Processing:
      case SessionStatus.Speaking:
        return 'text-blue-600';
      case SessionStatus.Error:
        return 'text-red-600';
      default:
        return 'text-gray-600';
    }
  };

  return (
    <div className="max-w-4xl mx-auto p-6 space-y-6">
      <div className="bg-white rounded-lg shadow-lg p-6">
        <h2 className="text-2xl font-bold mb-4 flex items-center gap-2">
          <Settings className="w-6 h-6" />
          实时对话系统测试
        </h2>
        
        {/* 连接状态 */}
        <div className="mb-6 p-4 bg-gray-50 rounded-lg">
          <div className="flex items-center gap-4 mb-2">
            <div className="flex items-center gap-2">
              {isConnected ? (
                <Wifi className="w-5 h-5 text-green-600" />
              ) : (
                <WifiOff className="w-5 h-5 text-red-600" />
              )}
              <span className={`font-medium ${
                isConnected ? 'text-green-600' : 'text-red-600'
              }`}>
                {isConnected ? '已连接' : '未连接'}
              </span>
            </div>
            
            {sessionId && (
              <div className="text-sm text-gray-600">
                会话ID: {sessionId.substring(0, 12)}...
              </div>
            )}
            
            {sessionStatus && (
              <div className={`text-sm font-medium ${getStatusColor(sessionStatus)}`}>
                状态: {sessionStatus}
              </div>
            )}
          </div>
          
          {isConnecting && (
            <div className="text-sm text-blue-600">正在连接...</div>
          )}
        </div>

        {/* 控制按钮 */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
          <button
            onClick={handleConnect}
            disabled={isConnected || isConnecting}
            className="flex items-center justify-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Wifi className="w-4 h-4" />
            连接服务器
          </button>
          
          <button
            onClick={handleDisconnect}
            disabled={!isConnected}
            className="flex items-center justify-center gap-2 px-4 py-2 bg-gray-600 text-white rounded-lg hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <WifiOff className="w-4 h-4" />
            断开连接
          </button>
          
          <button
            onClick={handleStartConversation}
            disabled={!isConnected || !!sessionId}
            className="flex items-center justify-center gap-2 px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Play className="w-4 h-4" />
            开始对话
          </button>
          
          <button
            onClick={handleStopConversation}
            disabled={!sessionId}
            className="flex items-center justify-center gap-2 px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Square className="w-4 h-4" />
            停止对话
          </button>
        </div>

        {/* 录音控制 */}
        <div className="flex gap-4 mb-6">
          <button
            onClick={handleStartRecording}
            disabled={!sessionId || isRecording}
            className="flex items-center justify-center gap-2 px-6 py-3 bg-red-600 text-white rounded-lg hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Mic className="w-5 h-5" />
            开始录音
          </button>
          
          <button
            onClick={handleStopRecording}
            disabled={!isRecording}
            className="flex items-center justify-center gap-2 px-6 py-3 bg-gray-600 text-white rounded-lg hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <MicOff className="w-5 h-5" />
            停止录音
          </button>
        </div>

        {/* 错误显示 */}
        {(connectionError || audioError) && (
          <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded-lg">
            <h3 className="font-medium text-red-800 mb-2">错误信息:</h3>
            {connectionError && (
              <p className="text-red-700 text-sm mb-1">连接错误: {connectionError}</p>
            )}
            {audioError && (
              <p className="text-red-700 text-sm">音频错误: {audioError}</p>
            )}
          </div>
        )}
      </div>

      {/* 消息列表 */}
      {messages.length > 0 && (
        <div className="bg-white rounded-lg shadow-lg p-6">
          <h3 className="text-lg font-semibold mb-4">对话消息</h3>
          <div className="space-y-2 max-h-60 overflow-y-auto">
            {messages.map((message) => (
              <div
                key={message.id}
                className={`p-3 rounded-lg ${
                  message.isUser
                    ? 'bg-blue-100 text-blue-800 ml-8'
                    : 'bg-gray-100 text-gray-800 mr-8'
                }`}
              >
                <div className="text-sm font-medium mb-1">
                  {message.isUser ? '用户' : 'AI助手'}
                </div>
                <div>{message.text}</div>
                <div className="text-xs text-gray-500 mt-1">
                  {new Date(message.timestamp).toLocaleTimeString()}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* 测试日志 */}
      <div className="bg-white rounded-lg shadow-lg p-6">
        <div className="flex justify-between items-center mb-4">
          <h3 className="text-lg font-semibold">测试日志</h3>
          <button
            onClick={handleClearLog}
            className="px-3 py-1 text-sm bg-gray-200 text-gray-700 rounded hover:bg-gray-300"
          >
            清空日志
          </button>
        </div>
        <div className="bg-gray-900 text-green-400 p-4 rounded-lg font-mono text-sm max-h-60 overflow-y-auto">
          {testLog.length === 0 ? (
            <div className="text-gray-500">暂无日志...</div>
          ) : (
            testLog.map((log, index) => (
              <div key={index} className="mb-1">
                {log}
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  );
};