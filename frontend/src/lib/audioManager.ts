import { AudioConfig } from '../types/audio';

export class WebAudioManager {
  private audioContext: AudioContext | null = null;
  private mediaRecorder: MediaRecorder | null = null;
  private recordingStream: MediaStream | null = null;
  private processor: ScriptProcessorNode | null = null;
  private isRecording = false;
  private onAudioDataCallback: ((data: Float32Array) => void) | null = null;

  constructor() {
    // 初始化时不创建AudioContext，等到用户交互时再创建
  }

  // 初始化音频上下文
  private async initAudioContext(config: AudioConfig): Promise<void> {
    if (!this.audioContext) {
      this.audioContext = new AudioContext({ 
        sampleRate: config.sampleRate 
      });
    }
    
    // 确保AudioContext处于运行状态
    if (this.audioContext.state === 'suspended') {
      await this.audioContext.resume();
    }
  }

  // 开始录音
  async startRecording(
    config: AudioConfig, 
    onAudioData: (data: Float32Array) => void
  ): Promise<void> {
    try {
      await this.initAudioContext(config);
      
      this.onAudioDataCallback = onAudioData;
      
      // 获取麦克风权限
      this.recordingStream = await navigator.mediaDevices.getUserMedia({
        audio: {
          sampleRate: config.sampleRate,
          channelCount: config.channels,
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true
        }
      });

      if (!this.audioContext) {
        throw new Error('AudioContext not initialized');
      }

      // 创建音频源和处理器
      const source = this.audioContext.createMediaStreamSource(this.recordingStream);
      this.processor = this.audioContext.createScriptProcessor(
        config.bufferSize, 
        config.channels, 
        config.channels
      );

      // 音频数据处理
      this.processor.onaudioprocess = (event) => {
        if (this.isRecording && this.onAudioDataCallback) {
          const inputData = event.inputBuffer.getChannelData(0);
          this.onAudioDataCallback(new Float32Array(inputData));
        }
      };

      // 连接音频节点
      source.connect(this.processor);
      this.processor.connect(this.audioContext.destination);
      
      this.isRecording = true;
      console.log('录音已开始');
    } catch (error) {
      console.error('开始录音失败:', error);
      throw error;
    }
  }

  // 停止录音
  stopRecording(): void {
    this.isRecording = false;
    
    if (this.processor) {
      this.processor.disconnect();
      this.processor = null;
    }
    
    if (this.recordingStream) {
      this.recordingStream.getTracks().forEach(track => track.stop());
      this.recordingStream = null;
    }
    
    this.onAudioDataCallback = null;
    console.log('录音已停止');
  }

  // 播放音频
  async playAudio(audioData: Float32Array, config: AudioConfig): Promise<void> {
    try {
      await this.initAudioContext(config);
      
      if (!this.audioContext) {
        throw new Error('AudioContext not initialized');
      }

      // 创建音频缓冲区
      const buffer = this.audioContext.createBuffer(
        config.channels, 
        audioData.length, 
        config.sampleRate
      );
      buffer.copyToChannel(audioData, 0);

      // 创建音频源并播放
      const source = this.audioContext.createBufferSource();
      source.buffer = buffer;
      source.connect(this.audioContext.destination);
      source.start();
    } catch (error) {
      console.error('播放音频失败:', error);
      throw error;
    }
  }

  // 获取录音状态
  getRecordingStatus(): boolean {
    return this.isRecording;
  }

  // 清理资源
  dispose(): void {
    this.stopRecording();
    
    if (this.audioContext) {
      this.audioContext.close();
      this.audioContext = null;
    }
  }

  // 检查浏览器支持
  static isSupported(): boolean {
    return !!(navigator.mediaDevices && 
             navigator.mediaDevices.getUserMedia && 
             window.AudioContext);
  }

  // 获取默认音频配置
  static getDefaultConfig(): AudioConfig {
    return {
      sampleRate: 24000,
      channels: 1,
      format: 'float32',
      bufferSize: 1024
    };
  }
}