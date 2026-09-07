using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EarthquakeGame
{
    // Owns the physical block tower shared by the player and the NPC:
    // placing new blocks and shaking the base when an earthquake hits the
    // player's location.
    public class BlockTowerManager : MonoBehaviour
    {
        // Fired the moment any block falls off the tower - used by
        // GameManager to end the Player-vs-NPC duel in sudden death.
        public event Action OnBlockFell;

        [Header("Base platform")]
        public Rigidbody2D baseRigidbody;
        public float baseHalfWidth = 4f;

        [Header("Block settings")]
        public float blockSize = 0.6f;
        public float spawnHeightMargin = 1.5f;
        [Tooltip("Every block is this single elongated rectangle (area = blockSize squared, width = blockSize*elongation, height = blockSize/elongation). Only its rotation varies.")]
        public float elongation = 1.4f;

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

        [Header("Stability (blocks should only fall from real earthquakes)")]
        public float blockMass = 3f;
        public float blockLinearDrag = 1f;
        public float blockAngularDrag = 2f;

        // High friction, no bounce - shared by the base and every block so
        // stacked blocks grip each other and don't slide/topple on their
        // own; only an actual shake should be able to knock them over.
        private static PhysicsMaterial2D highFrictionMaterial;
        public static PhysicsMaterial2D HighFrictionMaterial
        {
            get
            {
                if (highFrictionMaterial == null)
                {
                    highFrictionMaterial = new PhysicsMaterial2D("BlockFriction")
                    {
                        friction = 1.2f,
                        bounciness = 0f
                    };
                }
                return highFrictionMaterial;
            }
        }

        [Tooltip("Below this speed (units/sec and deg/sec), the most recently dropped block counts as settled and the next one may be placed.")]
        public float settleLinearThreshold = 0.05f;
        public float settleAngularThreshold = 5f;

        private readonly List<Block> aliveBlocks = new List<Block>();
        private Vector3 basePlatformRestPosition;
        private Coroutine shakeCoroutine;
        private Rigidbody2D lastPlacedRigidbody;

        // The player can only drop a new block once the previous one has
        // come to rest, so towers rise deliberately instead of blocks
        // being stacked mid-fall.
        public bool IsSettled()
        {
            if (lastPlacedRigidbody == null) return true;
            return lastPlacedRigidbody.velocity.sqrMagnitude < settleLinearThreshold * settleLinearThreshold
                && Mathf.Abs(lastPlacedRigidbody.angularVelocity) < settleAngularThreshold;
        }

        void Awake()
        {
            if (baseRigidbody != null) basePlatformRestPosition = baseRigidbody.transform.position;
        }

        public int AliveBlockCount => aliveBlocks.Count;
        public float CurrentHeight => GetCurrentTowerTopY() - GetBaseTopY();

        // Height at which the next block would be dropped, useful for
        // showing the player a "landing here" preview before they commit.
        public float GetNextSpawnY() => GetCurrentTowerTopY() + spawnHeightMargin;

        // Keeps a placement X position within the base platform's bounds.
        public float ClampX(float xPosition)
        {
            float margin = blockSize * elongation * 0.5f;
            return Mathf.Clamp(xPosition, -baseHalfWidth + margin, baseHalfWidth - margin);
        }

        // The single fixed block size, exposed so the preview (GameManager)
        // can show the exact same rectangle the player is about to drop.
        public Vector2 GetBlockSize()
        {
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

            var collider = obj.GetComponent<Collider2D>();
            if (collider != null) collider.sharedMaterial = HighFrictionMaterial;

            var rb = obj.AddComponent<Rigidbody2D>();
            rb.mass = blockMass;
            rb.drag = blockLinearDrag;
            rb.angularDrag = blockAngularDrag;

            var block = obj.AddComponent<Block>();
            block.Setup(BlockShape.Rectangle);

            lastPlacedRigidbody = rb;
            aliveBlocks.Add(block);
            return block;
        }

        private float GetBaseTopY()
        {
            return baseRigidbody != null ? baseRigidbody.transform.position.y + 0.15f : 0f;
        }

        private float GetCurrentTowerTopY()
        {
            float highest = GetBaseTopY();
            foreach (var block in aliveBlocks)
            {
                if (block == null) continue;
                // Use the collider's world bounds (not a fixed blockSize)
                // since blocks are rotated rectangles.
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
                    OnBlockFell?.Invoke();
                }
            }
        }

        public void ClearAllBlocks()
        {
            foreach (var block in aliveBlocks)
            {
                if (block != null) Destroy(block.gameObject);
            }
            aliveBlocks.Clear();
            lastPlacedRigidbody = null;
        }
    }
}
