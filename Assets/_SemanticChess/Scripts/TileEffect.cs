public enum TileEffectType { Burning, Occupied, Ice, Plant }

[System.Serializable]
public class TileEffect
{
    public TileEffectType Type;
    public int Duration; // -1 = permanent, >0 = turns remaining

    public TileEffect(TileEffectType type, int duration = -1)
    {
        Type = type;
        Duration = duration;
    }

    public bool Tick()
    {
        if (Duration < 0) return false;
        Duration--;
        return Duration <= 0;
    }
}
