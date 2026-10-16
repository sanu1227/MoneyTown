using System.Collections;
using UnityEngine;

namespace MoneyTown
{
    public sealed class GameMusic : MonoBehaviour
    {
        Coroutine fade;
        bool started, paused;
        int resumeSample;
        double startedAt;
        float fadeFactor;
        public bool MusicEnabled { get; private set; } = true;
        public int VolumePercent { get; private set; } = 25;
        public bool IsSuspended { get; private set; } = true;
        public AudioSource OutputSource { get; private set; }

        public void Initialize()
        {
            if (OutputSource != null) return;
            OutputSource = gameObject.AddComponent<AudioSource>();
            OutputSource.playOnAwake = false;
            OutputSource.loop = true;
            OutputSource.spatialBlend = 0;
            OutputSource.pitch = 1;
            OutputSource.volume = 0;
            OutputSource.clip = Resources.Load<AudioClip>("Audio/town-lounge");
        }

        public void Configure(bool enabled, int volumePercent)
        {
            Initialize();
            MusicEnabled = enabled;
            VolumePercent = Mathf.Clamp(volumePercent, 0, 100);
            ApplyState();
        }

        public void SetSuspended(bool suspended)
        {
            IsSuspended = suspended;
            ApplyState();
        }

        void ApplyState()
        {
            if (OutputSource == null || OutputSource.clip == null) return;
            if (!MusicEnabled || VolumePercent == 0 || IsSuspended || !isActiveAndEnabled) {
                Pause();
                return;
            }
            if (!OutputSource.isPlaying) {
                StopFade();
                OutputSource.volume = 0;
                if (started && paused) OutputSource.UnPause();
                if (!OutputSource.isPlaying) {
                    // Deactivating the GameObject can stop its native audio voice.
                    if (started) OutputSource.timeSamples = paused ? resumeSample : Playhead();
                    OutputSource.Play();
                }
                resumeSample = OutputSource.timeSamples;
                startedAt = AudioSettings.dspTime;
                started = true;
                paused = false;
                fadeFactor = 0;
                fade = StartCoroutine(FadeIn());
            }
            OutputSource.volume = VolumePercent / 100f * fadeFactor;
        }

        int Playhead()
        {
            int sample = OutputSource.timeSamples;
            if (OutputSource.isPlaying || sample > 0) return sample;
            var clip = OutputSource.clip;
            if (clip == null || clip.samples <= 0) return 0;
            double elapsed = System.Math.Max(0, AudioSettings.dspTime - startedAt);
            return (int)((resumeSample + elapsed * clip.frequency) % clip.samples);
        }

        void Pause()
        {
            StopFade();
            if (OutputSource == null) return;
            if (started && !paused) {
                resumeSample = Playhead();
                OutputSource.Pause();
                paused = true;
            }
            OutputSource.volume = 0;
        }

        IEnumerator FadeIn()
        {
            float elapsed = 0;
            while (elapsed < .5f) {
                fadeFactor = Mathf.Clamp01(elapsed / .5f);
                OutputSource.volume = VolumePercent / 100f * fadeFactor;
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            fadeFactor = 1;
            OutputSource.volume = VolumePercent / 100f;
            fade = null;
        }

        void StopFade()
        {
            if (fade != null) StopCoroutine(fade);
            fade = null;
        }

        void OnEnable() => ApplyState();
        void OnDisable() => Pause();
        void OnDestroy() => StopFade();
    }
}
