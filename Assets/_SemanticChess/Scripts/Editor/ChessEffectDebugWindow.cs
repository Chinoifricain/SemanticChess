using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ChessEffectDebugWindow : EditorWindow
{
    private ChessBoard _chessBoard;
    private int _selectedIndex = -1;
    private int _swapTargetIndex = -1;
    private EffectType _selectedEffect = EffectType.Stun;
    private int _duration = 3;
    private int _pushDirCol;
    private int _pushDirRow = -1;
    private int _pushDistance = 1;
    private bool _pickingSwapTarget;
    private Vector2 _scrollPos;

    // Tile effects
    private TileEffectType _selectedTileEffect = TileEffectType.Burning;
    private int _tileDuration = -1;
    private int _mode; // 0 = piece effects, 1 = tile effects

    private static readonly string[] PieceLetters = { "P", "N", "B", "R", "Q", "K" };
    private static readonly string[] EffectNames = System.Enum.GetNames(typeof(EffectType));
    private static readonly string[] TileEffectNames = System.Enum.GetNames(typeof(TileEffectType));
    private static readonly string[] ModeNames = { "Piece Effects", "Tile Effects" };

    // Tile effect letter indicators
    private static readonly Dictionary<TileEffectType, string> TileEffectLetters = new Dictionary<TileEffectType, string>
    {
        { TileEffectType.Burning, "B" },
        { TileEffectType.Occupied, "O" },
        { TileEffectType.Ice, "I" },
        { TileEffectType.Plant, "P" },
    };

    [MenuItem("Tools/Effect Debug")]
    public static void ShowWindow()
    {
        GetWindow<ChessEffectDebugWindow>("Effect Debug");
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play mode to use this window.", MessageType.Info);
            return;
        }

        if (_chessBoard == null)
            _chessBoard = Object.FindAnyObjectByType<ChessBoard>();

        if (_chessBoard == null)
        {
            EditorGUILayout.HelpBox("No ChessBoard found in scene.", MessageType.Warning);
            return;
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        // Mode toggle
        _mode = GUILayout.Toolbar(_mode, ModeNames);
        EditorGUILayout.Space(8);

        DrawBoardGrid();
        EditorGUILayout.Space(8);

        if (_mode == 0)
        {
            DrawPieceEffectControls();
        }
        else
        {
            DrawTileEffectControls();
        }

        EditorGUILayout.EndScrollView();

        if (Application.isPlaying)
            Repaint();
    }

    private void OnInspectorUpdate()
    {
        if (Application.isPlaying)
            Repaint();
    }

    // --- Board Grid ---

    private void DrawBoardGrid()
    {
        EditorGUILayout.LabelField(_pickingSwapTarget ? "Click swap target:" : "Select a tile:",
            EditorStyles.boldLabel);

        for (int row = 0; row < 8; row++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int col = 0; col < 8; col++)
            {
                int idx = row * 8 + col;
                ChessPiece piece = _chessBoard.GetPiece(idx);

                // Build label: piece + tile effect indicator
                string pieceLabel = piece != null
                    ? (piece.Color == PieceColor.White ? "w" : "b") + PieceLetters[(int)piece.PieceType]
                    : "--";

                string tileLabel = GetTileEffectIndicator(idx);
                string label = tileLabel.Length > 0 ? $"{pieceLabel}\n{tileLabel}" : pieceLabel;

                bool isSelected = idx == _selectedIndex;
                bool isSwapTarget = idx == _swapTargetIndex;

                GUIStyle style = new GUIStyle(EditorStyles.miniButton);
                style.fontSize = 9;
                style.alignment = TextAnchor.MiddleCenter;

                if (isSelected)
                {
                    style.normal.textColor = new Color(0.2f, 0.8f, 1f);
                    style.fontStyle = FontStyle.Bold;
                }
                else if (isSwapTarget)
                {
                    style.normal.textColor = new Color(1f, 0.9f, 0.2f);
                    style.fontStyle = FontStyle.Bold;
                }
                else if (tileLabel.Length > 0)
                {
                    style.normal.textColor = new Color(1f, 0.6f, 0.3f);
                }

                if (piece != null && piece.Effects.Count > 0)
                    style.fontStyle = FontStyle.BoldAndItalic;

                if (GUILayout.Button(label, style, GUILayout.Width(36), GUILayout.Height(32)))
                {
                    if (_pickingSwapTarget)
                    {
                        _swapTargetIndex = idx;
                        _pickingSwapTarget = false;
                    }
                    else
                    {
                        _selectedIndex = idx;
                        _swapTargetIndex = -1;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private string GetTileEffectIndicator(int index)
    {
        var effects = _chessBoard.GetTileEffects(index);
        if (effects.Count == 0) return "";
        string result = "";
        foreach (var e in effects)
        {
            if (TileEffectLetters.TryGetValue(e.Type, out string letter))
                result += letter;
        }
        return result;
    }

    // ========== PIECE EFFECTS ==========

    private void DrawPieceEffectControls()
    {
        DrawEffectSelector();
        EditorGUILayout.Space(4);
        DrawDuration();
        EditorGUILayout.Space(4);
        DrawConditionalParams();
        EditorGUILayout.Space(8);
        DrawApplyButton();
        EditorGUILayout.Space(8);
        DrawActivePieceEffects();
    }

    private void DrawEffectSelector()
    {
        EditorGUILayout.LabelField("Piece Effect:", EditorStyles.boldLabel);
        _selectedEffect = (EffectType)GUILayout.Toolbar((int)_selectedEffect, EffectNames);
    }

    private void DrawDuration()
    {
        EditorGUILayout.BeginHorizontal();
        _duration = EditorGUILayout.IntField("Duration", _duration);
        EditorGUILayout.LabelField("(-1 = permanent)", GUILayout.Width(110));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawConditionalParams()
    {
        if (_selectedEffect == EffectType.Push)
            DrawPushParams();
        else if (_selectedEffect == EffectType.Swap)
            DrawSwapParams();
    }

    private void DrawPushParams()
    {
        EditorGUILayout.LabelField("Push Direction:", EditorStyles.boldLabel);

        for (int dr = -1; dr <= 1; dr++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dc == 0 && dr == 0)
                {
                    GUILayout.Label("*", EditorStyles.centeredGreyMiniLabel,
                        GUILayout.Width(34), GUILayout.Height(24));
                }
                else
                {
                    bool active = _pushDirCol == dc && _pushDirRow == dr;
                    string arrow = GetArrow(dc, dr);
                    GUIStyle style = new GUIStyle(EditorStyles.miniButton);
                    if (active)
                    {
                        style.normal.textColor = new Color(0.2f, 0.8f, 1f);
                        style.fontStyle = FontStyle.Bold;
                    }

                    if (GUILayout.Button(arrow, style, GUILayout.Width(34), GUILayout.Height(24)))
                    {
                        _pushDirCol = dc;
                        _pushDirRow = dr;
                    }
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        _pushDistance = EditorGUILayout.IntField("Distance", _pushDistance);
        if (_pushDistance < 1) _pushDistance = 1;
    }

    private void DrawSwapParams()
    {
        EditorGUILayout.BeginHorizontal();
        string targetLabel = _swapTargetIndex >= 0 ? $"Tile {_swapTargetIndex}" : "None";
        EditorGUILayout.LabelField($"Swap target: {targetLabel}");
        if (GUILayout.Button("Pick", GUILayout.Width(50)))
            _pickingSwapTarget = true;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawApplyButton()
    {
        bool canApply = _selectedIndex >= 0 && _chessBoard.GetPiece(_selectedIndex) != null;
        GUI.enabled = canApply;
        if (GUILayout.Button("Apply Piece Effect", GUILayout.Height(28)))
            ApplyCurrentEffect();
        GUI.enabled = true;
    }

    private void DrawActivePieceEffects()
    {
        if (_selectedIndex < 0) return;
        ChessPiece piece = _chessBoard.GetPiece(_selectedIndex);
        if (piece == null) return;

        if (piece.Effects.Count == 0)
        {
            EditorGUILayout.LabelField("No active piece effects.", EditorStyles.miniLabel);
            return;
        }

        EditorGUILayout.LabelField("Active Piece Effects:", EditorStyles.boldLabel);
        ChessEffect toRemove = null;
        foreach (var effect in piece.Effects)
        {
            EditorGUILayout.BeginHorizontal();
            string durLabel = effect.Duration < 0 ? "perm" : $"{effect.Duration}t";
            EditorGUILayout.LabelField($"  {effect.Type} ({durLabel})");
            if (GUILayout.Button("X", GUILayout.Width(22)))
                toRemove = effect;
            EditorGUILayout.EndHorizontal();
        }
        if (toRemove != null)
            piece.RemoveEffect(toRemove);
    }

    private void ApplyCurrentEffect()
    {
        var effect = new ChessEffect(_selectedEffect, _duration);

        if (_selectedEffect == EffectType.Push)
        {
            effect.PushDirCol = _pushDirCol;
            effect.PushDirRow = _pushDirRow;
            effect.PushDistance = _pushDistance;
        }
        else if (_selectedEffect == EffectType.Swap)
        {
            effect.SwapTargetIndex = _swapTargetIndex;
        }

        _chessBoard.ApplyEffect(_selectedIndex, effect);
    }

    // ========== TILE EFFECTS ==========

    private void DrawTileEffectControls()
    {
        EditorGUILayout.LabelField("Tile Effect:", EditorStyles.boldLabel);
        _selectedTileEffect = (TileEffectType)GUILayout.Toolbar((int)_selectedTileEffect, TileEffectNames);

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        _tileDuration = EditorGUILayout.IntField("Duration", _tileDuration);
        EditorGUILayout.LabelField("(-1 = permanent)", GUILayout.Width(110));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        GUI.enabled = _selectedIndex >= 0;
        if (GUILayout.Button("Add Tile Effect", GUILayout.Height(28)))
        {
            _chessBoard.AddTileEffect(_selectedIndex, new TileEffect(_selectedTileEffect, _tileDuration));
        }
        GUI.enabled = true;

        EditorGUILayout.Space(8);

        DrawActiveTileEffects();
    }

    private void DrawActiveTileEffects()
    {
        if (_selectedIndex < 0) return;

        var effects = _chessBoard.GetTileEffects(_selectedIndex);
        if (effects.Count == 0)
        {
            EditorGUILayout.LabelField("No tile effects on this tile.", EditorStyles.miniLabel);
            return;
        }

        EditorGUILayout.LabelField("Active Tile Effects:", EditorStyles.boldLabel);
        TileEffect toRemove = null;
        foreach (var effect in effects)
        {
            EditorGUILayout.BeginHorizontal();
            string durLabel = effect.Duration < 0 ? "perm" : $"{effect.Duration}t";
            EditorGUILayout.LabelField($"  {effect.Type} ({durLabel})");
            if (GUILayout.Button("X", GUILayout.Width(22)))
                toRemove = effect;
            EditorGUILayout.EndHorizontal();
        }
        if (toRemove != null)
            _chessBoard.RemoveTileEffect(_selectedIndex, toRemove);
    }

    // --- Helpers ---

    private static string GetArrow(int dc, int dr)
    {
        if (dc == 0 && dr == -1) return "\u2191";
        if (dc == 0 && dr == 1) return "\u2193";
        if (dc == -1 && dr == 0) return "\u2190";
        if (dc == 1 && dr == 0) return "\u2192";
        if (dc == -1 && dr == -1) return "\u2196";
        if (dc == 1 && dr == -1) return "\u2197";
        if (dc == -1 && dr == 1) return "\u2199";
        if (dc == 1 && dr == 1) return "\u2198";
        return "?";
    }
}
