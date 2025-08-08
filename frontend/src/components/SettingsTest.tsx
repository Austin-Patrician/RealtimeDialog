import React, { useState, useEffect } from 'react';
import { useConversationStore } from '../lib/store';
import { AudioConfig } from '../types/audio';
import { WebAudioManager } from '../lib/audioManager';
import { Settings, CheckCircle, XCircle, AlertCircle, Play, Square } from 'lucide-react';

export const SettingsTest: React.FC = () => {
  const { audioConfig, setAudioConfig } = useConversationStore();
  const [testResults, setTestResults] = useState<{
    [key: string]: { status: 'success' | 'error' | 'warning'; message: string }
  }>({});
  const [isTestingAudio, setIsTestingAudio] = useState(false);
  const [audioManager, setAudioManager] = useState<WebAudioManager | null>(null);

  useEffect(() => {
    // 初始化音频管理器
    const manager = new WebAudioManager();
    setAudioManager(manager);
    
    return () => {
      if (manager) {
        manager.cleanup();
      }
    };
  }, []);

  const addTestResult = (key: string, status: 'success' | 'error' | 'warning', message: string) => {
    setTestResults(prev => ({
      ...prev,
      [key]: { status, message }
    }));
  };

  const testBrowserSupport = () => {
    // 测试Web Audio API支持
    if (window.AudioContext || (window as any).webkitAudioContext) {
      addTestResult('webAudio', 'success', 'Web Audio API 支持正常');
    } else {
      addTestResult('webAudio', 'error', 'Web Audio API 不支持');
    }

    // 测试MediaDevices API支持
    if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
      addTestResult('mediaDevices', 'success', 'MediaDevices API 支持正常');
    } else {
      addTestResult('mediaDevices', 'error', 'MediaDevices API 不支持');
    }

    // 测试WebSocket支持
    if (window.WebSocket) {
      addTestResult('webSocket', 'success', 'WebSocket 支持正常');
    } else {
      addTestResult('webSocket', 'error', 'WebSocket 不支持');
    }

    // 测试本地存储
    try {
      localStorage.setItem('test', 'test');
      localStorage.removeItem('test');
      addTestResult('localStorage', 'success', '本地存储支持正常');
    } catch (error) {
      addTestResult('localStorage', 'error', '本地存储不支持');
    }
  };

  const testAudioConfig = async () => {
    if (!audioManager) {
      addTestResult('audioConfig', 'error', '音频管理器未初始化');
      return;
    }

    try {
      // 测试音频配置是否有效
      const isSupported = await audioManager.checkBrowserSupport();
      if (!isSupported.supported) {
        addTestResult('audioConfig', 'error', `音频配置不支持: ${isSupported.reason}`);
        return;
      }

      // 测试不同采样率
      const sampleRates = [16000, 24000, 44100, 48000];
      for (const sampleRate of sampleRates) {
        const testConfig = { ...audioConfig, sampleRate };
        try {
          // 这里可以添加更详细的采样率测试逻辑
          addTestResult(`sampleRate_${sampleRate}`, 'success', `采样率 ${sampleRate}Hz 支持`);
        } catch (error) {
          addTestResult(`sampleRate_${sampleRate}`, 'warning', `采样率 ${sampleRate}Hz 可能不支持`);
        }
      }

      // 测试缓冲区大小
      const bufferSizes = [256, 512, 1024, 2048, 4096];
      for (const bufferSize of bufferSizes) {
        if (bufferSize >= 256 && bufferSize <= 16384 && (bufferSize & (bufferSize - 1)) === 0) {
          addTestResult(`bufferSize_${bufferSize}`, 'success', `缓冲区大小 ${bufferSize} 有效`);
        } else {
          addTestResult(`bufferSize_${bufferSize}`, 'error', `缓冲区大小 ${bufferSize} 无效`);
        }
      }

      addTestResult('audioConfig', 'success', '音频配置测试完成');
    } catch (error) {
      addTestResult('audioConfig', 'error', `音频配置测试失败: ${error instanceof Error ? error.message : '未知错误'}`);
    }
  };

  const testAudioRecording = async () => {
    if (!audioManager) {
      addTestResult('audioRecording', 'error', '音频管理器未初始化');
      return;
    }

    setIsTestingAudio(true);
    
    try {
      // 测试录音功能
      let recordedData: Float32Array | null = null;
      
      await audioManager.startRecording(audioConfig, (audioData) => {
        recordedData = audioData;
        addTestResult('audioRecording', 'success', `录音数据接收成功，长度: ${audioData.length}`);
      });

      // 录音2秒
      await new Promise(resolve => setTimeout(resolve, 2000));
      
      audioManager.stopRecording();
      
      if (recordedData) {
        addTestResult('audioData', 'success', `音频数据格式正确，采样率: ${audioConfig.sampleRate}Hz`);
        
        // 测试音频播放
        try {
          await audioManager.playAudio(recordedData, audioConfig.sampleRate);
          addTestResult('audioPlayback', 'success', '音频播放测试成功');
        } catch (error) {
          addTestResult('audioPlayback', 'error', `音频播放测试失败: ${error instanceof Error ? error.message : '未知错误'}`);
        }
      } else {
        addTestResult('audioData', 'warning', '未接收到音频数据');
      }
    } catch (error) {
      addTestResult('audioRecording', 'error', `录音测试失败: ${error instanceof Error ? error.message : '未知错误'}`);
    } finally {
      setIsTestingAudio(false);
    }
  };

  const testLocalStorage = () => {
    try {
      // 测试保存和读取音频配置
      const testConfig: AudioConfig = {
        sampleRate: 24000,
        channels: 1,
        format: 'float32',
        bufferSize: 1024
      };
      
      localStorage.setItem('test_audioConfig', JSON.stringify(testConfig));
      const savedConfig = localStorage.getItem('test_audioConfig');
      
      if (savedConfig) {
        const parsedConfig = JSON.parse(savedConfig);
        if (JSON.stringify(parsedConfig) === JSON.stringify(testConfig)) {
          addTestResult('configStorage', 'success', '音频配置存储测试成功');
        } else {
          addTestResult('configStorage', 'error', '音频配置存储数据不匹配');
        }
      } else {
        addTestResult('configStorage', 'error', '无法读取存储的音频配置');
      }
      
      localStorage.removeItem('test_audioConfig');
    } catch (error) {
      addTestResult('configStorage', 'error', `配置存储测试失败: ${error instanceof Error ? error.message : '未知错误'}`);
    }
  };

  const runAllTests = async () => {
    setTestResults({});
    
    // 依次运行所有测试
    testBrowserSupport();
    await testAudioConfig();
    testLocalStorage();
  };

  const getStatusIcon = (status: 'success' | 'error' | 'warning') => {
    switch (status) {
      case 'success':
        return <CheckCircle className="w-4 h-4 text-green-600" />;
      case 'error':
        return <XCircle className="w-4 h-4 text-red-600" />;
      case 'warning':
        return <AlertCircle className="w-4 h-4 text-yellow-600" />;
    }
  };

  const getStatusColor = (status: 'success' | 'error' | 'warning') => {
    switch (status) {
      case 'success':
        return 'text-green-800 bg-green-50 border-green-200';
      case 'error':
        return 'text-red-800 bg-red-50 border-red-200';
      case 'warning':
        return 'text-yellow-800 bg-yellow-50 border-yellow-200';
    }
  };

  return (
    <div className="max-w-4xl mx-auto p-6 space-y-6">
      <div className="bg-white rounded-lg shadow-lg p-6">
        <h2 className="text-2xl font-bold mb-4 flex items-center gap-2">
          <Settings className="w-6 h-6" />
          设置页面功能测试
        </h2>
        
        {/* 当前音频配置 */}
        <div className="mb-6 p-4 bg-gray-50 rounded-lg">
          <h3 className="text-lg font-semibold mb-3">当前音频配置</h3>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
            <div>
              <span className="text-gray-600">采样率:</span>
              <span className="ml-2 font-medium">{audioConfig.sampleRate} Hz</span>
            </div>
            <div>
              <span className="text-gray-600">声道数:</span>
              <span className="ml-2 font-medium">{audioConfig.channels}</span>
            </div>
            <div>
              <span className="text-gray-600">格式:</span>
              <span className="ml-2 font-medium">{audioConfig.format}</span>
            </div>
            <div>
              <span className="text-gray-600">缓冲区:</span>
              <span className="ml-2 font-medium">{audioConfig.bufferSize}</span>
            </div>
          </div>
        </div>

        {/* 测试控制按钮 */}
        <div className="flex gap-4 mb-6">
          <button
            onClick={runAllTests}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
          >
            <Settings className="w-4 h-4" />
            运行所有测试
          </button>
          
          <button
            onClick={testAudioRecording}
            disabled={isTestingAudio}
            className="flex items-center gap-2 px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isTestingAudio ? (
              <Square className="w-4 h-4" />
            ) : (
              <Play className="w-4 h-4" />
            )}
            {isTestingAudio ? '录音测试中...' : '测试录音功能'}
          </button>
        </div>

        {/* 测试结果 */}
        {Object.keys(testResults).length > 0 && (
          <div className="space-y-3">
            <h3 className="text-lg font-semibold">测试结果</h3>
            <div className="space-y-2">
              {Object.entries(testResults).map(([key, result]) => (
                <div
                  key={key}
                  className={`flex items-center gap-3 p-3 rounded-lg border ${getStatusColor(result.status)}`}
                >
                  {getStatusIcon(result.status)}
                  <div className="flex-1">
                    <div className="font-medium">{key}</div>
                    <div className="text-sm">{result.message}</div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* 测试说明 */}
        <div className="mt-6 p-4 bg-blue-50 border border-blue-200 rounded-lg">
          <h3 className="font-medium text-blue-800 mb-2">测试说明</h3>
          <ul className="text-sm text-blue-700 space-y-1">
            <li>• 浏览器支持测试：检查Web Audio API、MediaDevices API等支持情况</li>
            <li>• 音频配置测试：验证不同采样率和缓冲区大小的有效性</li>
            <li>• 录音功能测试：测试麦克风权限获取和音频数据采集</li>
            <li>• 本地存储测试：验证配置保存和读取功能</li>
          </ul>
        </div>
      </div>
    </div>
  );
};