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
        public float ClampX(float xPosition)
        {
            return Mathf.Clamp(xPosition, -baseHalfWidth + blockSize * 0.5f, baseHalfWidth - blockSize * 0.5f);
        }

        public Block PlaceBlock(BlockShape shape, float xPosition)
        {
            xPosition = ClampX(xPosition);
            float spawnY = GetCurrentTowerTopY() + spawnHeightMargin;

            GameObject obj = new GameObject($"Block_{shape}");
            obj.transform.position = new Vector3(xPosition, spawnY, 0);

            Color color = shape switch
            {
                BlockShape.Square => new Color(0.85f, 0.4f, 0.4f),
                BlockShape.Triangle => new Color(0.4f, 0.75f, 0.85f),
                BlockShape.Circle => new Color(0.9f, 0.8f, 0.3f),
                _ => Color.white
            };
            ShapeMeshFactory.Apply(obj, shape, blockSize, color);

            var rb = obj.AddComponent<Rigidbody2D>();
            rb.mass = 1f;

            var block = obj.AddComponent<Block>();
            block.Setup(shape);

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
                float top = block.transform.position.y + blockSize * 0.5f;
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
        }

        public int GetScore()
        {
            int total = 0;
            foreach (var block in aliveBlocks)
            {
                if (block != null) total += block.Score;
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
        }
    }
}
