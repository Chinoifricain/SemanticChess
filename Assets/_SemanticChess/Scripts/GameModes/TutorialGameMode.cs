using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialGameMode : IGameMode
{
    public GameModeType ModeType => GameModeType.Tutorial;

    private ChessBoard _board;
    private Camera _cam;
    private TutorialManager _tutoUI;
    private List<TutorialScenario> _scenarios;
    private int _scenarioIndex;
    private int _guidedFrom;
    private int _guidedTo;
    private bool _waitingForInput;
    private bool _advancing;

    public void SetTutorialUI(TutorialManager tutoUI)
    {
        _tutoUI = tutoUI;
        _scenarios = TutorialScenario.GetAll();
        _scenarioIndex = 0;
    }

    public void OnMatchStart(ChessBoard board)
    {
        _board = board;
        _cam = Camera.main;
        GameManager.Instance.StartCoroutine(LoadScenario(0));
    }

    public void OnMatchEnd(MatchResult result) { }

    public void OnTurnStart(PieceColor color)
    {
        // After White's guided capture, turn switches to Black.
        // Use this as the signal to advance to the next scenario.
        if (color == PieceColor.Black && !_advancing)
        {
            _advancing = true;
            _waitingForInput = false;
            GameManager.Instance.StartCoroutine(AdvanceScenario());
        }
    }

    public void OnUpdate()
    {
        if (_board == null || !_waitingForInput) return;

        // Hold click to fast-forward during reaction effects
        if (_board.IsPlayingReaction && Input.GetMouseButton(0))
            Time.timeScale = 4f;
        else if (Time.timeScale != 1f)
            Time.timeScale = 1f;

        if (!_board.IsMoving && !_board.IsGameOver)
            UpdateHover();

        if (_board.IsMoving || _board.IsGameOver) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
        int index = _board.WorldToTileIndex(world);
        if (index >= 0)
            OnTileClicked(index);
    }

    public void OnDeactivate()
    {
        _waitingForInput = false;
        if (_board != null)
        {
            _board.DeselectPiece();
            _board.SetHoveredIndex(-1);
            _board.SetHoveredTileIndex(-1);
        }
        Time.timeScale = 1f;
        _tutoUI?.Destroy();
    }

    private IEnumerator LoadScenario(int index)
    {
        _scenarioIndex = index;
        _advancing = false;
        _waitingForInput = false;

        var scenario = _scenarios[index];
        _guidedFrom = scenario.GuidedFrom;
        _guidedTo = scenario.GuidedTo;

        // Clear board and spawn custom pieces
        _board.ClearBoard();
        foreach (var p in scenario.Pieces)
            _board.SpawnPieceWithElement(p.Index, p.Type, p.Color, p.Element, p.Emoji);

        GameManager.Instance.GameUI.UpdateTurn(PieceColor.White);

        // Show intro card, then dock it to the left so the player can still read it
        yield return GameManager.Instance.StartCoroutine(
            _tutoUI.ShowCardAndDock(scenario.IntroTitle, scenario.IntroBody));

        // Pre-select the guided piece so indicators are visible
        _board.SelectPiece(_guidedFrom);
        _board.PulseMoveIndicator(_guidedTo);
        _waitingForInput = true;
    }

    private IEnumerator AdvanceScenario()
    {
        var scenario = _scenarios[_scenarioIndex];

        // Zoom to capture tile, show post-capture card, zoom out
        yield return GameManager.Instance.StartCoroutine(
            _tutoUI.ZoomToTile(_board, _guidedTo));
        yield return GameManager.Instance.StartCoroutine(
            _tutoUI.ShowCard(scenario.PostTitle, scenario.PostBody));
        yield return GameManager.Instance.StartCoroutine(
            _tutoUI.ZoomOut());

        int next = _scenarioIndex + 1;
        if (next < _scenarios.Count)
        {
            yield return new WaitForSeconds(0.3f);
            yield return GameManager.Instance.StartCoroutine(LoadScenario(next));
        }
        else
        {
            // Tutorial complete — show final card and return to menu
            yield return GameManager.Instance.StartCoroutine(
                _tutoUI.ShowCard("Tutorial Complete!",
                    "You now know the basics: win or lose the element clash, plan around matchups, and watch how your elements evolve.\n\nGood luck!"));
            GameManager.Instance.EndTutorial();
        }
    }

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

        // If nothing selected, only allow selecting the guided piece
        if (selected == -1)
        {
            if (index == _guidedFrom)
                _board.SelectPiece(index);
            return;
        }

        // Click same piece — deselect
        if (index == selected)
        {
            _board.DeselectPiece();
            return;
        }

        // Click the guided target — submit the move
        if (selected == _guidedFrom && index == _guidedTo)
        {
            _board.DeselectPiece();

            // Inject forced reaction data so HandleCapture skips the API
            var scenario = _scenarios[_scenarioIndex];
            if (scenario.ForcedMix != null)
                _board.SetPendingReaction(scenario.ForcedMix, scenario.ForcedReaction);

            _board.SubmitMove(new MoveRequest
            {
                FromIndex = _guidedFrom,
                ToIndex = _guidedTo,
                Player = PieceColor.White
            });
            _waitingForInput = false;
            return;
        }

        // Any other click — re-select guided piece if they clicked elsewhere
        if (index != _guidedFrom)
        {
            _board.DeselectPiece();
            _board.SelectPiece(_guidedFrom);
        }
    }
}
