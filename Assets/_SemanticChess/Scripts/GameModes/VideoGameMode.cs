using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VideoGameMode : IGameMode
{
    public GameModeType ModeType => GameModeType.Video;

    private ChessBoard _board;
    private VideoUI _videoUI;
    private Camera _cam;
    private List<VideoScene> _scenes;
    private int _sceneIndex;
    private bool _loading;      // true = currently loading a scene (coroutine running)
    private Coroutine _loadCoroutine;
    private int _currentCamFocus = -1;  // tile the camera is currently zoomed to (-1 = default)

    // Playable scene state
    private float _playTimer;
    private bool _fading;

    public void SetVideoUI(VideoUI videoUI)
    {
        _videoUI = videoUI;
        _scenes = VideoScenario.GetAll();
        _sceneIndex = 0;
    }

    public void OnMatchStart(ChessBoard board)
    {
        _board = board;
        _cam = Camera.main;
        _loadCoroutine = GameManager.Instance.StartCoroutine(LoadScene(0));
    }

    public void OnMatchEnd(MatchResult result) { }

    public void OnTurnStart(PieceColor color)
    {
        // After White's move, turn switches to Black.
        // This signals the scene's action is complete.
        if (color == PieceColor.Black && !_loading)
        {
            var scene = _scenes[_sceneIndex];
            if (!scene.Playable)
            {
                if (scene.AutoAdvance)
                {
                    int next = _sceneIndex + 1;
                    if (next < _scenes.Count)
                        _loadCoroutine = GameManager.Instance.StartCoroutine(LoadScene(next));
                    else
                        GameManager.Instance.EndVideo();
                }
                else if (scene.AutoPlay)
                {
                    // Brief pause so the move result is visible, then advance
                    _loadCoroutine = GameManager.Instance.StartCoroutine(AutoPlayAdvance());
                }
                else
                {
                    _loadCoroutine = GameManager.Instance.StartCoroutine(AutoPlayAdvance());
                }
            }
        }
    }

    public void OnUpdate()
    {
        if (_board == null) return;

        var scene = _sceneIndex < _scenes.Count ? _scenes[_sceneIndex] : null;

        // Time scale — only manage when not loading (transitions run at 1x)
        if (!_loading && scene != null)
        {
            float baseScale = scene.TimeScale;
            if (_board.IsPlayingReaction && Input.GetMouseButton(0))
                Time.timeScale = Mathf.Max(baseScale, 4f);
            else
                Time.timeScale = baseScale;
        }

        // Playable scene timer (ticks even during animations)
        if (scene != null && scene.Playable && !_loading)
        {
            _playTimer -= Time.deltaTime;

            // Start fade 2 seconds before end of last scene
            if (!_fading && _sceneIndex == _scenes.Count - 1 && _playTimer <= 2f)
            {
                _fading = true;
                _videoUI.StartGradualFade(2f);
            }

            // Auto-advance when timer expires (wait for board to be idle)
            if (_playTimer <= 0f && !_board.IsMoving && !_board.IsPlayingReaction)
            {
                _board.DeselectPiece();
                int next = _sceneIndex + 1;
                if (next < _scenes.Count)
                    _loadCoroutine = GameManager.Instance.StartCoroutine(LoadScene(next));
                else
                    GameManager.Instance.EndVideo();
                return;
            }
        }

        // Don't process input while loading, mid-move, or mid-reaction
        if (_loading || _board.IsMoving || _board.IsPlayingReaction) return;

        // Escape → exit video mode
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GameManager.Instance.EndVideo();
            return;
        }

        // --- Playable scene input ---
        if (scene != null && scene.Playable)
        {
            if (!_board.IsGameOver)
                UpdateHover();

            if (!_board.IsGameOver && Input.GetMouseButtonDown(0))
            {
                Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
                int index = _board.WorldToTileIndex(world);
                if (index >= 0)
                    OnTileClicked(index);
            }
            return;
        }

    }

    public void OnDeactivate()
    {
        _loading = false;
        _fading = false;
        if (_loadCoroutine != null)
            GameManager.Instance.StopCoroutine(_loadCoroutine);
        if (_board != null)
        {
            _board.DeselectPiece();
            _board.SetHoveredIndex(-1);
            _board.SetHoveredTileIndex(-1);
        }
        Time.timeScale = 1f;
        _videoUI?.Destroy();
    }

    // --- Playable input (mirrors LocalGameMode) ---

    private void UpdateHover()
    {
        Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
        int index = _board.WorldToTileIndex(world);

        int newHovered = -1;
        if (index >= 0 && _board.GetPiece(index) != null)
            newHovered = index;

        _board.SetHoveredIndex(newHovered);
        _board.SetHoveredTileIndex(index);
    }

    private void OnTileClicked(int index)
    {
        int selected = _board.SelectedIndex;

        // Nothing selected — try to select
        if (selected == -1)
        {
            _board.SelectPiece(index);
            return;
        }

        // Click same piece — deselect
        if (index == selected)
        {
            _board.DeselectPiece();
            return;
        }

        // Click another friendly piece that isn't stunned — switch selection
        ChessPiece clickedPiece = _board.GetPiece(index);
        if (clickedPiece != null && clickedPiece.Color == _board.CurrentTurn
            && !clickedPiece.HasEffect(EffectType.Stun))
        {
            _board.DeselectPiece();
            _board.SelectPiece(index);
            return;
        }

        // Click a valid move destination — submit move
        var validMoves = _board.GetLegalMovesFor(selected);
        if (validMoves.Contains(index))
        {
            int from = selected;
            _board.DeselectPiece();
            _board.SubmitMove(new MoveRequest
            {
                FromIndex = from,
                ToIndex = index,
                Player = _board.CurrentTurn
            });
            return;
        }

        // Click elsewhere — deselect
        _board.DeselectPiece();
    }

    // --- Auto-play (simulated mouse) ---

    private IEnumerator RunAutoPlay()
    {
        yield return new WaitForSeconds(0.3f);

        int[] move = PickAutoPlayMove();
        if (move == null)
        {
            // No legal moves — wait briefly then advance
            yield return new WaitForSeconds(1f);
            AdvanceToNext();
            yield break;
        }

        int from = move[0];
        int to = move[1];
        var turn = _board.CurrentTurn;

        // Collect all friendly piece indices for scanning
        var friendlies = new List<int>();
        for (int i = 0; i < 64; i++)
        {
            var p = _board.GetPiece(i);
            if (p != null && p.Color == turn && i != from)
                friendlies.Add(i);
        }

        // Scan the board — hover over 2-4 random friendly pieces quickly
        int scanCount = Mathf.Min(Random.Range(2, 5), friendlies.Count);
        for (int s = 0; s < scanCount; s++)
        {
            int idx = friendlies[Random.Range(0, friendlies.Count)];
            _board.SetHoveredIndex(idx);
            _board.SetHoveredTileIndex(idx);
            yield return new WaitForSeconds(Random.Range(0.15f, 0.3f));
        }

        // Hover over the source piece (triggers element label pop)
        _board.SetHoveredIndex(from);
        _board.SetHoveredTileIndex(from);
        yield return new WaitForSeconds(0.4f);

        // Select the piece (jitter animation + movement indicators)
        _board.SelectPiece(from);
        yield return new WaitForSeconds(0.35f);

        // Scan legal moves — hover over 1-3 other destinations before the real one
        var legalMoves = _board.GetLegalMovesFor(from);
        var otherMoves = new List<int>();
        foreach (int m in legalMoves)
            if (m != to) otherMoves.Add(m);

        int peekCount = Mathf.Min(Random.Range(1, 4), otherMoves.Count);
        for (int s = 0; s < peekCount; s++)
        {
            int idx = otherMoves[Random.Range(0, otherMoves.Count)];
            var peekPiece = _board.GetPiece(idx);
            _board.SetHoveredIndex(peekPiece != null ? idx : -1);
            _board.SetHoveredTileIndex(idx);
            yield return new WaitForSeconds(Random.Range(0.15f, 0.3f));
        }

        // Hover over the target tile
        var targetPiece = _board.GetPiece(to);
        _board.SetHoveredIndex(targetPiece != null ? to : -1);
        _board.SetHoveredTileIndex(to);
        yield return new WaitForSeconds(0.3f);

        // Submit the move
        _board.DeselectPiece();
        _board.SetHoveredIndex(-1);
        _board.SetHoveredTileIndex(-1);
        _board.SubmitMove(new MoveRequest
        {
            FromIndex = from,
            ToIndex = to,
            Player = _board.CurrentTurn
        });

        // OnTurnStart(Black) will handle advancing
    }

    private int[] PickAutoPlayMove()
    {
        var turn = _board.CurrentTurn;
        var captures = new List<int[]>();
        var nonCaptures = new List<int[]>();

        for (int i = 0; i < 64; i++)
        {
            var piece = _board.GetPiece(i);
            if (piece == null || piece.Color != turn) continue;
            if (piece.HasEffect(EffectType.Stun)) continue;

            var moves = _board.GetLegalMovesFor(i);
            foreach (int target in moves)
            {
                var targetPiece = _board.GetPiece(target);
                if (targetPiece != null && targetPiece.Color != turn)
                    captures.Add(new[] { i, target });
                else
                    nonCaptures.Add(new[] { i, target });
            }
        }

        if (captures.Count > 0)
            return captures[Random.Range(0, captures.Count)];
        if (nonCaptures.Count > 0)
            return nonCaptures[Random.Range(0, nonCaptures.Count)];
        return null;
    }

    private IEnumerator AutoPlayAdvance()
    {
        yield return new WaitForSeconds(0.8f);
        AdvanceToNext();
    }

    private void AdvanceToNext()
    {
        int next = _sceneIndex + 1;
        if (next < _scenes.Count)
            _loadCoroutine = GameManager.Instance.StartCoroutine(LoadScene(next));
        else
            GameManager.Instance.EndVideo();
    }

    // --- Coroutines ---

    private IEnumerator FadeAndExit()
    {
        yield return _videoUI.FadeToBlack();
        yield return new WaitForSeconds(2f);
        GameManager.Instance.EndVideo();
    }

    private IEnumerator LoadScene(int index)
    {
        _loading = true;
        _sceneIndex = index;

        var scene = _scenes[index];

        // Reset timeScale to 1 for transitions
        Time.timeScale = 1f;

        // Check if caption/title stays the same between scenes
        var prevScene = index > 0 ? _scenes[index - 1] : null;
        bool sameCaption = prevScene != null && !scene.ShowTitle && !prevScene.ShowTitle
            && scene.Caption != null && scene.Caption == prevScene.Caption;
        bool sameTitle = prevScene != null && scene.ShowTitle && prevScene.ShowTitle;
        bool titleToNonTitle = prevScene != null && prevScene.ShowTitle && !scene.ShowTitle;

        // Snap dolly back to origin before anything changes
        _videoUI.SnapDolly();

        // Hide previous caption/title (skip if unchanged)
        if (!sameCaption && !sameTitle)
        {
            if (!titleToNonTitle)
            {
                _videoUI.HideCaption();
                _videoUI.HideTitle();
            }
        }

        // Transition
        bool needsZoomOut = _currentCamFocus >= 0 && _currentCamFocus != scene.CameraFocus;
        if (titleToNonTitle)
        {
            _videoUI.HideTitle();
            yield return _videoUI.FadeToBlack(0.8f);
            _videoUI.ResetCamera();
            _currentCamFocus = -1;
        }
        else if (index == 0)
            yield return new WaitForSeconds(0.3f);
        else if (needsZoomOut)
            yield return _videoUI.ZoomOut();
        else
            yield return new WaitForSeconds(0.3f);

        // Setup board
        if (scene.Pieces == null)
        {
            _board.ResetBoard(null);
        }
        else
        {
            _board.ClearBoard();
            foreach (var p in scene.Pieces)
                _board.SpawnPieceWithElement(p.Index, p.Type, p.Color, p.Element, p.Emoji);
        }

        // Pre-apply stun for Cleanse demo
        if (scene.PreStunTarget >= 0)
        {
            var piece = _board.GetPiece(scene.PreStunTarget);
            if (piece != null)
                piece.AddEffect(new ChessEffect(EffectType.Stun, 99));
        }

        // Show caption or title (skip if unchanged)
        if (!sameCaption && !sameTitle)
        {
            if (scene.ShowTitle)
                _videoUI.ShowTitle();
            else
                _videoUI.ShowCaption(scene.Caption);
        }

        // Fade back from black after title-to-non-title transition
        if (titleToNonTitle)
        {
            yield return new WaitForSeconds(0.3f);
            yield return _videoUI.FadeFromBlack(0.8f);
        }
        else
            yield return new WaitForSeconds(0.5f);

        // Camera: pan across row or zoom to tile
        if (scene.CameraPanRow >= 0)
        {
            yield return _videoUI.PanAcrossRow(_board, scene.CameraPanRow, scene.PanDuration);
            _currentCamFocus = scene.CameraPanRow * 8 + 7; // track end tile for zoom-out
        }
        else
        {
            if (scene.CameraFocus >= 0 && scene.CameraFocus != _currentCamFocus)
                yield return _videoUI.ZoomToTile(_board, scene.CameraFocus);
            _currentCamFocus = scene.CameraFocus;
        }

        // Start subtle camera dolly (runs in realtime, killed on next scene)
        _videoUI.StartDolly(scene.DollyX, scene.DollyY);

        _loading = false;

        // --- Capture scene ---
        if (scene.AutoMoveFrom >= 0 && scene.AutoMoveTo >= 0)
        {
            float delay = scene.PreMoveDelay > 0 ? scene.PreMoveDelay : 1.5f;
            yield return new WaitForSeconds(delay);

            if (scene.ForcedMix != null)
                _board.SetPendingReaction(scene.ForcedMix, scene.ForcedReaction);

            _board.SubmitMove(new MoveRequest
            {
                FromIndex = scene.AutoMoveFrom,
                ToIndex = scene.AutoMoveTo,
                Player = PieceColor.White
            });

            // OnTurnStart(Black) will auto-advance
        }
        // --- Direct effect scene (showcase) ---
        else if (scene.EffectCenter >= 0 && scene.ForcedReaction != null)
        {
            yield return new WaitForSeconds(1.0f);

            yield return _board.PlayReaction(
                scene.EffectCenter,
                scene.ForcedReaction,
                PieceColor.White,
                "won"
            );

            yield return new WaitForSeconds(0.3f);
            _loadCoroutine = GameManager.Instance.StartCoroutine(AutoPlayAdvance());
        }
        // --- Playable scene ---
        else if (scene.Playable)
        {
            _playTimer = scene.PlayDuration;
            // Player interaction handled in OnUpdate
        }
        // --- Auto-play scene (simulated mouse) ---
        else if (scene.AutoPlay)
        {
            if (_sceneIndex == _scenes.Count - 1)
            {
                _fading = true;
                _videoUI.StartGradualFade(4f);
            }
            GameManager.Instance.StartCoroutine(RunAutoPlay());
        }
        // --- Static scene (no action) ---
        else
        {
            yield return new WaitForSeconds(1.5f);
            AdvanceToNext();
        }
    }
}
