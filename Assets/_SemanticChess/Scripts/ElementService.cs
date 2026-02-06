using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class ElementMixResult
{
    public string newElement;
    public string emoji;          // one or more emoji representing the new element
    public string winningElement; // element name that wins, or "draw"
    public string reasoning;
}

public class ElementService : MonoBehaviour
{
    private const string API_URL = "https://api-relay.raphael-tan-fr.workers.dev/gemini";
    private const string API_TOKEN = "mrMPhQ9eazu1YYD";
    private const string MODEL = "gemini-2.0-flash-lite";

    // Cache by sorted pair "A|B" -> result
    private readonly Dictionary<string, ElementMixResult> _cache = new();

    public IEnumerator GetElementMix(string attackerElement, string defenderElement, Action<ElementMixResult> callback)
    {
        string cacheKey = GetCacheKey(attackerElement, defenderElement);

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            callback?.Invoke(cached);
            yield break;
        }

        string prompt = BuildPrompt(attackerElement, defenderElement);
        string json = BuildRequestJson(prompt);
        byte[] body = Encoding.UTF8.GetBytes(json);

        using var www = new UnityWebRequest(API_URL, "POST");
        www.uploadHandler = new UploadHandlerRaw(body);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Authorization", $"Bearer {API_TOKEN}");
        www.timeout = 15;

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[ElementService] API error: {www.error}");
            callback?.Invoke(null);
            yield break;
        }

        string response = www.downloadHandler.text;
        ElementMixResult result = ParseResponse(response);

        if (result != null)
        {
            _cache[cacheKey] = result;
            callback?.Invoke(result);
        }
        else
        {
            Debug.LogError($"[ElementService] Failed to parse: {response}");
            callback?.Invoke(null);
        }
    }

    public void ClearCache() => _cache.Clear();

    private static string GetCacheKey(string a, string b)
    {
        return string.Compare(a, b, StringComparison.Ordinal) <= 0
            ? $"{a}|{b}"
            : $"{b}|{a}";
    }

    private static string BuildPrompt(string attacker, string defender)
    {
        return
$@"Two chess piece elements collide: ""{attacker}"" captures ""{defender}"".

1. What new element is created from mixing these two? (1-2 words max, creative but thematic)
2. Pick 1-2 emoji that best represent the new element. Be creative and use the full Unicode emoji range.
3. Which element wins semantically? (classic: Water beats Fire, Fire beats Plant, Plant beats Water — but be creative with evolved/exotic elements)

Return ONLY valid JSON, no markdown:
{{""newElement"":""name"",""emoji"":""one or two emoji"",""winningElement"":""{attacker}"" or ""{defender}"" or ""draw"",""reasoning"":""brief""}}";
    }

    private static string BuildRequestJson(string prompt)
    {
        // Manual JSON build — Unity's JsonUtility can't serialize anonymous/nested properly
        string escaped = prompt.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        return
$@"{{""model"":""{MODEL}"",""payload"":{{""contents"":[{{""parts"":[{{""text"":""{escaped}""}}]}}],""generationConfig"":{{""thinkingConfig"":{{""thinkingBudget"":0}}}}}}}}";
    }

    private static ElementMixResult ParseResponse(string raw)
    {
        try
        {
            // Extract the text field from Gemini response
            // Response shape: { "candidates": [{ "content": { "parts": [{ "text": "..." }] } }] }
            string text = ExtractTextField(raw);
            if (text == null) return null;

            // Extract JSON object from text (may have markdown fences)
            string json = ExtractJson(text);
            if (json == null) return null;

            var result = JsonUtility.FromJson<ElementMixResult>(json);

            if (string.IsNullOrEmpty(result.newElement) || string.IsNullOrEmpty(result.winningElement))
                return null;

            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ElementService] Parse error: {e.Message}");
            return null;
        }
    }

    private static string ExtractTextField(string raw)
    {
        // Find "text":" in the response and extract the value
        const string marker = "\"text\":\"";
        int start = raw.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;
        start += marker.Length;

        // Walk forward to find the closing quote (handling escaped quotes)
        var sb = new StringBuilder();
        for (int i = start; i < raw.Length; i++)
        {
            if (raw[i] == '\\' && i + 1 < raw.Length)
            {
                char next = raw[i + 1];
                switch (next)
                {
                    case '"': sb.Append('"'); i++; break;
                    case '\\': sb.Append('\\'); i++; break;
                    case 'n': sb.Append('\n'); i++; break;
                    case 'r': i++; break;
                    case 't': sb.Append('\t'); i++; break;
                    default: sb.Append(raw[i]); break;
                }
            }
            else if (raw[i] == '"')
            {
                break;
            }
            else
            {
                sb.Append(raw[i]);
            }
        }

        return sb.ToString();
    }

    private static string ExtractJson(string text)
    {
        text = text.Trim();

        // Strip markdown code fences
        if (text.StartsWith("```json")) text = text[7..];
        else if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        text = text.Trim();

        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];

        return null;
    }
}
