import React, { useState } from 'react';
import { ConversationInterface } from '../components/ConversationInterface';
import { Settings } from './Settings';
import { AudioTest } from '../components/AudioTest';
import { ConversationTest } from '../components/ConversationTest';
import { SettingsTest } from '../components/SettingsTest';

export const Home: React.FC = () => {
  const [currentPage, setCurrentPage] = useState<'conversation' | 'settings' | 'test' | 'conversation-test' | 'settings-test'>('conversation');

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 to-indigo-100">
      <div className="container mx-auto px-4 py-8">
        <header className="text-center mb-8">
          <h1 className="text-4xl font-bold text-gray-800 mb-2">
            实时对话系统
          </h1>
          <p className="text-gray-600">
            基于Web Audio API和SignalR的实时语音对话
          </p>
        </header>

        <div className="flex justify-center mb-6">
          <div className="bg-white rounded-lg shadow-md p-1 flex">
            <button
              onClick={() => setCurrentPage('conversation')}
              className={`px-6 py-2 rounded-md transition-colors ${
                currentPage === 'conversation'
                  ? 'bg-blue-500 text-white'
                  : 'text-gray-600 hover:text-blue-500'
              }`}
            >
              对话界面
            </button>
            <button
              onClick={() => setCurrentPage('settings')}
              className={`px-6 py-2 rounded-md transition-colors ${
                currentPage === 'settings'
                  ? 'bg-blue-500 text-white'
                  : 'text-gray-600 hover:text-blue-500'
              }`}
            >
              设置
            </button>
            <button
              onClick={() => setCurrentPage('test')}
              className={`px-6 py-2 rounded-md transition-colors ${
                currentPage === 'test'
                  ? 'bg-blue-500 text-white'
                  : 'text-gray-600 hover:text-blue-500'
              }`}
            >
              音频测试
            </button>
            <button
              onClick={() => setCurrentPage('conversation-test')}
              className={`px-6 py-2 rounded-md transition-colors ${
                currentPage === 'conversation-test'
                  ? 'bg-blue-500 text-white'
                  : 'text-gray-600 hover:text-blue-500'
              }`}
            >
              对话测试
            </button>
            <button
              onClick={() => setCurrentPage('settings-test')}
              className={`px-6 py-2 rounded-md transition-colors ${
                currentPage === 'settings-test'
                  ? 'bg-blue-500 text-white'
                  : 'text-gray-600 hover:text-blue-500'
              }`}
            >
              设置测试
            </button>
          </div>
        </div>

        <main>
           {currentPage === 'conversation' && <ConversationInterface />}
           {currentPage === 'settings' && <Settings onBack={() => setCurrentPage('conversation')} />}
           {currentPage === 'test' && <AudioTest />}
           {currentPage === 'conversation-test' && <ConversationTest />}
           {currentPage === 'settings-test' && <SettingsTest />}
         </main>
      </div>
    </div>
  );
};