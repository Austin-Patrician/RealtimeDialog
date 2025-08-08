import React, { useRef, useEffect, useState } from 'react';
import { cn } from '../lib/utils';

interface AudioWaveformProps {
  audioData?: Float32Array;
  isRecording?: boolean;
  isPlaying?: boolean;
  className?: string;
  height?: number;
  width?: number;
  color?: string;
  backgroundColor?: string;
}

export const AudioWaveform: React.FC<AudioWaveformProps> = ({
  audioData,
  isRecording = false,
  isPlaying = false,
  className,
  height = 60,
  width = 300,
  color = '#3b82f6',
  backgroundColor = '#f1f5f9'
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const animationRef = useRef<number>();
  const [animationPhase, setAnimationPhase] = useState(0);

  // 绘制波形
  const drawWaveform = (canvas: HTMLCanvasElement, data?: Float32Array) => {
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // 清空画布
    ctx.fillStyle = backgroundColor;
    ctx.fillRect(0, 0, width, height);

    if (!data || data.length === 0) {
      // 绘制静态线条
      ctx.strokeStyle = color;
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(0, height / 2);
      ctx.lineTo(width, height / 2);
      ctx.stroke();
      return;
    }

    // 绘制波形数据
    ctx.strokeStyle = color;
    ctx.lineWidth = 1.5;
    ctx.beginPath();

    const sliceWidth = width / data.length;
    let x = 0;

    for (let i = 0; i < data.length; i++) {
      const v = data[i] * 0.5; // 缩放幅度
      const y = (v * height / 2) + (height / 2);

      if (i === 0) {
        ctx.moveTo(x, y);
      } else {
        ctx.lineTo(x, y);
      }

      x += sliceWidth;
    }

    ctx.stroke();
  };

  // 绘制录音动画
  const drawRecordingAnimation = (canvas: HTMLCanvasElement, phase: number) => {
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // 清空画布
    ctx.fillStyle = backgroundColor;
    ctx.fillRect(0, 0, width, height);

    // 绘制动态波形
    ctx.strokeStyle = color;
    ctx.lineWidth = 2;
    ctx.beginPath();

    const centerY = height / 2;
    const amplitude = 20;
    const frequency = 0.02;

    for (let x = 0; x < width; x++) {
      const y = centerY + Math.sin(x * frequency + phase) * amplitude * Math.random();
      
      if (x === 0) {
        ctx.moveTo(x, y);
      } else {
        ctx.lineTo(x, y);
      }
    }

    ctx.stroke();
  };

  // 绘制播放动画
  const drawPlayingAnimation = (canvas: HTMLCanvasElement, phase: number) => {
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // 清空画布
    ctx.fillStyle = backgroundColor;
    ctx.fillRect(0, 0, width, height);

    // 绘制播放指示器
    const progress = (phase % 100) / 100;
    const indicatorX = progress * width;

    // 绘制背景波形
    if (audioData && audioData.length > 0) {
      ctx.strokeStyle = '#cbd5e1';
      ctx.lineWidth = 1;
      ctx.beginPath();

      const sliceWidth = width / audioData.length;
      let x = 0;

      for (let i = 0; i < audioData.length; i++) {
        const v = audioData[i] * 0.5;
        const y = (v * height / 2) + (height / 2);

        if (i === 0) {
          ctx.moveTo(x, y);
        } else {
          ctx.lineTo(x, y);
        }

        x += sliceWidth;
      }

      ctx.stroke();
    }

    // 绘制播放进度
    ctx.strokeStyle = color;
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(indicatorX, 0);
    ctx.lineTo(indicatorX, height);
    ctx.stroke();
  };

  // 动画循环
  const animate = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    if (isRecording) {
      drawRecordingAnimation(canvas, animationPhase);
      setAnimationPhase(prev => prev + 0.1);
    } else if (isPlaying) {
      drawPlayingAnimation(canvas, animationPhase);
      setAnimationPhase(prev => prev + 1);
    } else {
      drawWaveform(canvas, audioData);
    }

    if (isRecording || isPlaying) {
      animationRef.current = requestAnimationFrame(animate);
    }
  };

  // 初始化和更新
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    // 设置画布尺寸
    canvas.width = width;
    canvas.height = height;

    if (isRecording || isPlaying) {
      animate();
    } else {
      drawWaveform(canvas, audioData);
    }

    return () => {
      if (animationRef.current) {
        cancelAnimationFrame(animationRef.current);
      }
    };
  }, [audioData, isRecording, isPlaying, width, height, color, backgroundColor]);

  return (
    <div className={cn('flex items-center justify-center', className)}>
      <canvas
        ref={canvasRef}
        className="border border-gray-200 rounded-md"
        style={{ width: `${width}px`, height: `${height}px` }}
      />
    </div>
  );
};