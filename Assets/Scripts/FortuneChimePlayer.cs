using UnityEngine;

namespace EarthquakeGame
{
    // Plays a short mystical-sounding chime for the fortune teller's
    // periodic forecast, generated procedurally at runtime (a few
    // arpeggiated sine tones with individual decay envelopes) so the
    // project needs no imported audio assets.
    public class FortuneChimePlayer : MonoBehaviour
    {
        public int sampleRate = 44100;

        private AudioSource audioSource;

        void Awake()
        {
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        public void PlayChime()
        {
            AudioClip clip = BuildChimeClip();
            audioSource.PlayOneShot(clip, 0.5f);
        }

        private AudioClip BuildChimeClip()
        {
            // A gentle ascending arpeggio (major-ish chord), each note
            // entering slightly after the last and ringing out with a
            // long decay - reads as "magical" rather than "alarm".
            float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.5f }; // C5 E5 G5 C6
            float noteSpacing = 0.12f;
            float noteDecay = 1.1f;
            float totalDuration = noteSpacing * (freqs.Length - 1) + noteDecay + 0.2f;

            int sampleCount = Mathf.CeilToInt(totalDuration * sampleRate);
            var samples = new float[sampleCount];

            for (int n = 0; n < freqs.Length; n++)
            {
                float startTime = n * noteSpacing;
                int startSample = Mathf.FloorToInt(startTime * sampleRate);
                int noteSampleCount = Mathf.FloorToInt(noteDecay * sampleRate);

                for (int i = 0; i < noteSampleCount; i++)
                {
                    int sampleIndex = startSample + i;
                    if (sampleIndex >= sampleCount) break;

                    float t = (float)i / sampleRate;
                    float envelope = Mathf.Exp(-t * 3.2f);
                    float value = Mathf.Sin(2 * Mathf.PI * freqs[n] * t) * envelope * 0.35f;
                    samples[sampleIndex] += value;
                }
            }

            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = Mathf.Clamp(samples[i], -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("FortuneChime", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
