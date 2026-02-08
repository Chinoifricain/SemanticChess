using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class EmojiLoader : MonoBehaviour
{
    private const string TWEMOJI_BASE = "https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/72x72/";
    private const float EMOJI_PPU = 200f;

    private readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, List<Action<Sprite>>> _pending = new Dictionary<string, List<Action<Sprite>>>();

    public Sprite GetCached(string emoji)
    {
        if (string.IsNullOrEmpty(emoji)) return null;
        string key = GetFirstEmoji(emoji);
        _cache.TryGetValue(key, out Sprite s);
        return s;
    }

    public IEnumerator Load(string emoji, Action<Sprite> callback)
    {
        if (string.IsNullOrEmpty(emoji)) { callback?.Invoke(null); yield break; }

        string key = GetFirstEmoji(emoji);
        if (string.IsNullOrEmpty(key)) { callback?.Invoke(null); yield break; }

        if (_cache.TryGetValue(key, out Sprite cached))
        {
            callback?.Invoke(cached);
            yield break;
        }

        if (_pending.TryGetValue(key, out var waiters))
        {
            waiters.Add(callback);
            yield break;
        }

        _pending[key] = new List<Action<Sprite>> { callback };

        string url = EmojiToUrl(key);
        using var www = new UnityWebRequest(url, "GET");
        www.downloadHandler = new DownloadHandlerBuffer();
        www.timeout = 10;
        yield return www.SendWebRequest();

        Sprite sprite = null;
        if (www.result == UnityWebRequest.Result.Success)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(www.downloadHandler.data);
            tex.filterMode = FilterMode.Bilinear;
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), EMOJI_PPU);
        }
        else
        {
            Debug.LogWarning("[EmojiLoader] Failed '" + key + "': " + www.error);
        }

        _cache[key] = sprite;

        if (_pending.TryGetValue(key, out var cbs))
        {
            foreach (var cb in cbs) cb?.Invoke(sprite);
            _pending.Remove(key);
        }
    }

    private static string GetFirstEmoji(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        int i = 0;
        while (i < input.Length && char.IsWhiteSpace(input[i]))
            i++;
        int start = i;

        if (i >= input.Length) return "";

        // If the first character is a plain ASCII letter/digit, it's not an emoji
        int firstCp = char.ConvertToUtf32(input, i);
        if (firstCp < 0x200) return "";

        i += char.IsSurrogatePair(input, i) ? 2 : 1;

        while (i < input.Length)
        {
            int cp = char.ConvertToUtf32(input, i);
            int len = char.IsSurrogatePair(input, i) ? 2 : 1;

            if (cp == 0xFE0F || cp == 0xFE0E)
                i += len;
            else if (cp == 0x200D)
            {
                i += len;
                if (i < input.Length)
                    i += char.IsSurrogatePair(input, i) ? 2 : 1;
            }
            else if (cp >= 0x1F3FB && cp <= 0x1F3FF)
                i += len;
            else if (cp == 0x20E3)
                i += len;
            else if (cp >= 0x1F1E6 && cp <= 0x1F1FF)
                i += len;
            else
                break;
        }

        return input.Substring(start, i - start);
    }

    private static string EmojiToUrl(string emoji)
    {
        List<string> parts = new List<string>();
        for (int i = 0; i < emoji.Length;)
        {
            int cp = char.ConvertToUtf32(emoji, i);
            if (cp != 0xFE0F)
                parts.Add(cp.ToString("x"));
            i += char.IsSurrogatePair(emoji, i) ? 2 : 1;
        }
        return TWEMOJI_BASE + string.Join("-", parts) + ".png";
    }
}
