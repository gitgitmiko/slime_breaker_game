namespace SlimeBreaker
{
    public enum GameState
    {
        Boot,
        Menu,
        Playing,
        BossIntro,
        LevelUp,
        Paused,
        Victory,
        Defeat,
        /// <summary>All lives gone: waiting for New Game / Continue (watch an ad).</summary>
        OutOfLives
    }
}
