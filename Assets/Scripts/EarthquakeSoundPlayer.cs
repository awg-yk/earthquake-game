using UnityEngine;

namespace EarthquakeGame
{
    // Plays a short "rumble" sound when an earthquake hits, generated
    // procedurally at runtime (filtered noise with an attack/decay
    // envelope) so the project needs no imported audio assets.
    public class EarthquakeSoundPlayer : MonoBehaviour
    {
        public float baseDuration = 0.8f;
        public float durationPerRank = 0.35f;
        public int sampleRate = 44100;

        private AudioSource audioSource;

        void Awake()
        {
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        public void PlayRumble(int intensityRank)
        {
            if (intensityRank <= 0) return;

            float volume = Mathf.Clamp01(0.15f + intensityRank * 0.07f);
            AudioClip clip = BuildRumbleClip(intensityRank);
            audioSource.PlayOneShot(clip, volume);
        }

        private AudioClip BuildRumbleClip(int intensityRank)
        {
            float duration = baseDuration + durationPerRank * intensityRank;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[sampleCount];

            // Low-pass-ish rumble: sum a couple of low frequencies plus
            // filtered noise, shaped by an attack/decay envelope. Stronger
            // intensity = a bit louder and a touch harsher (more noise).
            float baseFreq = 45f;
            float noiseMix = Mathf.Clamp01(0.2f + intensityRank * 0.05f);
            float prevNoise = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / sampleCount;

                float envelope = progress < 0.05f
                    ? progress / 0.05f
                    : Mathf.Clamp01(1f - (progress - 0.05f) / 0.95f);

                float tone = Mathf.Sin(2 * Mathf.PI * baseFreq * t) * 0.6f
                           + Mathf.Sin(2 * Mathf.PI * baseFreq * 1.5f * t) * 0.3f;

                float rawNoise = Random.Range(-1f, 1f);
                prevNoise = Mathf.Lerp(prevNoise, rawNoise, 0.15f); // crude low-pass

                float sample = Mathf.Lerp(tone, prevNoise, noiseMix) * envelope;
                samples[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("EarthquakeRumble", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
