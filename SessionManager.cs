using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace MatimaticServer
{
    public class SessionManager
    {
        private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();

        public async Task HandleConnection(WebSocket socket)
        {
            var session = GetAvailableSession();
            await session.HandleConnection(socket);
        }

        private GameSession GetAvailableSession()
        {
            foreach (var session in _sessions.Values)
            {
                if (session.CanJoin)
                    return session;
            }

            var id = Guid.NewGuid();
            var newSession = new GameSession();
            newSession.OnEnded += () => _sessions.TryRemove(id, out _);
            _sessions[id] = newSession;
            return newSession;
        }
    }
}