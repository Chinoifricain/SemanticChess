public enum TileEffectType { Burning, Occupied, Ice, Plant }

[System.Serializable]
public class TileEffect
{
    public TileEffectType Type;
    public int Duration; // -1 = permanent, >0 = turns remaining
    public PieceColor OwnerColor; // color that created this effect; ticks on owner's turns

    public TileEffect(TileEffectType type, int duration = -1, PieceColor ownerColor = PieceColor.White)
    {
        Type = type;
        Duration = duration;
        OwnerColor = ownerColor;
    }

    public bool Tick()
    {
        if (Duration < 0) return false;
        Duration--;
        return Duration <= 0;
    }
}
