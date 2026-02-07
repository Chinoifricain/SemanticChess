using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// ─── Data Structures ────────────────────────────────────────────────────────

[Serializable]
public class ElementMixResult
{
    public string newElement;
    public string emoji;
    public string winningElement;
    public string reasoning;
}

[Serializable]
public class ReactionEffectEntry
{
    public string pattern;       // "+", "x", "*", "forward", "L", "ring", "area"
    public int distance;         // how far the pattern extends from capture square
    public bool obstructed;      // if true, rays stop at first piece (line of sight)
    public string target;        // "enemies", "friendlies", "all_pieces", "empty", "all"
    public string effect;        // Damage, Stun, Shield, Push, Convert, Poison, Transform, Burning, Ice, Plant
    public string direction;     // Push only: "outwards", "inwards", "clockwise", "counter_clockwise", "up", "down", "left", "right"
    public int push_distance;    // Push only: how far to push
    public string piece_type;    // Transform only: "Pawn", "Knight", "Bishop", "Rook", "Queen"
    public int duration;
}

[Serializable]
public class ElementReactionResult
{
    public ReactionEffectEntry[] effects;
    public string flavor;
}

public struct ReactionContext
{
    public string CaptureSquare;
    public string PieceType;
    public string PieceColor;
    public int CombinedPower;
    public string PowerTier;
    public string AttackerElement;
    public string DefenderElement;
    public int NearbyEnemies;
    public int NearbyFriendlies;
}

// ─── Service ────────────────────────────────────────────────────────────────

public class ElementService : MonoBehaviour
{
    private const string API_URL = "https://api-relay.raphael-tan-fr.workers.dev/gemini";
    private const string API_TOKEN = "mrMPhQ9eazu1YYD";
    private const string MODEL = "gemini-2.0-flash";

    private readonly Dictionary<string, ElementMixResult> _cache = new();

    // ─── Public API ─────────────────────────────────────────────────────

    public bool HasCachedMix(string a, string b) => _cache.ContainsKey(GetCacheKey(a, b));

    /// <summary>Returns cached or freshly-fetched element mix (no reaction).</summary>
    public IEnumerator GetElementMix(string atkElem, string defElem, Action<ElementMixResult> callback)
    {
        string key = GetCacheKey(atkElem, defElem);
        if (_cache.TryGetValue(key, out var cached)) { callback?.Invoke(cached); yield break; }

        string response = null;
        yield return SendRequest(ElementPrompts.BuildMixPrompt(atkElem, defElem), r => response = r);

        if (response == null) { callback?.Invoke(null); yield break; }

        var result = ParseMixResponse(response);
        if (result != null) { _cache[key] = result; callback?.Invoke(result); }
        else { Debug.LogError($"[ElementService] Failed to parse mix: {response}"); callback?.Invoke(null); }
    }

    /// <summary>Single API call that returns both element mix AND reaction (used when mix is not cached).</summary>
    public IEnumerator GetElementMixAndReaction(
        string atkElem, string defElem, ReactionContext ctx,
        Action<ElementMixResult, ElementReactionResult> callback)
    {
        string response = null;
        yield return SendRequest(ElementPrompts.BuildCombinedPrompt(atkElem, defElem, ctx), r => response = r);

        if (response == null) { callback?.Invoke(null, null); yield break; }

        var (mix, reaction) = ParseCombinedResponse(response);
        if (mix != null) _cache[GetCacheKey(atkElem, defElem)] = mix;
        else Debug.LogError($"[ElementService] Failed to parse combined mix: {response}");

        callback?.Invoke(mix, reaction);
    }

    /// <summary>Reaction-only API call (used when mix was already cached).</summary>
    public IEnumerator GetElementReaction(
        ElementMixResult mix, ReactionContext ctx,
        Action<ElementReactionResult> callback)
    {
        string response = null;
        yield return SendRequest(ElementPrompts.BuildReactionPrompt(mix.newElement, mix.emoji, ctx), r => response = r);

        if (response == null) { callback?.Invoke(null); yield break; }

        var result = ParseReactionResponse(response);
        if (result != null) callback?.Invoke(result);
        else { Debug.LogError($"[ElementService] Failed to parse reaction: {response}"); callback?.Invoke(null); }
    }

