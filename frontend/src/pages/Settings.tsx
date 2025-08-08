import React, { useState } from 'react';
import { useConversationStore } from '../lib/store';
import { AudioConfig } from '../types/audio';
import { WebAudioManager } from '../lib/audioManager';
import { ArrowLeft, Save, RotateCcw } from 'lucide-react';
import { cn } from '../lib/utils';

interface SettingsProps {
  onBack: () => void;
}

export const Settings: React.FC<SettingsProps> = ({ onBack }) => {
  const { audioConfig, setAudioConfig } = useConversationStore();
  
  const [localConfig, setLocalConfig] = useState<AudioConfig>(audioConfig);
  const [serverUrl, setServerUrl] = useState('http://localhost:5000');
  const [apiKey, setApiKey] = useState('');
  const [hasChanges, setHasChanges] = useState(false);

  // 处理音频配置变更
  const handleConfigChange = (key: keyof AudioConfig, value: any) => {
    setLocalConfig(prev => ({ ...prev, [key]: value }));
    setHasChanges(true);
  };

  // 保存设置
  const handleSave = () => {
    setAudioConfig(localConfig);
    setHasChanges(false);
    
    // 保存到本地存储
    localStorage.setItem('audioConfig', JSON.stringify(localConfig));
    localStorage.setItem('serverUrl', serverUrl);
    localStorage.setItem('apiKey', apiKey);
  };

  // 重置为默认设置
  const handleReset = () => {
    const defaultConfig = WebAudioManager.getDefaultConfig();
    setLocalConfig(defaultConfig);
    setServerUrl('http://localhost:5000');
    setApiKey('');
    setHasChanges(true);
  };

  // 加载保存的设置
  React.useEffect(() => {
    const savedConfig = localStorage.getItem('audioConfig');
    const savedServerUrl = localStorage.getItem('serverUrl');
    const savedApiKey = localStorage.getItem('apiKey');
    
    if (savedConfig) {
      try {
        setLocalConfig(JSON.parse(savedConfig));
      } catch (error) {
        console.error('Failed to parse saved audio config:', error);
      }
    }
    
    if (savedServerUrl) {
      setServerUrl(savedServerUrl);
    }
    
    if (savedApiKey) {
      setApiKey(savedApiKey);
    }
  }, []);

  return (
    <div className="min-h-screen bg-gray-50">
      {/* 头部 */}
      <div className="bg-white shadow-sm border-b">
        <div className="max-w-4xl mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-4">
              <button
                onClick={onBack}
                className="p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-md transition-colors"
              >
                <ArrowLeft className="w-5 h-5" />
              </button>
              <h1 className="text-xl font-semibold text-gray-900">设置</h1>
            </div>
            
            <div className="flex items-center space-x-2">
              <button
                onClick={handleReset}
                className="flex items-center space-x-2 px-3 py-1.5 text-sm font-medium text-gray-600 hover:text-gray-800 hover:bg-gray-100 rounded-md transition-colors"
              >
                <RotateCcw className="w-4 h-4" />
                <span>重置</span>
              </button>
              
              <button
                onClick={handleSave}
                disabled={!hasChanges}
                className={cn(
                  'flex items-center space-x-2 px-4 py-1.5 text-sm font-medium rounded-md transition-colors',
                  hasChanges
                    ? 'bg-blue-500 text-white hover:bg-blue-600'
                    : 'bg-gray-100 text-gray-400 cursor-not-allowed'
                )}
              >
                <Save className="w-4 h-4" />
                <span>保存</span>
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* 设置内容 */}
      <div className="max-w-4xl mx-auto px-4 py-8">
        <div className="space-y-8">
          {/* 服务器配置 */}
          <div className="card">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">服务器配置</h2>
            
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  服务器地址
                </label>
                <input
                  type="url"
                  value={serverUrl}
                  onChange={(e) => {
                    setServerUrl(e.target.value);
                    setHasChanges(true);
                  }}
                  placeholder="http://localhost:5000"
                  className="input-field"
                />
                <p className="text-xs text-gray-500 mt-1">
                  后端SignalR服务器的地址
                </p>
              </div>
              
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  API密钥 (可选)
                </label>
                <input
                  type="password"
                  value={apiKey}
                  onChange={(e) => {
                    setApiKey(e.target.value);
                    setHasChanges(true);
                  }}
                  placeholder="输入豆包API密钥"
                  className="input-field"
                />
                <p className="text-xs text-gray-500 mt-1">
                  用于访问豆包API的密钥
                </p>
              </div>
            </div>
          </div>

          {/* 音频配置 */}
          <div className="card">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">音频配置</h2>
            
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  采样率 (Hz)
                </label>
                <select
                  value={localConfig.sampleRate}
                  onChange={(e) => handleConfigChange('sampleRate', parseInt(e.target.value))}
                  className="input-field"
                >
                  <option value={16000}>16,000 Hz</option>
                  <option value={24000}>24,000 Hz (推荐)</option>
                  <option value={44100}>44,100 Hz</option>
                  <option value={48000}>48,000 Hz</option>
                </select>
                <p className="text-xs text-gray-500 mt-1">
                  音频采样频率，影响音质和文件大小
                </p>
              </div>
              
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  声道数
                </label>
                <select
                  value={localConfig.channels}
                  onChange={(e) => handleConfigChange('channels', parseInt(e.target.value))}
                  className="input-field"
                >
                  <option value={1}>单声道 (推荐)</option>
                  <option value={2}>立体声</option>
                </select>
                <p className="text-xs text-gray-500 mt-1">
                  音频声道数量
                </p>
              </div>
              
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  音频格式
                </label>
                <select
                  value={localConfig.format}
                  onChange={(e) => handleConfigChange('format', e.target.value)}
                  className="input-field"
                >
                  <option value="float32">Float32 (推荐)</option>
                  <option value="int16">Int16</option>
                </select>
                <p className="text-xs text-gray-500 mt-1">
                  音频数据格式
                </p>
              </div>
              
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  缓冲区大小
                </label>
                <select
                  value={localConfig.bufferSize}
                  onChange={(e) => handleConfigChange('bufferSize', parseInt(e.target.value))}
                  className="input-field"
                >
                  <option value={256}>256 (低延迟)</option>
                  <option value={512}>512</option>
                  <option value={1024}>1024 (推荐)</option>
                  <option value={2048}>2048</option>
                  <option value={4096}>4096 (高稳定性)</option>
                </select>
                <p className="text-xs text-gray-500 mt-1">
                  音频处理缓冲区大小，影响延迟和稳定性
                </p>
              </div>
            </div>
          </div>

          {/* 系统信息 */}
          <div className="card">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">系统信息</h2>
            
            <div className="space-y-3 text-sm">
              <div className="flex justify-between">
                <span className="text-gray-600">浏览器:</span>
                <span className="text-gray-900">{navigator.userAgent.split(' ')[0]}</span>
              </div>
              
              <div className="flex justify-between">
                <span className="text-gray-600">Web Audio API:</span>
                <span className={cn(
                  'font-medium',
                  (window.AudioContext || (window as any).webkitAudioContext) ? 'text-green-600' : 'text-red-600'
                )}>
                  {(window.AudioContext || (window as any).webkitAudioContext) ? '支持' : '不支持'}
                </span>
              </div>
              
              <div className="flex justify-between">
                <span className="text-gray-600">MediaDevices API:</span>
                <span className={cn(
                  'font-medium',
                  (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) ? 'text-green-600' : 'text-red-600'
                )}>
                  {(navigator.mediaDevices && navigator.mediaDevices.getUserMedia) ? '支持' : '不支持'}
                </span>
              </div>
              
              <div className="flex justify-between">
                <span className="text-gray-600">WebSocket:</span>
                <span className={cn(
                  'font-medium',
                  window.WebSocket ? 'text-green-600' : 'text-red-600'
                )}>
                  {window.WebSocket ? '支持' : '不支持'}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};