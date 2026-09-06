using UnityEngine;

namespace EarthquakeGame
{
    // A single placed block. BlockTowerManager tracks scoring (block count
    // + tower height) itself, so this component just tags the GameObject
    // and remembers which shape it was built from.
    public class Block : MonoBehaviour
    {
        public BlockShape Shape { get; private set; }

        public void Setup(BlockShape shape)
        {
            Shape = shape;
        }
    }
}