    public void ClearCache() => _cache.Clear();

    // ─── HTTP Request ───────────────────────────────────────────────────

    private static string BuildRequestJson(string prompt)
    {
        string escaped = prompt.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        return
$@"{{""model"":""{MODEL}"",""payload"":{{""contents"":[{{""parts"":[{{""text"":""{escaped}""}}]}}],""generationConfig"":{{""thinkingConfig"":{{""thinkingBudget"":0}}}}}}}}";
    }

    private IEnumerator SendRequest(string prompt, Action<string> onResponse)
    {
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
            onResponse?.Invoke(null);
            yield break;
        }

        onResponse?.Invoke(www.downloadHandler.text);
    }

    // ─── Response Parsers ───────────────────────────────────────────────

    private static ElementMixResult ParseMixResponse(string raw)
    {
        try
        {
            string text = ExtractTextField(raw);
            if (text == null) return null;
            string json = ExtractJson(text);
            if (json == null) return null;

            var result = JsonUtility.FromJson<ElementMixResult>(json);
            if (string.IsNullOrEmpty(result.newElement) || string.IsNullOrEmpty(result.winningElement))
                return null;
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ElementService] Mix parse error: {e.Message}");
            return null;
        }
    }

    private static ElementReactionResult ParseReactionResponse(string raw)
    {
        try
        {
            string text = ExtractTextField(raw);
            if (text == null) return null;
            string json = ExtractJson(text);
            if (json == null) return null;

            return new ElementReactionResult
            {
                flavor = ExtractStringField(json, "flavor"),
                effects = ParseEffectsArray(json)
            };
        }
        catch (Exception e)
        {
            Debug.LogError($"[ElementService] Reaction parse error: {e.Message}");
            return null;
        }
    }

    private static (ElementMixResult, ElementReactionResult) ParseCombinedResponse(string raw)
    {
        try
        {
            string text = ExtractTextField(raw);
            if (text == null) return (null, null);
            string json = ExtractJson(text);
            if (json == null) return (null, null);

            // Parse "mix" sub-object
            string mixJson = ExtractSubObject(json, "mix");
            ElementMixResult mix = null;
            if (mixJson != null)
            {
                mix = JsonUtility.FromJson<ElementMixResult>(mixJson);
                if (string.IsNullOrEmpty(mix?.newElement)) mix = null;
            }

            // Parse "reaction" sub-object
            string reactionJson = ExtractSubObject(json, "reaction");
            ElementReactionResult reaction = null;
            if (reactionJson != null)
            {
                reaction = new ElementReactionResult
                {
                    flavor = ExtractStringField(reactionJson, "flavor"),
                    effects = ParseEffectsArray(reactionJson)
                };
            }

            return (mix, reaction);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ElementService] Combined parse error: {e.Message}");
            return (null, null);
        }
    }

    // ─── JSON Helpers ───────────────────────────────────────────────────

    private static string GetCacheKey(string a, string b)
    {
        return string.Compare(a, b, StringComparison.Ordinal) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
    }

    private static string ExtractTextField(string raw)
    {
        const string marker = "\"text\":\"";
        int start = raw.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;
        start += marker.Length;

        var sb = new StringBuilder();
        for (int i = start; i < raw.Length; i++)
        {
            if (raw[i] == '\\' && i + 1 < raw.Length)
            {
                char next = raw[i + 1];
                switch (next)
                {
                    case '"':  sb.Append('"'); i++; break;
                    case '\\': sb.Append('\\'); i++; break;
                    case 'n':  sb.Append('\n'); i++; break;
                    case 'r':  i++; break;
                    case 't':  sb.Append('\t'); i++; break;
                    default:   sb.Append(raw[i]); break;
                }
            }
            else if (raw[i] == '"') break;
            else sb.Append(raw[i]);
        }

        return sb.ToString();
    }

    private static string ExtractJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json")) text = text[7..];
        else if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        text = text.Trim();

        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        return (start >= 0 && end > start) ? text[start..(end + 1)] : null;
    }

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
        var entries = new List<ReactionEffectEntry>();
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
            duration = ExtractIntField(json, "duration", 0)
        };
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
}
