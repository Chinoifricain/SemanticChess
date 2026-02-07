using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ElementEntry
{
    public string name;
    public string emoji;
    public string parentA;
    public string parentB;
}

[Serializable]
public class ElementCollectionData
{
    public List<ElementEntry> elements = new List<ElementEntry>();
}

public static class ElementCollection
{
    private const string PREFS_KEY = "element_collection";
    private const string PLAYED_KEY = "has_played_before";

    private static ElementCollectionData _data;
    private static Dictionary<string, ElementEntry> _lookup;

    private static readonly (string name, string emoji)[] BaseElements =
    {
        ("Fire",  "\U0001F525"),
        ("Water", "\U0001F4A7"),
        ("Plant", "\U0001F33F"),
        ("Air",   "\U0001F4A8"),
    };

    public static bool HasPlayedBefore
    {
        get => PlayerPrefs.GetInt(PLAYED_KEY, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(PLAYED_KEY, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void Load()
    {
        if (PlayerPrefs.HasKey(PREFS_KEY))
        {
            string json = PlayerPrefs.GetString(PREFS_KEY);
            _data = JsonUtility.FromJson<ElementCollectionData>(json);
            if (_data == null) _data = new ElementCollectionData();
        }
        else
        {
            _data = new ElementCollectionData();
        }

        // Seed base elements if missing
        foreach (var (name, emoji) in BaseElements)
        {
            bool found = false;
            for (int i = 0; i < _data.elements.Count; i++)
            {
                if (string.Equals(_data.elements[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                _data.elements.Add(new ElementEntry { name = name, emoji = emoji, parentA = "", parentB = "" });
            }
        }

        RebuildLookup();
    }

    public static void Save()
    {
        if (_data == null) return;
        string json = JsonUtility.ToJson(_data);
        PlayerPrefs.SetString(PREFS_KEY, json);
        PlayerPrefs.Save();
    }

    public static bool AddElement(string name, string emoji, string parentA, string parentB)
    {
        EnsureLoaded();
        if (_lookup.ContainsKey(name.ToLowerInvariant())) return false;

        var entry = new ElementEntry
        {
            name = name,
            emoji = emoji,
            parentA = parentA ?? "",
            parentB = parentB ?? ""
        };
        _data.elements.Add(entry);
        _lookup[name.ToLowerInvariant()] = entry;
        return true;
    }

    public static bool HasElement(string name)
    {
        EnsureLoaded();
        return _lookup.ContainsKey(name.ToLowerInvariant());
    }

    public static ElementEntry GetElement(string name)
    {
        EnsureLoaded();
        _lookup.TryGetValue(name.ToLowerInvariant(), out var entry);
        return entry;
    }

    public static List<ElementEntry> GetAll()
    {
        EnsureLoaded();
        return _data.elements;
    }

    public static List<ElementEntry> GetRoots()
    {
        EnsureLoaded();
        var roots = new List<ElementEntry>();
        foreach (var e in _data.elements)
        {
            if (string.IsNullOrEmpty(e.parentA) && string.IsNullOrEmpty(e.parentB))
                roots.Add(e);
        }
        return roots;
    }

    public static List<ElementEntry> GetChildrenOf(string parentName)
    {
        EnsureLoaded();
        var children = new List<ElementEntry>();
        string lower = parentName.ToLowerInvariant();
        foreach (var e in _data.elements)
        {
            if (string.IsNullOrEmpty(e.parentA) && string.IsNullOrEmpty(e.parentB))
                continue; // skip base elements
            if (e.parentA.ToLowerInvariant() == lower || e.parentB.ToLowerInvariant() == lower)
                children.Add(e);
        }
        return children;
    }

    public static List<ElementEntry> Search(string query)
    {
        EnsureLoaded();
        var results = new List<ElementEntry>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        string lower = query.ToLowerInvariant();
        foreach (var e in _data.elements)
        {
            if (e.name.ToLowerInvariant().Contains(lower))
                results.Add(e);
        }
        return results;
    }

    private static void RebuildLookup()
    {
        _lookup = new Dictionary<string, ElementEntry>();
        foreach (var e in _data.elements)
            _lookup[e.name.ToLowerInvariant()] = e;
    }

    private static void EnsureLoaded()
    {
        if (_data == null) Load();
    }
}
