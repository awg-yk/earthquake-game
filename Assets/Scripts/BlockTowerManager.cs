using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EarthquakeGame
{
    // Owns the physical block tower: placing new blocks, shaking the base
    // when an earthquake hits the player's location, and scoring whatever
    // is still standing at the end of the year.
    public class BlockTowerManager : MonoBehaviour
    {
        [Header("Base platform")]
        public Rigidbody2D baseRigidbody;
        public float baseHalfWidth = 4f;

        [Header("Block settings")]
        public float blockSize = 0.6f;
        public float spawnHeightMargin = 1.5f;
        [Tooltip("Range of random elongation applied to each block's rectangle (area stays constant: width = blockSize*elongation, height = blockSize/elongation).")]
        public float minElongation = 0.6f;
        public float maxElongation = 1.8f;

        // Highest the tower has ever reached above the base, tracked for the round-end height record.
        private float maxHeightReached;
        public float MaxHeightReached => maxHeightReached;

        [Header("Shake settings")]
        [Tooltip("Screen/world units of shake amplitude per intensity rank point.")]
        public float shakeAmplitudePerRank = 0.08f;
        [Tooltip("Minimum shake duration, in seconds, at the weakest felt intensity.")]
        public float baseShakeDuration = 0.8f;
        [Tooltip("Additional seconds of shaking added per intensity rank point.")]
        public float shakeDurationPerRank = 0.35f;
        public float shakeFrequency = 25f;

        [Tooltip("Y position below which a block is considered fallen/lost.")]
        public float fallenYThreshold = -3f;

        private readonly List<Block> aliveBlocks = new List<Block>();
        private Vector3 basePlatformRestPosition;
        private Coroutine shakeCoroutine;

        void Awake()
        {
            if (baseRigidbody != null) basePlatformRestPosition = baseRigidbody.transform.position;
        }

        public int AliveBlockCount => aliveBlocks.Count;

        // Height at which the next block would be dropped, useful for
        // showing the player a "landing here" preview before they commit.
        public float GetNextSpawnY() => GetCurrentTowerTopY() + spawnHeightMargin;

        // Keeps a placement X position within the base platform's bounds.
        // Uses the widest a block can ever be (fully elongated) as the
        // safety margin so an elongated block never hangs off the edge.
        public float ClampX(float xPosition)
        {
            float margin = blockSize * maxElongation * 0.5f;
            return Mathf.Clamp(xPosition, -baseHalfWidth + margin, baseHalfWidth - margin);
        }

        // Picks a random elongation for the next block, exposed so the
        // preview (GameManager) can show the exact same rectangle the
        // player is about to drop.
        public Vector2 RollRandomSize()
        {
            float elongation = Random.Range(minElongation, maxElongation);
            return new Vector2(blockSize * elongation, blockSize / elongation);
        }

        public Block PlaceBlock(Vector2 size, float xPosition, float rotationDegrees = 0f)
        {
            xPosition = ClampX(xPosition);
            float spawnY = GetCurrentTowerTopY() + spawnHeightMargin;

            GameObject obj = new GameObject("Block_Rectangle");
            obj.transform.position = new Vector3(xPosition, spawnY, 0);
            obj.transform.rotation = Quaternion.Euler(0, 0, rotationDegrees);

            Color color = new Color(0.85f, 0.45f, 0.4f);
            ShapeMeshFactory.Apply(obj, size, color);

            var rb = obj.AddComponent<Rigidbody2D>();
            rb.mass = 1f;

            var block = obj.AddComponent<Block>();
            block.Setup(BlockShape.Rectangle);

            aliveBlocks.Add(block);
            return block;
        }

        private float GetCurrentTowerTopY()
        {
            float baseTop = baseRigidbody != null
                ? baseRigidbody.transform.position.y + 0.15f
                : 0f;

            float highest = baseTop;
            foreach (var block in aliveBlocks)
            {
                if (block == null) continue;
                // Use the collider's world bounds (not a fixed blockSize)
                // since blocks now have randomized, rotated rectangles.
                var collider = block.GetComponent<Collider2D>();
                float top = collider != null ? collider.bounds.max.y : block.transform.position.y;
                if (top > highest) highest = top;
            }
            return highest;
        }

        // Called once per day after checking whether an earthquake hit the
        // player's prefecture. rank 0 means no shaking this day.
        public void Shake(int intensityRank)
        {
            if (intensityRank <= 0 || baseRigidbody == null) return;
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeRoutine(intensityRank));
        }

        private IEnumerator ShakeRoutine(int intensityRank)
        {
            // Snapshot who's standing before the shake, so we can tell
            // afterwards who survived it (for the score multiplier) even
            // though PlaceBlock/Update keep mutating aliveBlocks over time.
            var blocksBeforeShake = new List<Block>(aliveBlocks);

            float amplitude = shakeAmplitudePerRank * intensityRank;
            float duration = baseShakeDuration + shakeDurationPerRank * intensityRank;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float damping = 1f - (elapsed / duration);
                float offsetX = Mathf.Sin(elapsed * shakeFrequency) * amplitude * damping;
                baseRigidbody.MovePosition(basePlatformRestPosition + new Vector3(offsetX, 0, 0));
                yield return null;
            }

            baseRigidbody.MovePosition(basePlatformRestPosition);

            int shindoNumber = IntensityScale.GetShindoNumberFromRank(intensityRank);
            if (shindoNumber >= 5)
            {
                foreach (var block in blocksBeforeShake)
                {
                    if (block != null && aliveBlocks.Contains(block))
                    {
                        block.ApplySurvivedShindo(shindoNumber);
                    }
                }
            }

            shakeCoroutine = null;
        }

        void Update()
        {
            for (int i = aliveBlocks.Count - 1; i >= 0; i--)
            {
                var block = aliveBlocks[i];
                if (block == null || block.transform.position.y < fallenYThreshold)
                {
                    aliveBlocks.RemoveAt(i);
                    if (block != null) Destroy(block.gameObject);
                }
            }

            float baseTop = baseRigidbody != null ? baseRigidbody.transform.position.y + 0.15f : 0f;
            float currentHeight = GetCurrentTowerTopY() - baseTop;
            if (currentHeight > maxHeightReached) maxHeightReached = currentHeight;
        }

        public int GetScore()
        {
            int total = 0;
            foreach (var block in aliveBlocks)
            {
                if (block != null) total += block.FinalScore;
            }
            return total;
        }

        public void ClearAllBlocks()
        {
            foreach (var block in aliveBlocks)
            {
                if (block != null) Destroy(block.gameObject);
            }
            aliveBlocks.Clear();
            maxHeightReached = 0f;
        }
    }
}
