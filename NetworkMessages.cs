namespace MatimaticServer
{
    public enum MessageType
    {
        Join,
        LobbyUpdate,
        GameStart,
        CardDealt,
        PlaceCard,
        PlayerMoved,
        GameOver,
        Error
    }

    public class NetworkMessage
    {
        public MessageType Type { get; set; }
        public string? Payload { get; set; }
    }

    public class JoinPayload
    {
        public string Nickname { get; set; } = "";
    }

    public class LobbyUpdatePayload
    {
        public List<string> Players { get; set; } = new();
        public int SecondsLeft { get; set; }
    }

    public class GameStartPayload
    {
        public List<string> Players { get; set; } = new();
    }

    public class CardDealtPayload
    {
        public int CardValue { get; set; }
        public int TurnNumber { get; set; }
        public int SecondsToPlace { get; set; }
    }

    public class PlaceCardPayload
    {
        public int Row { get; set; }
        public int Col { get; set; }
    }

    public class PlayerMovedPayload
    {
        public string Nickname { get; set; } = "";
        public int Row { get; set; }
        public int Col { get; set; }
        public int CardValue { get; set; }
    }

    public class GameOverPayload
    {
        public List<PlayerResult> Results { get; set; } = new();
    }

    public class PlayerResult
    {
        public string Nickname { get; set; } = "";
        public int Score { get; set; }
        public int?[][] Grid { get; set; } = new int?[5][];
    }
}