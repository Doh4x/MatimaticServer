using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace MatimaticServer
{
    public class PlayerState
    {
        public string Nickname { get; set; } = "";
        public WebSocket Socket { get; set; } = null!;
        public int?[][] Grid { get; set; } = Enumerable.Range(0, 5).Select(_ => new int?[5]).ToArray();
        public bool PlacedThisTurn { get; set; }
    }

    public class GameHub
    {
        private readonly ConcurrentDictionary<string, PlayerState> _players = new();
        private readonly List<int> _deck = new();

        private int _currentCardIndex;
        private int _turnNumber;
        private bool _gameStarted;
        private bool _lobbyCountdownStarted;
        private int _currentCardValue;

        private const int MaxPlayers = 10;
        private const int LobbySeconds = 60;
        private const int TurnSeconds = 10;
        private const int TotalTurns = 25;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public async Task HandleConnection(WebSocket socket)
        {
            string? nickname = null;
            var buffer = new byte[4096];

            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var msg = JsonSerializer.Deserialize<NetworkMessage>(json);
                    if (msg == null) continue;

                    if (msg.Type == MessageType.Join)
                    {
                        var payload = Deserialize<JoinPayload>(msg.Payload);
                        if (payload == null) continue;
                        nickname = await HandleJoin(socket, payload.Nickname);
                    }
                    else if (msg.Type == MessageType.PlaceCard && nickname != null)
                    {
                        var payload = Deserialize<PlaceCardPayload>(msg.Payload);
                        if (payload != null)
                            await HandlePlaceCard(nickname, payload);
                    }
                }
            }
            finally
            {
                if (nickname != null)
                {
                    _players.TryRemove(nickname, out _);
                    if (!_gameStarted)
                        await BroadcastLobbyUpdate(LobbySeconds);
                }

                if (socket.State == WebSocketState.Open)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);

                socket.Dispose();
            }
        }

        private async Task<string?> HandleJoin(WebSocket socket, string nickname)
        {
            lock (_players)
            {
                if (_gameStarted || _players.Count >= MaxPlayers || _players.ContainsKey(nickname))
                {
                    _ = SendSafeAsync(socket, MessageType.Error, "Нельзя присоединиться");
                    return null;
                }

                _players[nickname] = new PlayerState
                {
                    Nickname = nickname,
                    Socket = socket
                };
            }

            await BroadcastLobbyUpdate(LobbySeconds);

            if (!_lobbyCountdownStarted)
            {
                _lobbyCountdownStarted = true;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await LobbyCountdown();
                    }
                    catch
                    {
                        Reset();
                    }
                });
            }

            return nickname;
        }

        private async Task LobbyCountdown()
        {
            for (int s = LobbySeconds; s >= 0; s--)
            {
                await BroadcastLobbyUpdate(s);
                await Task.Delay(1000);

                lock (_players)
                {
                    if (_players.Count >= MaxPlayers) break;
                }
            }

            lock (_players)
            {
                if (_players.Count < 2)
                {
                    _lobbyCountdownStarted = false;
                    return;
                }
            }

            await StartGame();
        }

        private async Task StartGame()
        {
            lock (_players)
            {
                _gameStarted = true;
                BuildDeck();
            }

            await BroadcastAsync(MessageType.GameStart, new GameStartPayload
            {
                Players = _players.Keys.ToList()
            });

            await RunGameLoop();
        }

        private async Task RunGameLoop()
        {
            for (_turnNumber = 1; _turnNumber <= TotalTurns; _turnNumber++)
            {
                _currentCardValue = _deck[_currentCardIndex++];

                foreach (var p in _players.Values)
                    p.PlacedThisTurn = false;

                await BroadcastAsync(MessageType.CardDealt, new CardDealtPayload
                {
                    CardValue = _currentCardValue,
                    TurnNumber = _turnNumber,
                    SecondsToPlace = TurnSeconds
                });

                await WaitForMoves();
                await AutoPlaceForIdlePlayers();
                await BroadcastAsync(MessageType.TurnTimeout, new { Turn = _turnNumber });
            }

            await FinishGame();
        }

        private async Task WaitForMoves()
        {
            for (int i = 0; i < TurnSeconds * 10; i++)
            {
                await Task.Delay(100);

                lock (_players)
                {
                    if (_players.Values.All(p => p.PlacedThisTurn))
                        return;
                }
            }
        }

        private async Task AutoPlaceForIdlePlayers()
        {
            var rnd = new Random();
            foreach (var player in _players.Values)
            {
                if (player.PlacedThisTurn) continue;

                var empty = new List<(int Row, int Col)>();
                for (int r = 0; r < 5; r++)
                    for (int c = 0; c < 5; c++)
                        if (!player.Grid[r][c].HasValue)
                            empty.Add((r, c));

                if (empty.Count == 0) continue;

                var pos = empty[rnd.Next(empty.Count)];
                player.Grid[pos.Row][pos.Col] = _currentCardValue;
                player.PlacedThisTurn = true;

                await BroadcastAsync(MessageType.PlayerMoved, new PlayerMovedPayload
                {
                    Nickname = player.Nickname,
                    Row = pos.Row,
                    Col = pos.Col,
                    CardValue = _currentCardValue
                });
            }
        }

        private async Task HandlePlaceCard(string nickname, PlaceCardPayload payload)
        {
            if (!_players.TryGetValue(nickname, out var player)) return;

            lock (player)
            {
                if (player.PlacedThisTurn) return;
                if (!_gameStarted) return;
                if (payload.Row < 0 || payload.Row > 4 || payload.Col < 0 || payload.Col > 4) return;
                if (player.Grid[payload.Row][payload.Col].HasValue) return;

                player.Grid[payload.Row][payload.Col] = _currentCardValue;
                player.PlacedThisTurn = true;
            }

            await BroadcastAsync(MessageType.PlayerMoved, new PlayerMovedPayload
            {
                Nickname = nickname,
                Row = payload.Row,
                Col = payload.Col,
                CardValue = _currentCardValue
            });
        }

        private async Task FinishGame()
        {
            var results = _players.Values
                .Select(p => new PlayerResult
                {
                    Nickname = p.Nickname,
                    Score = ScoreEngine.CalculateTotalScore(p.Grid),
                    Grid = p.Grid
                })
                .OrderByDescending(r => r.Score)
                .ToList();

            await BroadcastAsync(MessageType.GameOver, new GameOverPayload { Results = results });
            Reset();
        }

        private void Reset()
        {
            _gameStarted = false;
            _lobbyCountdownStarted = false;
            _currentCardIndex = 0;
            _turnNumber = 0;
            _currentCardValue = 0;
            _deck.Clear();
            _players.Clear();
        }

        private void BuildDeck()
        {
            _deck.Clear();

            for (int v = 1; v <= 13; v++)
                for (int i = 0; i < 4; i++)
                    _deck.Add(v);

            var rnd = new Random();
            for (int i = _deck.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (_deck[i], _deck[j]) = (_deck[j], _deck[i]);
            }

            _currentCardIndex = 0;
        }

        private async Task BroadcastLobbyUpdate(int secondsLeft)
        {
            await BroadcastAsync(MessageType.LobbyUpdate, new LobbyUpdatePayload
            {
                Players = _players.Keys.ToList(),
                SecondsLeft = secondsLeft
            });
        }

        private async Task BroadcastAsync<T>(MessageType type, T payload)
        {
            var tasks = _players.Values
                .Where(p => p.Socket.State == WebSocketState.Open)
                .Select(p => SendSafeAsync(p.Socket, type, payload));

            await Task.WhenAll(tasks);
        }

        private static async Task SendSafeAsync<T>(WebSocket socket, MessageType type, T payload)
        {
            try
            {
                await SendAsync(socket, type, payload);
            }
            catch
            {
            }
        }

        private static async Task SendAsync<T>(WebSocket socket, MessageType type, T payload)
        {
            var msg = new NetworkMessage
            {
                Type = type,
                Payload = JsonSerializer.Serialize(payload, JsonOptions)
            };

            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg, JsonOptions));
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static T? Deserialize<T>(string? json)
        {
            if (string.IsNullOrEmpty(json)) return default;
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}