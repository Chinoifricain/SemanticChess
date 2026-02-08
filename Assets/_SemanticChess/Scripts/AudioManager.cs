using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ─── Master Volume ───────────────────────────────────────

    [Header("Master Volume")]

    [Tooltip("Global volume multiplier applied to the AudioListener. Controls the overall loudness of all sounds.")]
    [Range(0f, 1f)] [SerializeField] private float _masterVolume = 1f;

    // ─── Music ────────────────────────────────────────────────

    [Header("Music")]

    [Tooltip("Background music clip. Loops forever from startup.")]
    [SerializeField] private AudioClip _musicClip;

    [Range(0f, 1f)] [SerializeField] private float _musicVolume = 0.3f;

    // ─── UI Sounds ───────────────────────────────────────────

    [Header("UI Sounds")]

    [Tooltip("Button hover/rollover. Plays on pointer enter for all JuicyButtons.")]
    [SerializeField] private AudioClip _buttonHover;
    [Range(0f, 1f)] [SerializeField] private float _buttonHoverVolume = 0.5f;

    [Tooltip("Button press. Plays on pointer down for all JuicyButtons.")]
    [SerializeField] private AudioClip _buttonClick;
    [Range(0f, 1f)] [SerializeField] private float _buttonClickVolume = 0.6f;

    [Tooltip("Cancel/back action. Call PlayCancel() explicitly for back/cancel buttons.")]
    [SerializeField] private AudioClip _buttonCancel;
    [Range(0f, 1f)] [SerializeField] private float _buttonCancelVolume = 0.5f;

    [Tooltip("Panel/popup appearing. Plays when menus, config screens, or selectors slide/pop open.")]
    [SerializeField] private AudioClip _panelOpen;
    [Range(0f, 1f)] [SerializeField] private float _panelOpenVolume = 0.4f;

    [Tooltip("Panel/popup closing. Plays when menus, config screens, or selectors slide/pop closed.")]
    [SerializeField] private AudioClip _panelClose;
    [Range(0f, 1f)] [SerializeField] private float _panelCloseVolume = 0.4f;

    [Tooltip("Invalid action feedback. Plays on invalid room code, stunned piece click, etc.")]
    [SerializeField] private AudioClip _error;
    [Range(0f, 1f)] [SerializeField] private float _errorVolume = 0.5f;

    // ─── Board Sounds ────────────────────────────────────────

    [Header("Board Sounds")]

    [Tooltip("Piece selected on the board. Plays when clicking a valid own piece to pick it up. Pitched up slightly.")]
    [SerializeField] private AudioClip _pieceSelect;
    [Range(0f, 1f)] [SerializeField] private float _pieceSelectVolume = 0.5f;

    [Tooltip("Piece deselected. Plays when clicking away from a selected piece or re-clicking it. Pitched down slightly.")]
    [SerializeField] private AudioClip _pieceDeselect;
    [Range(0f, 1f)] [SerializeField] private float _pieceDeselectVolume = 0.4f;

    [Tooltip("Piece lands on a tile after moving. Pitch varies by piece type: pawns are lower, queens/kings are higher.")]
    [SerializeField] private AudioClip _pieceMove;
    [Range(0f, 1f)] [SerializeField] private float _pieceMoveVolume = 0.6f;

    [Tooltip("Capture collision. Plays when the attacker reaches the defender's tile and the fight animation begins. Pitch is lower for heavier pieces.")]
    [SerializeField] private AudioClip _pieceCapture;
    [Range(0f, 1f)] [SerializeField] private float _pieceCaptureVolume = 0.7f;

    [Tooltip("Elemental trade won. Plays when the attacker's element beats the defender's element.")]
    [SerializeField] private AudioClip _tradeWon;
    [Range(0f, 1f)] [SerializeField] private float _tradeWonVolume = 0.6f;

    [Tooltip("Elemental trade lost. Plays when the attacker's element loses to the defender's. Pitched lower.")]
    [SerializeField] private AudioClip _tradeLost;
    [Range(0f, 1f)] [SerializeField] private float _tradeLostVolume = 0.6f;

    [Tooltip("Elemental trade draw. Plays on even trades where neither element wins.")]
    [SerializeField] private AudioClip _tradeDraw;
    [Range(0f, 1f)] [SerializeField] private float _tradeDrawVolume = 0.5f;

    [Tooltip("New element revealed. Plays during the element name zoom-in reveal animation after a capture.")]
    [SerializeField] private AudioClip _elementReveal;
    [Range(0f, 1f)] [SerializeField] private float _elementRevealVolume = 0.5f;

    [Tooltip("King in check. Plays when a move puts the opponent's king in check. Pitched up for urgency.")]
    [SerializeField] private AudioClip _check;
    [Range(0f, 1f)] [SerializeField] private float _checkVolume = 0.7f;

    [Tooltip("Checkmate detected. Plays when a player wins by checkmate. Decisive, final sound.")]
    [SerializeField] private AudioClip _checkmate;
    [Range(0f, 1f)] [SerializeField] private float _checkmateVolume = 0.8f;

    [Tooltip("Stalemate detected. Plays when the game ends in a draw due to no legal moves. Muted, neutral tone.")]
    [SerializeField] private AudioClip _stalemate;
    [Range(0f, 1f)] [SerializeField] private float _stalemateVolume = 0.6f;

    [Tooltip("Match beginning. Plays when a new game starts (local, AI, or online). Fresh start feel.")]
    [SerializeField] private AudioClip _gameStart;
    [Range(0f, 1f)] [SerializeField] private float _gameStartVolume = 0.6f;

    // ─── Effect Sounds ───────────────────────────────────────

    [Header("Effect Sounds")]

    [Tooltip("Destroy effect. Plays when a piece is destroyed by direct damage (not capture). Heavy impact.")]
    [SerializeField] private AudioClip _effectDamage;
    [Range(0f, 1f)] [SerializeField] private float _effectDamageVolume = 0.7f;

    [Tooltip("Push effect. Plays when a piece is shoved to another tile by a push reaction.")]
    [SerializeField] private AudioClip _effectPush;
    [Range(0f, 1f)] [SerializeField] private float _effectPushVolume = 0.5f;

    [Tooltip("Poison applied. Plays when poison is placed on a piece (kills after countdown). Sinister tone.")]
    [SerializeField] private AudioClip _effectPoison;
    [Range(0f, 1f)] [SerializeField] private float _effectPoisonVolume = 0.5f;

    [Tooltip("Stun applied. Plays when a piece is stunned and cannot move for a duration.")]
    [SerializeField] private AudioClip _effectStun;
    [Range(0f, 1f)] [SerializeField] private float _effectStunVolume = 0.5f;

    [Tooltip("Shield applied. Plays when a piece gains a protective shield that blocks the next capture. Bright, upward tone.")]
    [SerializeField] private AudioClip _effectShield;
    [Range(0f, 1f)] [SerializeField] private float _effectShieldVolume = 0.5f;

    [Tooltip("Convert effect. Plays when a piece switches color/allegiance to the opposing side.")]
    [SerializeField] private AudioClip _effectConvert;
    [Range(0f, 1f)] [SerializeField] private float _effectConvertVolume = 0.5f;

    [Tooltip("Transform effect. Plays when a piece temporarily changes its type (e.g. pawn becomes queen).")]
    [SerializeField] private AudioClip _effectTransform;
    [Range(0f, 1f)] [SerializeField] private float _effectTransformVolume = 0.5f;

    [Tooltip("Cleanse or dispel. Plays when negative effects are purged from a piece, or a shield is stripped.")]
    [SerializeField] private AudioClip _effectCleanse;
    [Range(0f, 1f)] [SerializeField] private float _effectCleanseVolume = 0.5f;

    [Tooltip("Effect impact on a tile. Plays for each wave of tiles hit during a reaction's spread animation. Pitch rises per wave for escalation.")]
    [SerializeField] private AudioClip _effectHit;
    [Range(0f, 1f)] [SerializeField] private float _effectHitVolume = 0.4f;

    // ─── Internals ───────────────────────────────────────────

    private AudioSource _source;
    private AudioSource _musicSource;

    private const string MusicVolumePref = "MusicVolume";
    private const string MusicMutedPref = "MusicMuted";
    private const string SfxMutedPref = "SfxMuted";

    private bool _musicMuted;
    private bool _sfxMuted;

    public float MusicVolume => _musicVolume;
    public bool MusicMuted => _musicMuted;
    public bool SfxMuted => _sfxMuted;

    private void Awake()
    {
        Instance = this;
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        AudioListener.volume = _masterVolume;

        // Music source
        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;

        if (PlayerPrefs.HasKey(MusicVolumePref))
            _musicVolume = PlayerPrefs.GetFloat(MusicVolumePref);
        _musicMuted = PlayerPrefs.GetInt(MusicMutedPref, 0) == 1;
        _sfxMuted = PlayerPrefs.GetInt(SfxMutedPref, 0) == 1;

        _musicSource.volume = _musicVolume;
        _musicSource.mute = _musicMuted;

        if (_musicClip != null)
        {
            _musicSource.clip = _musicClip;
            _musicSource.Play();
        }
    }

    private void OnValidate()
    {
        AudioListener.volume = _masterVolume;
        if (_musicSource != null) _musicSource.volume = _musicVolume;
    }

    public void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        if (_musicSource != null) _musicSource.volume = _musicVolume;
        PlayerPrefs.SetFloat(MusicVolumePref, _musicVolume);
    }

    public void SetMusicMuted(bool muted)
    {
        _musicMuted = muted;
        if (_musicSource != null) _musicSource.mute = _musicMuted;
        PlayerPrefs.SetInt(MusicMutedPref, muted ? 1 : 0);
    }

    public void SetSfxMuted(bool muted)
    {
        _sfxMuted = muted;
        PlayerPrefs.SetInt(SfxMutedPref, muted ? 1 : 0);
    }

    private void Play(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null || _sfxMuted) return;
        _source.pitch = pitch;
        _source.PlayOneShot(clip, volume);
    }

    // ─── UI ──────────────────────────────────────────────────

    public void PlayButtonHover() => Play(_buttonHover, _buttonHoverVolume, Random.Range(0.95f, 1.05f));
    public void PlayButtonClick() => Play(_buttonClick, _buttonClickVolume, 1f);
    public void PlayCancel() => Play(_buttonCancel, _buttonCancelVolume, 0.95f);
    public void PlayPanelOpen() => Play(_panelOpen, _panelOpenVolume, 1f);
    public void PlayPanelClose() => Play(_panelClose, _panelCloseVolume, 1f);
    public void PlayError() => Play(_error, _errorVolume, Random.Range(0.9f, 1f));

    // ─── Board ───────────────────────────────────────────────

    public void PlayPieceSelect() => Play(_pieceSelect, _pieceSelectVolume, 1.1f);
    public void PlayPieceDeselect() => Play(_pieceDeselect, _pieceDeselectVolume, 0.9f);

    public void PlayPieceMove(PieceType type) =>
        Play(_pieceMove, _pieceMoveVolume, GetPiecePitch(type));

    public void PlayCapture(PieceType attackerType) =>
        Play(_pieceCapture, _pieceCaptureVolume, GetPiecePitch(attackerType) * 0.9f);

    public void PlayTradeWon() => Play(_tradeWon, _tradeWonVolume, 1.15f);
    public void PlayTradeLost() => Play(_tradeLost, _tradeLostVolume, 0.85f);
    public void PlayTradeDraw() => Play(_tradeDraw, _tradeDrawVolume, 1f);
    public void PlayElementReveal() => Play(_elementReveal, _elementRevealVolume, 1f);
    public void PlayCheck() => Play(_check, _checkVolume, 1.1f);
    public void PlayCheckmate() => Play(_checkmate, _checkmateVolume, 1f);
    public void PlayStalemate() => Play(_stalemate, _stalemateVolume, 0.9f);
    public void PlayGameStart() => Play(_gameStart, _gameStartVolume, 1f);

    // ─── Effects ─────────────────────────────────────────────

    public void PlayEffect(EffectType type)
    {
        switch (type)
        {
            case EffectType.Damage:    Play(_effectDamage, _effectDamageVolume, 0.9f); break;
            case EffectType.Push:      Play(_effectPush, _effectPushVolume, 1f); break;
            case EffectType.Poison:    Play(_effectPoison, _effectPoisonVolume, 0.95f); break;
            case EffectType.Stun:      Play(_effectStun, _effectStunVolume, 0.9f); break;
            case EffectType.Shield:    Play(_effectShield, _effectShieldVolume, 1.1f); break;
            case EffectType.Convert:   Play(_effectConvert, _effectConvertVolume, 1f); break;
            case EffectType.Transform: Play(_effectTransform, _effectTransformVolume, 1.05f); break;
            case EffectType.Cleanse:   Play(_effectCleanse, _effectCleanseVolume, 1.1f); break;
        }
    }

    /// <summary>
    /// Play the effect-hit sound with ascending pitch per wave index (0-based).
    /// Each successive wave plays slightly higher for an escalation feel.
    /// </summary>
    public void PlayEffectHit(int waveIndex) =>
        Play(_effectHit, _effectHitVolume, 1f + waveIndex * 0.05f);

    // ─── Helpers ─────────────────────────────────────────────

    private static float GetPiecePitch(PieceType type) => type switch
    {
        PieceType.Pawn   => 0.85f,
        PieceType.Knight => 0.95f,
        PieceType.Bishop => 0.95f,
        PieceType.Rook   => 1.0f,
        PieceType.Queen  => 1.1f,
        PieceType.King   => 1.2f,
        _                => 1f
    };
}
