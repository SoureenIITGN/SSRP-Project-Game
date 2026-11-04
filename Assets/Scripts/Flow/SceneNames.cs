namespace SSRP.Flow
{
    /// <summary>[D1] Scene names in one place. No scene-name string literals anywhere else.</summary>
    public static class SceneNames
    {
        public const string Boot = "Boot";
        public const string MainMenu = "MainMenu";
        public const string Game = "Game";

        // Development scenes (built by SSRP > Build Project; not in the shipping flow).
        public const string DevPhysicsSandbox = "Dev_PhysicsSandbox";
        public const string DevTileArena = "Dev_TileArena";
        public const string DevInputPipeline = "Dev_InputPipeline";
        public const string DevSerialPanel = "Dev_SerialPanel";
    }
}
