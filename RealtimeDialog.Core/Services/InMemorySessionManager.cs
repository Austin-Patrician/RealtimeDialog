using System.Collections.Concurrent;
using RealtimeDialog.Core.Protocol;
using Microsoft.Extensions.Logging;

namespace RealtimeDialog.Core.Services;

public class InMemorySessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, ConversationSession> _sessions = new();
    private readonly ILogger<InMemorySessionManager> _logger;

    public InMemorySessionManager(ILogger<InMemorySessionManager> logger)
    {
        _logger = logger;
    }

    public Task<ConversationSession> CreateSessionAsync(string sessionId, AudioConfig audioConfig, string? userId = null)
    {
        var session = new ConversationSession
        {
            SessionId = sessionId,
            UserId = userId,
            StartTime = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            Status = SessionStatus.Initializing,
            AudioConfig = audioConfig,
            ConnectionId = string.Empty
        };

        _sessions.TryAdd(sessionId, session);
        _logger.LogInformation("Created new session {SessionId} for user {UserId}", sessionId, userId ?? "anonymous");
        
        return Task.FromResult(session);
    }

    public Task<ConversationSession?> GetSessionAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public async Task UpdateSessionStatusAsync(string sessionId, SessionStatus status)
    {
        var session = await GetSessionAsync(sessionId);
        if (session != null)
        {
            session.Status = status;
            session.LastActivity = DateTime.UtcNow;
            _logger.LogDebug("Updated session {SessionId} status to {Status}", sessionId, status);
        }
    }

    public async Task UpdateLastActivityAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session != null)
        {
            session.LastActivity = DateTime.UtcNow;
        }
    }

    public async Task SetConnectionIdAsync(string sessionId, string connectionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session != null)
        {
            session.ConnectionId = connectionId;
            _logger.LogDebug("Set connection ID {ConnectionId} for session {SessionId}", connectionId, sessionId);
        }
    }

    public Task RemoveSessionAsync(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            // 清理WebSocket连接
            session.DoubaoWebSocket?.Dispose();
            _logger.LogInformation("Removed session {SessionId}", sessionId);
        }
        return Task.CompletedTask;
    }

    public async Task<bool> IsSessionActiveAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        return session != null && session.Status != SessionStatus.Ended && session.Status != SessionStatus.Error;
    }

    public Task<IEnumerable<ConversationSession>> GetActiveSessionsAsync()
    {
        var activeSessions = _sessions.Values
            .Where(s => s.Status != SessionStatus.Ended && s.Status != SessionStatus.Error)
            .ToList();
        
        return Task.FromResult<IEnumerable<ConversationSession>>(activeSessions);
    }

    public async Task CleanupExpiredSessionsAsync(TimeSpan expireAfter)
    {
        var expiredSessions = _sessions.Values
            .Where(s => DateTime.UtcNow - s.LastActivity > expireAfter)
            .ToList();

        foreach (var session in expiredSessions)
        {
            await RemoveSessionAsync(session.SessionId);
            _logger.LogInformation("Cleaned up expired session {SessionId}", session.SessionId);
        }
    }
}