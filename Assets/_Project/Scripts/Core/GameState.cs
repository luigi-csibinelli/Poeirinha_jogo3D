namespace Poeirinha
{
    /// <summary>Estados possíveis de uma partida (GDD seção 10).</summary>
    public enum GameState
    {
        Playing,
        Paused,
        Victory,
        Defeat
    }

    /// <summary>Motivo da derrota, mostrado na tela de fim de jogo.</summary>
    public enum DefeatReason
    {
        None,
        BatteryEmpty,
        FellDownStairs,
        TimeUp
    }
}
