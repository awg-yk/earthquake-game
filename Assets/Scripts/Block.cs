using UnityEngine;

namespace EarthquakeGame
{
    // A single placed block. Knows its own shape/score, and reports itself
    // as "fallen" once it drops below the tower's base level so
    // BlockTowerManager can remove it from the surviving count.
    public class Block : MonoBehaviour
    {
        public BlockShape Shape { get; private set; }
        public int Score { get; private set; }

        public void Setup(BlockShape shape)
        {
            Shape = shape;
            Score = BlockShapeInfo.GetScore(shape);
        }
    }
}
