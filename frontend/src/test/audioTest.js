// 简单的音频播放测试
// 这个文件用于测试Web Audio API的音频播放功能

import { WebAudioManager } from '../lib/audioManager.js';

// 生成测试音频数据（440Hz正弦波，1秒）
function generateTestAudio(sampleRate = 24000, duration = 1, frequency = 440) {
  const samples = sampleRate * duration;
  const audioData = new Float32Array(samples);
  
  for (let i = 0; i < samples; i++) {
    audioData[i] = Math.sin(2 * Math.PI * frequency * i / sampleRate) * 0.3;
  }
  
  return audioData;
}

// 测试音频播放功能
export async function testAudioPlayback() {
  try {
    console.log('开始测试音频播放功能...');
    
    // 检查浏览器支持
    if (!WebAudioManager.isSupported()) {
      throw new Error('浏览器不支持Web Audio API');
    }
    
    // 创建音频管理器
    const audioManager = new WebAudioManager();
    const config = WebAudioManager.getDefaultConfig();
    
    // 生成测试音频数据
    const testAudio = generateTestAudio(config.sampleRate, 1, 440);
    console.log('生成测试音频数据:', testAudio.length, '个采样点');
    
    // 播放测试音频
    await audioManager.playAudio(testAudio, config);
    console.log('音频播放测试完成');
    
    // 清理资源
    audioManager.dispose();
    
    return { success: true, message: '音频播放功能正常' };
  } catch (error) {
    console.error('音频播放测试失败:', error);
    return { success: false, message: error.message };
  }
}

// 测试音频录制功能
export async function testAudioRecording() {
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
    
    // 开始录制（录制3秒）
    await audioManager.startRecording(config, (audioData) => {
      recordedSamples += audioData.length;
      console.log('收到音频数据:', audioData.length, '个采样点');
    });
    
    console.log('录音已开始，将录制3秒...');
    
    // 等待3秒
    await new Promise(resolve => setTimeout(resolve, 3000));
    
    // 停止录制
    audioManager.stopRecording();
    console.log('录音已停止，总共录制了', recordedSamples, '个采样点');
    
    // 清理资源
    audioManager.dispose();
    
    return { 
      success: true, 
      message: `音频录制功能正常，录制了${recordedSamples}个采样点` 
    };
  } catch (error) {
    console.error('音频录制测试失败:', error);
    return { success: false, message: error.message };
  }
}

// 在浏览器控制台中运行测试
if (typeof window !== 'undefined') {
  window.testAudioPlayback = testAudioPlayback;
  window.testAudioRecording = testAudioRecording;
  console.log('音频测试函数已加载到window对象:');
  console.log('- window.testAudioPlayback() - 测试音频播放');
  console.log('- window.testAudioRecording() - 测试音频录制');
}