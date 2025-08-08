import React, { useState } from 'react';
import { WebAudioManager } from '../lib/audioManager';
import { AudioConfig } from '../types/audio';

interface TestResult {
  success: boolean;
  message: string;
}

export const AudioTest: React.FC = () => {
  const [playbackResult, setPlaybackResult] = useState<TestResult | null>(null);
  const [recordingResult, setRecordingResult] = useState<TestResult | null>(null);
  const [isTestingPlayback, setIsTestingPlayback] = useState(false);
  const [isTestingRecording, setIsTestingRecording] = useState(false);
  const [recordingManager, setRecordingManager] = useState<WebAudioManager | null>(null);

  // 生成测试音频数据（440Hz正弦波）
  const generateTestAudio = (sampleRate: number, duration: number, frequency: number): Float32Array => {
    const samples = sampleRate * duration;
    const audioData = new Float32Array(samples);
    
    for (let i = 0; i < samples; i++) {
      audioData[i] = Math.sin(2 * Math.PI * frequency * i / sampleRate) * 0.3;
    }
    
    return audioData;
  };

  // 测试音频播放功能
  const testAudioPlayback = async () => {
    setIsTestingPlayback(true);
    setPlaybackResult(null);
    
    try {
      console.log('开始测试音频播放功能...');
      
      // 检查浏览器支持
      if (!WebAudioManager.isSupported()) {
        throw new Error('浏览器不支持Web Audio API');
      }
      
      // 创建音频管理器
      const audioManager = new WebAudioManager();
      const config = WebAudioManager.getDefaultConfig();
      
      // 生成测试音频数据（440Hz正弦波，1秒）
      const testAudio = generateTestAudio(config.sampleRate, 1, 440);
      console.log('生成测试音频数据:', testAudio.length, '个采样点');
      
      // 播放测试音频
      await audioManager.playAudio(testAudio, config);
      console.log('音频播放测试完成');
      
      // 清理资源
      audioManager.dispose();
      
      setPlaybackResult({ success: true, message: '音频播放功能正常 - 应该听到1秒的440Hz正弦波' });
    } catch (error) {
      console.error('音频播放测试失败:', error);
      setPlaybackResult({ 
        success: false, 
        message: error instanceof Error ? error.message : '音频播放测试失败' 
      });
    } finally {
      setIsTestingPlayback(false);
    }
  };

  // 开始录音测试
  const startRecordingTest = async () => {
    setIsTestingRecording(true);
    setRecordingResult(null);
    
    try {
      console.log('开始测试音频录制功能...');
      
      // 检查浏览器支持
      if (!WebAudioManager.isSupported()) {
        throw new Error('浏览器不支持Web Audio API');
      }
      
      // 创建音频管理器
      const audioManager = new WebAudioManager();
      const config = WebAudioManager.getDefaultConfig();
      
      let recordedSamples = 0;
      
      // 开始录制
      await audioManager.startRecording(config, (audioData) => {
        recordedSamples += audioData.length;
        console.log('收到音频数据:', audioData.length, '个采样点，总计:', recordedSamples);
      });
      
      setRecordingManager(audioManager);
      setRecordingResult({ 
        success: true, 
        message: `录音已开始，正在录制... (已录制 ${recordedSamples} 个采样点)` 
      });
      
      console.log('录音已开始，点击停止录音按钮来结束测试');
    } catch (error) {
      console.error('音频录制测试失败:', error);
      setRecordingResult({ 
        success: false, 
        message: error instanceof Error ? error.message : '音频录制测试失败' 
      });
      setIsTestingRecording(false);
    }
  };

  // 停止录音测试
  const stopRecordingTest = () => {
    if (recordingManager) {
      recordingManager.stopRecording();
      recordingManager.dispose();
      setRecordingManager(null);
    }
    
    setIsTestingRecording(false);
    setRecordingResult({ 
      success: true, 
      message: '录音测试完成 - 检查控制台查看录制的音频数据统计' 
    });
    
    console.log('录音测试已完成');
  };

  return (
    <div className="p-6 max-w-2xl mx-auto">
      <h2 className="text-2xl font-bold mb-6">Web Audio API 功能测试</h2>
      
      {/* 音频播放测试 */}
      <div className="mb-8 p-4 border rounded-lg">
        <h3 className="text-lg font-semibold mb-4">音频播放测试</h3>
        <p className="text-sm text-gray-600 mb-4">
          测试Web Audio API的音频播放功能，将播放1秒的440Hz正弦波。
        </p>
        
        <button
          onClick={testAudioPlayback}
          disabled={isTestingPlayback}
          className="px-4 py-2 bg-blue-500 text-white rounded hover:bg-blue-600 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {isTestingPlayback ? '测试中...' : '测试音频播放'}
        </button>
        
        {playbackResult && (
          <div className={`mt-4 p-3 rounded ${playbackResult.success ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'}`}>
            <strong>{playbackResult.success ? '✓ 成功' : '✗ 失败'}:</strong> {playbackResult.message}
          </div>
        )}
      </div>
      
      {/* 音频录制测试 */}
      <div className="mb-8 p-4 border rounded-lg">
        <h3 className="text-lg font-semibold mb-4">音频录制测试</h3>
        <p className="text-sm text-gray-600 mb-4">
          测试Web Audio API的音频录制功能，需要麦克风权限。录制的音频数据将在控制台显示。
        </p>
        
        <div className="space-x-2">
          <button
            onClick={startRecordingTest}
            disabled={isTestingRecording}
            className="px-4 py-2 bg-green-500 text-white rounded hover:bg-green-600 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isTestingRecording ? '录音中...' : '开始录音测试'}
          </button>
          
          {isTestingRecording && (
            <button
              onClick={stopRecordingTest}
              className="px-4 py-2 bg-red-500 text-white rounded hover:bg-red-600"
            >
              停止录音
            </button>
          )}
        </div>
        
        {recordingResult && (
          <div className={`mt-4 p-3 rounded ${recordingResult.success ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'}`}>
            <strong>{recordingResult.success ? '✓ 成功' : '✗ 失败'}:</strong> {recordingResult.message}
          </div>
        )}
      </div>
      
      {/* 浏览器支持检查 */}
      <div className="p-4 border rounded-lg bg-gray-50">
        <h3 className="text-lg font-semibold mb-4">浏览器支持检查</h3>
        <div className="space-y-2 text-sm">
          <div className={`flex items-center ${WebAudioManager.isSupported() ? 'text-green-600' : 'text-red-600'}`}>
            <span className="mr-2">{WebAudioManager.isSupported() ? '✓' : '✗'}</span>
            Web Audio API 支持
          </div>
          <div className={`flex items-center ${navigator.mediaDevices ? 'text-green-600' : 'text-red-600'}`}>
            <span className="mr-2">{navigator.mediaDevices ? '✓' : '✗'}</span>
            MediaDevices API 支持
          </div>
          <div className={`flex items-center ${navigator.mediaDevices?.getUserMedia ? 'text-green-600' : 'text-red-600'}`}>
            <span className="mr-2">{navigator.mediaDevices?.getUserMedia ? '✓' : '✗'}</span>
            getUserMedia 支持
          </div>
        </div>
      </div>
    </div>
  );
};