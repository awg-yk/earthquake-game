using UnityEngine;

namespace EarthquakeGame
{
    // A short, upbeat, looping background track generated procedurally at
    // runtime (square-wave melody + triangle-wave bass over a simple I-V-vi-IV
    // pop chord progression) so the project needs no imported music asset.
    public class BGMPlayer : MonoBehaviour
    {
        public int sampleRate = 44100;
        [Range(0f, 1f)] public float volume = 0.25f;
        public float bpm = 128f;

        private AudioSource audioSource;

        void Awake()
        {
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
        }

        public void Play()
        {
            if (audioSource.isPlaying)
            {
                Debug.Log("BGMPlayer: Play() called but audioSource already playing - ignoring.");
                return;
            }

            audioSource.mute = false;
            audioSource.clip = BuildLoop();
            audioSource.volume = volume;
            AudioListener.volume = 1f;
            audioSource.Play();

            Debug.Log($"BGMPlayer: started playback. clip length={audioSource.clip.length:0.00}s, volume={audioSource.volume}, AudioListener.volume={AudioListener.volume}, isPlaying={audioSource.isPlaying}");
        }

        public void Stop() => audioSource.Stop();

        private float NoteFreq(int semitoneFromA4)
        {
            return 440f * Mathf.Pow(2f, semitoneFromA4 / 12f);
        }

        private AudioClip BuildLoop()
        {
            // I - V - vi - IV in C major (C, G, Am, F): the classic upbeat
            // "pop" progression. Each chord lasts one bar (4 beats).
            float beatDuration = 60f / bpm;
            float barDuration = beatDuration * 4f;
            int bars = 4;
            float totalDuration = barDuration * bars;
            int sampleCount = Mathf.CeilToInt(totalDuration * sampleRate);
            var samples = new float[sampleCount];

            // Root note (semitones relative to A4) for each chord's bass,
            // and a simple major-triad arpeggio pattern built from it.
            int[] chordRootSemitone = { -9, -2, -12, -7 }; // C4, G4(-ish), A3, F4 relative to A4
            int[] arpeggioOffsets = { 0, 4, 7, 12 }; // root, major third, fifth, octave

            for (int bar = 0; bar < bars; bar++)
            {
                int root = chordRootSemitone[bar % chordRootSemitone.Length];
                float barStart = bar * barDuration;

                // Bass: one sustained triangle-ish note per bar.
                WriteNote(samples, barStart, barDuration * 0.95f, NoteFreq(root - 12), 0.28f, triangle: true);

                // Melody: a bouncy 8th-note arpeggio, two notes per beat.
                for (int step = 0; step < 8; step++)
                {
                    float noteStart = barStart + step * (beatDuration * 0.5f);
                    int offset = arpeggioOffsets[step % arpeggioOffsets.Length];
                    WriteNote(samples, noteStart, beatDuration * 0.45f, NoteFreq(root + offset), 0.18f, triangle: false);
                }
            }

            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = Mathf.Clamp(samples[i], -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("PopBGM", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void WriteNote(float[] samples, float startTime, float duration, float freq, float amplitude, bool triangle)
        {
            int startSample = Mathf.FloorToInt(startTime * sampleRate);
            int noteSampleCount = Mathf.FloorToInt(duration * sampleRate);

            for (int i = 0; i < noteSampleCount; i++)
            {
                int idx = startSample + i;
                if (idx < 0 || idx >= samples.Length) continue;

                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * (triangle ? 2.5f : 6f));

                float phase = (freq * t) % 1f;
                float value = triangle
                    ? 1f - 4f * Mathf.Abs(Mathf.Round(phase) - phase) // triangle wave
                    : (phase < 0.5f ? 1f : -1f);                      // square wave

                samples[idx] += value * amplitude * envelope;
            }
        }
    }
}
