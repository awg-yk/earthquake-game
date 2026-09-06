using System.Collections;
using UnityEngine;

namespace EarthquakeGame
{
    // Shakes the camera itself (in addition to BlockTowerManager shaking the
    // base) so an earthquake reads as a screen-wide event, not just
    // something happening to the tower.
    public class CameraShaker : MonoBehaviour
    {
        public float amplitudePerRank = 0.05f;
        public float baseDuration = 0.8f;
        public float durationPerRank = 0.35f;
        public float frequency = 30f;

        private Vector3 restPosition;
        private Coroutine shakeCoroutine;
        private bool isShaking;

        void Awake()
        {
            restPosition = transform.position;
        }

        void LateUpdate()
        {
            // Keep tracking wherever the camera is meant to rest (e.g. the
            // tower-height follow in GameManager moves it every frame) as
            // long as we're not the ones currently displacing it for a shake.
            if (!isShaking) restPosition = transform.position;
        }

        public void Shake(int intensityRank)
        {
            if (intensityRank <= 0) return;
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeRoutine(intensityRank));
        }

        private IEnumerator ShakeRoutine(int intensityRank)
        {
            isShaking = true;
            float amplitude = amplitudePerRank * intensityRank;
            float duration = baseDuration + durationPerRank * intensityRank;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float damping = 1f - (elapsed / duration);
                // Horizontal-only: real earthquakes read here as side-to-side
                // sway, and a vertical component made the base look like it
                // was bobbing up and down - an unstable, unintended look.
                float offsetX = (Mathf.PerlinNoise(Time.time * frequency, 0f) - 0.5f) * 2f * amplitude * damping;
                transform.position = restPosition + new Vector3(offsetX, 0, 0);
                yield return null;
            }

            transform.position = restPosition;
            isShaking = false;
            shakeCoroutine = null;
        }
    }
}
