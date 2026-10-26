// Small cross-scene hand-off between the main menu and the game scene. The menu
// stores the world generation parameters here, then loads the game scene, whose
// MapManager consumes them and runs the generation + character selection flow.
public static class GameSession
{
    public static WorldGenConfig PendingWorldConfig { get; private set; }

    public static bool HasPendingWorld => PendingWorldConfig != null;

    // Called from the main menu when the player presses Generate.
    public static void RequestWorld(WorldGenConfig config)
    {
        PendingWorldConfig = (config ?? WorldGenConfig.CreateDefault()).Clone();
    }

    // Called by MapManager once it has read the parameters.
    public static WorldGenConfig ConsumeWorldConfig()
    {
        WorldGenConfig config = PendingWorldConfig;
        PendingWorldConfig = null;
        return config;
    }

    public static void Clear()
    {
        PendingWorldConfig = null;
    }
}
