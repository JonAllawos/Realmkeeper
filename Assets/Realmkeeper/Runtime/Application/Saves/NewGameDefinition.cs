namespace Realmkeeper.Application
{
    /// <summary>
    /// M1.1: the one world New Game starts. A stand-in for the canonical world definition (M1.3): until then New Game
    /// always founds this name and seed, the test world the game has opened on since Phase 19. Changing these values
    /// changes every new game, never an existing save. Chosen by the owner (brief M1.1 §11; default kept).
    /// </summary>
    public static class NewGameDefinition
    {
        public const string WorldName = "Realmkeeper";
        public const long Seed = 123456789;
    }
}
