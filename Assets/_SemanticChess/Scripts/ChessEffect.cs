public enum EffectType { Damage, Stun, Push, Shield, Convert, Poison, Transform, Burning, Plant }

[System.Serializable]
public class ChessEffect
{
    public EffectType Type;
    public int Duration; // -1 = permanent, >0 = turns remaining

    // Push params
    public int PushDirCol; // -1, 0, or 1
    public int PushDirRow; // -1, 0, or 1
    public int PushDistance;

    // Transform params
    public PieceType TransformTarget;

    public ChessEffect(EffectType type, int duration = -1)
    {
        Type = type;
        Duration = duration;
    }

    public bool IsExpired => Duration == 0;

    /// <summary>
    /// Decrements duration by 1 if not permanent. Returns true if just expired.
    /// </summary>
    public bool Tick()
    {
        if (Duration < 0) return false;
        Duration--;
        return Duration <= 0;
    }
}
