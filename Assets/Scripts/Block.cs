using UnityEngine;

namespace EarthquakeGame
{
    // A single placed block. Knows its own shape/base score, and reports
    // itself as "fallen" once it drops below the tower's base level so
    // BlockTowerManager can remove it from the surviving count.
    //
    // Multiplier: a block that survives a shindo-5-or-stronger shake earns
    // a permanent score multiplier equal to that shindo number (5/6/7),
    // rewarding players who let their tower ride out big earthquakes
    // instead of always fleeing to safety.
    public class Block : MonoBehaviour
    {
        public BlockShape Shape { get; private set; }
        public int BaseScore { get; private set; }
        public int Multiplier { get; private set; } = 1;

        public int FinalScore => BaseScore * Multiplier;

        public void Setup(BlockShape shape)
        {
            Shape = shape;
            BaseScore = BlockShapeInfo.GetScore(shape);
        }

        // Called once per shake this block survived, with the shindo
        // number (5/6/7) of that shake. Keeps the highest ever survived.
        public void ApplySurvivedShindo(int shindoNumber)
        {
            if (shindoNumber > Multiplier) Multiplier = shindoNumber;
        }
    }
}
