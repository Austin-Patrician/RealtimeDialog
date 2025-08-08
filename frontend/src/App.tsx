import { useEffect } from 'react';
import { Home } from './pages/Home';
import { useConversationStore } from './lib/store';
import { checkBrowserSupport } from './lib/utils';

function App() {
  const { initialize, cleanup } = useConversationStore();

  useEffect(() => {
    // 检查浏览器支持
    const support = checkBrowserSupport();
    if (!support.webAudio || !support.mediaDevices || !support.webSocket) {
      console.warn('Browser support check:', support);
      alert('您的浏览器可能不支持某些功能，建议使用最新版本的Chrome、Firefox或Safari。');
    }

    // 从本地存储加载服务器URL，默认为localhost:5000
    const savedServerUrl = localStorage.getItem('serverUrl') || 'http://localhost:5000';
    
    // 初始化应用
    initialize(savedServerUrl);

    // 清理函数
    return () => {
      cleanup();
    };
  }, [initialize, cleanup]);

  return <Home />;
}

export default App;
