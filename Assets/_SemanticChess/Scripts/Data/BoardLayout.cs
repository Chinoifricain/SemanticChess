using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PieceSlotConfig
{
    public int index;
    public string element;
    public string emoji;
}

[Serializable]
public class BoardLayoutData
{
    public List<PieceSlotConfig> whiteSlots = new List<PieceSlotConfig>();
    public List<PieceSlotConfig> blackSlots = new List<PieceSlotConfig>();
}

public static class BoardLayout
{
    private const string PREFS_KEY = "board_layout";

    private static BoardLayoutData _data;

    // Default element arrays (must match ChessBoard)
    private static readonly string[] BackRankElements = { "Water", "Fire", "Plant", "Air", "Water", "Plant", "Fire", "Air" };
    private static readonly string[] PawnElements     = { "Fire", "Plant", "Water", "Air", "Fire", "Plant", "Water", "Air" };

    private static readonly Dictionary<string, string> BaseEmojis = new()
    {
        { "Fire",  "\U0001F525" },
        { "Water", "\U0001F4A7" },
        { "Plant", "\U0001F33F" },
        { "Air",   "\U0001F4A8" },
    };

    public static void Load()
    {
        if (PlayerPrefs.HasKey(PREFS_KEY))
        {
            string json = PlayerPrefs.GetString(PREFS_KEY);
            _data = JsonUtility.FromJson<BoardLayoutData>(json);
            if (_data == null) _data = new BoardLayoutData();
        }
        else
        {
            _data = new BoardLayoutData();
        }
    }

    public static void Save()
    {
        if (_data == null) return;
        string json = JsonUtility.ToJson(_data);
        PlayerPrefs.SetString(PREFS_KEY, json);
        PlayerPrefs.Save();
    }

    public static BoardLayoutData GetLayout()
    {
        EnsureLoaded();
        return _data;
    }

    public static void SetSlot(PieceColor color, int index, string element, string emoji)
    {
        EnsureLoaded();
        var slots = color == PieceColor.White ? _data.whiteSlots : _data.blackSlots;

        // Check if this is actually the default — if so, remove instead of storing
        string defaultElement = GetDefaultElement(color, index);
        if (element == defaultElement)
        {
            slots.RemoveAll(s => s.index == index);
            return;
        }

        // Update existing or add new
        var existing = slots.Find(s => s.index == index);
        if (existing != null)
        {
            existing.element = element;
            existing.emoji = emoji;
        }
        else
        {
            slots.Add(new PieceSlotConfig { index = index, element = element, emoji = emoji });
        }
    }

    public static (string element, string emoji) GetSlotElement(PieceColor color, int index)
    {
        EnsureLoaded();
        var slots = color == PieceColor.White ? _data.whiteSlots : _data.blackSlots;
        var custom = slots.Find(s => s.index == index);
        if (custom != null)
            return (custom.element, custom.emoji);

        string element = GetDefaultElement(color, index);
        string emoji = BaseEmojis.TryGetValue(element, out string e) ? e : "";
        return (element, emoji);
    }

    public static void ResetToDefaults()
    {
        EnsureLoaded();
        _data.whiteSlots.Clear();
        _data.blackSlots.Clear();
    }

    public static List<PieceSlotConfig> GetSlotsForColor(PieceColor color)
    {
        EnsureLoaded();
        return color == PieceColor.White ? _data.whiteSlots : _data.blackSlots;
    }

    private static string GetDefaultElement(PieceColor color, int index)
    {
        int col = index % 8;
        int row = index / 8;
        bool isPawn = (color == PieceColor.White) ? row == 6 : row == 1;
        return isPawn ? PawnElements[col] : BackRankElements[col];
    }

    private static void EnsureLoaded()
    {
        if (_data == null) Load();
    }
}
