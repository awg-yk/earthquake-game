namespace EarthquakeGame
{
    public enum BlockShape
    {
        Square,
        Triangle,
        Circle
    }

    // Fewer corners = harder to balance = worth more points if it survives.
    public static class BlockShapeInfo
    {
        public static int GetCorners(BlockShape shape)
        {
            switch (shape)
            {
                case BlockShape.Square: return 4;
                case BlockShape.Triangle: return 3;
                case BlockShape.Circle: return 0;
                default: return 4;
            }
        }

        public static int GetScore(BlockShape shape)
        {
            switch (shape)
            {
                case BlockShape.Square: return 3;
                case BlockShape.Triangle: return 6;
                case BlockShape.Circle: return 10;
                default: return 0;
            }
        }

        public static string GetLabel(BlockShape shape)
        {
            switch (shape)
            {
                case BlockShape.Square: return "四角";
                case BlockShape.Triangle: return "三角";
                case BlockShape.Circle: return "丸";
                default: return shape.ToString();
            }
        }
    }
}
