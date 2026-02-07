using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class RoomManager : MonoBehaviour
{
    private const string BASE_URL = "https://semantic-chess-rooms.raphael-tan-fr.workers.dev";

    private WebSocketClient _ws;
    private string _roomCode;
    private PieceColor _localColor;

    public string RoomCode => _roomCode;
    public PieceColor LocalColor => _localColor;
    public bool IsConnected => _ws != null && _ws.IsConnected;

    // --- Events ---
    public event Action<string, PieceColor> OnRoomJoined;      // (code, assignedColor)
    public event Action OnGameStart;
    public event Action<int, int> OnOpponentMove;               // (from, to)
    public event Action<int, int, ElementMixResult, ElementReactionResult> OnOpponentCaptureResult;
    public event Action<int> OnOpponentHover;
    public event Action<int> OnOpponentSelect;
    public event Action OnOpponentDeselect;
    public event Action OnOpponentDisconnect;
    public event Action OnOpponentReconnect;
    public event Action OnOpponentResign;
    public event Action OnRematchRequested;
    public event Action OnRematchStart;
    public event Action<string> OnError;
    public event Action<List<PieceSlotConfig>> OnOpponentBoardConfig;

    private void Update()
    {
        _ws?.DrainMessages(HandleMessage);
    }

    // --- Public API ---

    public void CreateRoom()
    {
        StartCoroutine(CreateRoomCoroutine());
    }

    public void JoinRoom(string code)
    {
        StartCoroutine(JoinRoomCoroutine(code.ToUpper().Trim()));
    }

    public void SendMove(int from, int to)
    {
        SendJson(new WsOutMessage { type = "move", from = from, to = to });
    }

    public void SendCaptureResult(int from, int to, ElementMixResult mix, ElementReactionResult reaction)
    {
        string mixJson = JsonUtility.ToJson(mix);
        string reactionJson = SerializeReaction(reaction);
        _ws?.Send($"{{\"type\":\"capture_result\",\"from\":{from},\"to\":{to},\"mix\":{mixJson},\"reaction\":{reactionJson}}}");
    }

    public void SendBoardConfig(List<PieceSlotConfig> slots)
    {
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"board_config\",\"slots\":[");
        for (int i = 0; i < slots.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(JsonUtility.ToJson(slots[i]));
        }
        sb.Append("]}");
        _ws?.Send(sb.ToString());
    }

    public void SendHover(int index)
    {
        SendJson(new WsOutMessage { type = "hover", index = index });
    }

    public void SendSelect(int index)
    {
        SendJson(new WsOutMessage { type = "select", index = index });
    }

    public void SendDeselect()
    {
        _ws?.Send("{\"type\":\"deselect\"}");
    }

    public void SendResign()
    {
        _ws?.Send("{\"type\":\"resign\"}");
    }

    public void SendRematch()
    {
        _ws?.Send("{\"type\":\"rematch\"}");
    }

    public void Disconnect()
    {
        _ws?.Close();
        _ws = null;
        _roomCode = null;
    }

    // --- HTTP Calls ---

    private System.Collections.IEnumerator CreateRoomCoroutine()
    {
        using var www = new UnityWebRequest($"{BASE_URL}/rooms", "POST");
        www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.timeout = 10;

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[RoomManager] Create room failed: {www.error}");
            OnError?.Invoke("Failed to create room");
            yield break;
        }

        var response = JsonUtility.FromJson<RoomResponse>(www.downloadHandler.text);
        ConnectWebSocket(response.code);
    }

    private System.Collections.IEnumerator JoinRoomCoroutine(string code)
    {
        string body = $"{{\"code\":\"{code}\"}}";
        using var www = new UnityWebRequest($"{BASE_URL}/rooms/join", "POST");
        www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.timeout = 10;

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            string errorBody = www.downloadHandler?.text ?? "";
            Debug.LogError($"[RoomManager] Join room failed: {www.error} — {errorBody}");

            if (www.responseCode == 404)
                OnError?.Invoke("Room not found");
            else if (www.responseCode == 409)
                OnError?.Invoke("Room is full");
            else
                OnError?.Invoke("Failed to join room");
            yield break;
        }

        var response = JsonUtility.FromJson<RoomResponse>(www.downloadHandler.text);
        ConnectWebSocket(response.code);
    }

    // --- WebSocket ---

    private void ConnectWebSocket(string code)
    {
        _ws?.Close();
        _ws = new WebSocketClient();

        _ws.OnDisconnected += () => Debug.Log("[RoomManager] WebSocket disconnected");
        _ws.OnError += err => OnError?.Invoke(err);

        string wsUrl = BASE_URL.Replace("https://", "wss://").Replace("http://", "ws://");
        _ws.Connect($"{wsUrl}/rooms/{code}/ws");
    }

    // --- Message Handling ---

    private void HandleMessage(string raw)
    {
        var msg = JsonUtility.FromJson<WsInMessage>(raw);

        switch (msg.type)
        {
            case "joined":
                _roomCode = msg.code;
                _localColor = msg.color == "white" ? PieceColor.White : PieceColor.Black;
                Debug.Log($"[RoomManager] Joined room {_roomCode} as {_localColor}");
                OnRoomJoined?.Invoke(_roomCode, _localColor);
                break;

            case "game_start":
                Debug.Log("[RoomManager] Game starting!");
                OnGameStart?.Invoke();
                break;

            case "opponent_move":
                OnOpponentMove?.Invoke(msg.from, msg.to);
                break;

            case "opponent_capture_result":
                var mix = ParseMixFromRaw(raw);
                var reaction = ParseReactionFromRaw(raw);
                OnOpponentCaptureResult?.Invoke(msg.from, msg.to, mix, reaction);
                break;

            case "opponent_board_config":
                var configSlots = ParseBoardConfigSlots(raw);
                OnOpponentBoardConfig?.Invoke(configSlots);
                break;

            case "opponent_hover":
                OnOpponentHover?.Invoke(msg.index);
                break;

            case "opponent_select":
                OnOpponentSelect?.Invoke(msg.index);
                break;

            case "opponent_deselect":
                OnOpponentDeselect?.Invoke();
                break;

            case "opponent_disconnect":
                Debug.Log("[RoomManager] Opponent disconnected");
                OnOpponentDisconnect?.Invoke();
                break;

            case "opponent_reconnect":
                Debug.Log("[RoomManager] Opponent reconnected");
                OnOpponentReconnect?.Invoke();
                break;

            case "opponent_resign":
                OnOpponentResign?.Invoke();
                break;

            case "rematch_requested":
                OnRematchRequested?.Invoke();
                break;

            case "rematch_start":
                OnRematchStart?.Invoke();
                break;

            case "error":
                Debug.LogError($"[RoomManager] Server error: {msg.message}");
                OnError?.Invoke(msg.message);
                break;
        }
    }

    // --- JSON Helpers ---

    private void SendJson(WsOutMessage msg)
    {
        _ws?.Send(JsonUtility.ToJson(msg));
    }

    private static string SerializeReaction(ElementReactionResult reaction)
    {
        // JsonUtility can't handle arrays with optional fields well, so build manually
        var sb = new StringBuilder();
        sb.Append("{\"effects\":[");
        for (int i = 0; i < reaction.effects.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(JsonUtility.ToJson(reaction.effects[i]));
        }
        sb.Append("],\"flavor\":\"");
        sb.Append(reaction.flavor?.Replace("\"", "\\\"") ?? "");
        sb.Append("\"}");
        return sb.ToString();
    }

    /// <summary>Extract the "mix" sub-object from a raw JSON message and parse it.</summary>
    private static ElementMixResult ParseMixFromRaw(string raw)
    {
        try
        {
            string sub = ExtractSubObject(raw, "mix");
            return sub != null ? JsonUtility.FromJson<ElementMixResult>(sub) : null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomManager] Failed to parse mix: {e.Message}");
            return null;
        }
    }

    /// <summary>Extract the "reaction" sub-object from a raw JSON message and parse it.</summary>
    private static ElementReactionResult ParseReactionFromRaw(string raw)
    {
        try
        {
            string sub = ExtractSubObject(raw, "reaction");
            if (sub == null) return null;

            // Reuse the same manual array parsing as ElementService
            return new ElementReactionResult
            {
                flavor = ExtractStringField(sub, "flavor"),
                effects = ParseEffectsArray(sub)
            };
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomManager] Failed to parse reaction: {e.Message}");
            return null;
        }
    }

    // --- Shared JSON parsing (same logic as ElementService) ---

    private static string ExtractSubObject(string json, string fieldName)
    {
        string marker = $"\"{fieldName}\"";
        int idx = json.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;

        int colonIdx = json.IndexOf(':', idx + marker.Length);
        if (colonIdx < 0) return null;

        int start = json.IndexOf('{', colonIdx + 1);
        if (start < 0) return null;

        int depth = 0;
        bool inStr = false;
        for (int i = start; i < json.Length; i++)
        {
            if (inStr) { if (json[i] == '\\') { i++; continue; } if (json[i] == '"') inStr = false; continue; }
            if (json[i] == '"') { inStr = true; continue; }
            if (json[i] == '{') depth++;
            else if (json[i] == '}') { depth--; if (depth == 0) return json[start..(i + 1)]; }
        }
        return null;
    }

    private static string ExtractStringField(string json, string fieldName)
    {
        string[] markers = { $"\"{fieldName}\":\"", $"\"{fieldName}\": \"" };
        int start = -1;
        foreach (string m in markers)
        {
            int idx = json.IndexOf(m, StringComparison.Ordinal);
            if (idx >= 0) { start = idx + m.Length; break; }
        }
        if (start < 0) return null;

        var sb = new StringBuilder();
        for (int i = start; i < json.Length; i++)
        {
            if (json[i] == '\\' && i + 1 < json.Length) { sb.Append(json[i + 1]); i++; }
            else if (json[i] == '"') break;
            else sb.Append(json[i]);
        }
        return sb.ToString();
    }

    private static int ExtractIntField(string json, string fieldName, int defaultValue)
    {
        string[] markers = { $"\"{fieldName}\":", $"\"{fieldName}\" :" };
        int start = -1;
        foreach (string m in markers)
        {
            int idx = json.IndexOf(m, StringComparison.Ordinal);
            if (idx >= 0) { start = idx + m.Length; break; }
        }
        if (start < 0) return defaultValue;

        while (start < json.Length && json[start] == ' ') start++;

        var sb = new StringBuilder();
        if (start < json.Length && json[start] == '-') { sb.Append('-'); start++; }
        while (start < json.Length && char.IsDigit(json[start])) { sb.Append(json[start]); start++; }

        return int.TryParse(sb.ToString(), out int val) ? val : defaultValue;
    }

    private static bool ExtractBoolField(string json, string fieldName, bool defaultValue)
    {
        string[] markers = { $"\"{fieldName}\":", $"\"{fieldName}\" :" };
        int start = -1;
        foreach (string m in markers)
        {
            int idx = json.IndexOf(m, StringComparison.Ordinal);
            if (idx >= 0) { start = idx + m.Length; break; }
        }
        if (start < 0) return defaultValue;

        while (start < json.Length && json[start] == ' ') start++;

        if (start + 4 <= json.Length && json.Substring(start, 4) == "true") return true;
        if (start + 5 <= json.Length && json.Substring(start, 5) == "false") return false;
        return defaultValue;
    }

    private static ReactionEffectEntry[] ParseEffectsArray(string json)
    {
        string marker = "\"effects\"";
        int idx = json.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return Array.Empty<ReactionEffectEntry>();

        int arrStart = json.IndexOf('[', idx + marker.Length);
        if (arrStart < 0) return Array.Empty<ReactionEffectEntry>();

        int depth = 0, arrEnd = -1;
        bool inStr = false;
        for (int i = arrStart; i < json.Length; i++)
        {
            if (inStr) { if (json[i] == '\\') { i++; continue; } if (json[i] == '"') inStr = false; continue; }
            if (json[i] == '"') { inStr = true; continue; }
            if (json[i] == '[') depth++;
            else if (json[i] == ']') { depth--; if (depth == 0) { arrEnd = i; break; } }
        }
        if (arrEnd < 0) return Array.Empty<ReactionEffectEntry>();

        string content = json[(arrStart + 1)..arrEnd];
        var entries = new System.Collections.Generic.List<ReactionEffectEntry>();
        depth = 0;
        int objStart = -1;
        inStr = false;

        for (int i = 0; i < content.Length; i++)
        {
            if (inStr) { if (content[i] == '\\') { i++; continue; } if (content[i] == '"') inStr = false; continue; }
            if (content[i] == '"') { inStr = true; continue; }
            if (content[i] == '{') { if (depth == 0) objStart = i; depth++; }
            else if (content[i] == '}')
            {
                depth--;
                if (depth == 0 && objStart >= 0)
                {
                    var entry = ParseEffectEntry(content[objStart..(i + 1)]);
                    if (entry != null) entries.Add(entry);
                    objStart = -1;
                }
            }
        }

        return entries.ToArray();
    }

    private static ReactionEffectEntry ParseEffectEntry(string json)
    {
        string pattern = ExtractStringField(json, "pattern");
        string effect = ExtractStringField(json, "effect");
        if (pattern == null || effect == null) return null;

        return new ReactionEffectEntry
        {
            pattern = pattern,
            distance = ExtractIntField(json, "distance", 1),
            obstructed = ExtractBoolField(json, "obstructed", true),
            target = ExtractStringField(json, "target") ?? "all",
            effect = effect,
            direction = ExtractStringField(json, "direction"),
            push_distance = ExtractIntField(json, "push_distance", 1),
            piece_type = ExtractStringField(json, "piece_type"),
            duration = ExtractIntField(json, "duration", 0)
        };
    }

    private List<PieceSlotConfig> ParseBoardConfigSlots(string raw)
    {
        var result = new List<PieceSlotConfig>();
        try
        {
            // Find the "slots" array
            string marker = "\"slots\"";
            int idx = raw.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return result;

            int arrStart = raw.IndexOf('[', idx + marker.Length);
            if (arrStart < 0) return result;

            int depth = 0, arrEnd = -1;
            bool inStr = false;
            for (int i = arrStart; i < raw.Length; i++)
            {
                if (inStr) { if (raw[i] == '\\') { i++; continue; } if (raw[i] == '"') inStr = false; continue; }
                if (raw[i] == '"') { inStr = true; continue; }
                if (raw[i] == '[') depth++;
                else if (raw[i] == ']') { depth--; if (depth == 0) { arrEnd = i; break; } }
            }
            if (arrEnd < 0) return result;

            string content = raw[(arrStart + 1)..arrEnd];
            depth = 0;
            int objStart = -1;
            inStr = false;

            for (int i = 0; i < content.Length; i++)
            {
                if (inStr) { if (content[i] == '\\') { i++; continue; } if (content[i] == '"') inStr = false; continue; }
                if (content[i] == '"') { inStr = true; continue; }
                if (content[i] == '{') { if (depth == 0) objStart = i; depth++; }
                else if (content[i] == '}')
                {
                    depth--;
                    if (depth == 0 && objStart >= 0)
                    {
                        string objJson = content[objStart..(i + 1)];
                        var slot = new PieceSlotConfig
                        {
                            index = ExtractIntField(objJson, "index", 0),
                            element = ExtractStringField(objJson, "element") ?? "",
                            emoji = ExtractStringField(objJson, "emoji") ?? ""
                        };
                        result.Add(slot);
                        objStart = -1;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomManager] Failed to parse board config: {e.Message}");
        }
        return result;
    }

    // --- Data Structures ---

    [Serializable]
    private class RoomResponse
    {
        public string roomId;
        public string code;
        public string error;
    }

    [Serializable]
    private class WsInMessage
    {
        public string type;
        public int from;
        public int to;
        public int index;
        public string color;
        public string code;
        public string message;
    }

    [Serializable]
    private class WsOutMessage
    {
        public string type;
        public int from;
        public int to;
        public int index;
    }
}
