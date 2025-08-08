using RealtimeDialog.Core.Protocol;

namespace RealtimeDialog.Core.Services;

public interface ISessionManager
{
    /// <summary>
    /// 创建新的对话会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="audioConfig">音频配置</param>
    /// <param name="userId">用户ID（可选）</param>
    /// <returns>创建的会话</returns>
    Task<ConversationSession> CreateSessionAsync(string sessionId, AudioConfig audioConfig, string? userId = null);
    
    /// <summary>
    /// 获取指定会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>会话对象，如果不存在则返回null</returns>
    Task<ConversationSession?> GetSessionAsync(string sessionId);
    
    /// <summary>
    /// 更新会话状态
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="status">新状态</param>
    Task UpdateSessionStatusAsync(string sessionId, SessionStatus status);
    
    /// <summary>
    /// 更新会话的最后活动时间
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    Task UpdateLastActivityAsync(string sessionId);
    
    /// <summary>
    /// 设置会话的连接ID
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="connectionId">SignalR连接ID</param>
    Task SetConnectionIdAsync(string sessionId, string connectionId);
    
    /// <summary>
    /// 移除会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    Task RemoveSessionAsync(string sessionId);
    
    /// <summary>
    /// 检查会话是否活跃
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>是否活跃</returns>
    Task<bool> IsSessionActiveAsync(string sessionId);
    
    /// <summary>
    /// 获取所有活跃会话
    /// </summary>
    /// <returns>活跃会话列表</returns>
    Task<IEnumerable<ConversationSession>> GetActiveSessionsAsync();
    
    /// <summary>
    /// 清理过期会话
    /// </summary>
    /// <param name="expireAfter">过期时间</param>
    Task CleanupExpiredSessionsAsync(TimeSpan expireAfter);
}