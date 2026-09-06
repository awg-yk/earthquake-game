namespace EarthquakeGame
{
    // Only rectangles now (squares are just the elongation=1 case) - see
    // BlockTowerManager.PlaceBlock, which randomizes each block's aspect
    // ratio (long-and-thin vs. short-and-wide) for stacking variety.
    public enum BlockShape
    {
        Rectangle
    }

    public static class BlockShapeInfo
    {
        // Flat base score - shape no longer varies, so there's nothing to
        // differentiate here anymore. The block-survives-a-big-shake
        // multiplier (Block.ApplySurvivedShindo) is the main scoring lever.
        public const int BaseScore = 5;

        public static int GetScore(BlockShape shape) => BaseScore;

        public static string GetLabel(BlockShape shape) => "長方形";
    }
}
