using System;
using System.Collections.Concurrent;
using System.Text;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
#endif

public class WebSocketClient
{
    private readonly ConcurrentQueue<string> _incoming = new();
    private bool _intentionalClose;

    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;

#if UNITY_WEBGL && !UNITY_EDITOR

    // ───── WebGL implementation (browser-native WebSocket via jslib) ─────

    [DllImport("__Internal")] private static extern int WebSocket_Connect(string url);
    [DllImport("__Internal")] private static extern void WebSocket_Send(int id, string msg);
    [DllImport("__Internal")] private static extern void WebSocket_Close(int id);
    [DllImport("__Internal")] private static extern int WebSocket_GetState(int id);
    [DllImport("__Internal")] private static extern IntPtr WebSocket_GetNextMessage(int id);
    [DllImport("__Internal")] private static extern void WebSocket_FreeMsgBuffer(IntPtr ptr);

    private int _wsId = -1;

    public bool IsConnected => _wsId >= 0 && WebSocket_GetState(_wsId) == 1;

    public void Connect(string url)
    {
        _intentionalClose = false;
        _wsId = WebSocket_Connect(url);
    }

    public void Send(string message)
    {
        if (_wsId >= 0 && WebSocket_GetState(_wsId) == 1)
            WebSocket_Send(_wsId, message);
    }

    public void Close()
    {
        _intentionalClose = true;
        if (_wsId >= 0)
            WebSocket_Close(_wsId);
        _wsId = -1;
    }

    /// <summary>
    /// Drain all queued messages. Call from Unity main thread in Update().
    /// In WebGL, this also polls the JS-side message queue and handles
    /// internal control messages (__open, __close, __error).
    /// </summary>
    public int DrainMessages(Action<string> handler)
    {
        if (_wsId < 0) return 0;

        int count = 0;
        IntPtr ptr;
        while ((ptr = WebSocket_GetNextMessage(_wsId)) != IntPtr.Zero)
        {
            string msg = PtrToStringUTF8(ptr);
            WebSocket_FreeMsgBuffer(ptr);

            if (msg != null && msg.StartsWith("{\"type\":\"__"))
            {
                if (msg.Contains("\"__open\""))
                    OnConnected?.Invoke();
                else if (msg.Contains("\"__close\""))
                    OnDisconnected?.Invoke();
                else if (msg.Contains("\"__error\""))
                    OnError?.Invoke("WebSocket error");
                continue;
            }

            handler(msg);
            count++;
        }

        return count;
    }

    private static string PtrToStringUTF8(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return null;
        int len = 0;
        while (Marshal.ReadByte(ptr, len) != 0) len++;
        if (len == 0) return "";
        byte[] bytes = new byte[len];
        Marshal.Copy(ptr, bytes, 0, len);
        return Encoding.UTF8.GetString(bytes);
    }

#else

    // ───── Native implementation (System.Net.WebSockets) ─────

    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    private Uri _uri;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public async void Connect(string url)
    {
        _uri = new Uri(url);
        _intentionalClose = false;
        await ConnectInternal();
    }

    private async Task ConnectInternal()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            _ws?.Dispose();
            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(_uri, _cts.Token);
            OnConnected?.Invoke();
            _ = ReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocket] Connect failed: {e.Message}");
            OnError?.Invoke(e.Message);
        }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[4096];

        try
        {
            while (_ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                var sb = new StringBuilder();
                WebSocketReceiveResult result;

                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        HandleDisconnect();
                        return;
                    }
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                _incoming.Enqueue(sb.ToString());
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException e)
        {
            Debug.LogWarning($"[WebSocket] Receive error: {e.Message}");
            HandleDisconnect();
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocket] Unexpected error: {e.Message}");
            HandleDisconnect();
        }
    }

    private async void HandleDisconnect()
    {
        OnDisconnected?.Invoke();

        if (_intentionalClose) return;

        // Auto-reconnect with backoff
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            int delay = Math.Min(1000 * attempt, 5000);
            Debug.Log($"[WebSocket] Reconnecting in {delay}ms (attempt {attempt})...");
            await Task.Delay(delay);

            if (_intentionalClose) return;

            try
            {
                _ws?.Dispose();
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(_uri, _cts.Token);
                Debug.Log("[WebSocket] Reconnected!");
                OnConnected?.Invoke();
                _ = ReceiveLoop();
                return;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebSocket] Reconnect attempt {attempt} failed: {e.Message}");
            }
        }

        Debug.LogError("[WebSocket] Failed to reconnect after 5 attempts.");
        OnError?.Invoke("Failed to reconnect");
    }

    public void Send(string message)
    {
        if (_ws?.State != WebSocketState.Open) return;

        var bytes = Encoding.UTF8.GetBytes(message);
        _ = _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
    }

    public async void Close()
    {
        _intentionalClose = true;
        _cts?.Cancel();

        if (_ws?.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
            }
            catch { }
        }

        _ws?.Dispose();
        _ws = null;
    }

    /// <summary>
    /// Drain all queued messages. Call from Unity main thread in Update().
    /// </summary>
    public int DrainMessages(Action<string> handler)
    {
        int count = 0;
        while (_incoming.TryDequeue(out string msg))
        {
            handler(msg);
            count++;
        }
        return count;
    }

#endif
}
