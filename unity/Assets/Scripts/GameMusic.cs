using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoneyTown
{
    public sealed class GameMusic : MonoBehaviour
    {
        readonly System.Random random = new System.Random();
        readonly List<AudioClip> tracks = new List<AudioClip>();
        readonly List<string> titles = new List<string>();
        int[] bag;
        int bagPosition;
        Coroutine fade;
        bool started, paused;
        int resumeSample;
        double startedAt;
        float fadeFactor;
        public bool MusicEnabled { get; private set; } = true;
        public int VolumePercent { get; private set; } = 25;
        public bool IsSuspended { get; private set; } = true;
        public AudioSource OutputSource { get; private set; }
        public int TrackCount => tracks.Count;
        public int CurrentTrackIndex { get; private set; } = -1;
        public string CurrentTitle => CurrentTrackIndex < 0 ? "재생 가능한 곡 없음" : titles[CurrentTrackIndex];

        public void Initialize()
        {
            if (OutputSource != null) return;
            OutputSource = gameObject.AddComponent<AudioSource>();
            OutputSource.playOnAwake = false;
            OutputSource.loop = false;
            OutputSource.spatialBlend = 0;
            OutputSource.pitch = 1;
            OutputSource.volume = 0;
            string[] paths = { "town-lounge", "market-morning", "newsroom-bossa", "city-evening" };
            string[] names = { "장 마감의 여유", "아침의 티커", "신문 가판대", "도시의 야간장" };
            for (int i = 0; i < paths.Length; i++) {
                var clip = Resources.Load<AudioClip>("Audio/" + paths[i]);
                if (clip == null) continue;
                tracks.Add(clip); titles.Add(names[i]);
            }
            bag = new int[tracks.Count];
            bagPosition = bag.Length;
            SelectNextTrack();
        }

        public void NextTrack()
        {
            Initialize();
            SelectNextTrack();
            ApplyState();
        }

        void SelectNextTrack()
        {
            if (TrackCount == 0) return;
            if (bagPosition >= bag.Length) {
                for (int i = 0; i < bag.Length; i++) bag[i] = i;
                for (int i = bag.Length - 1; i > 0; i--) {
                    int j = random.Next(i + 1), value = bag[i];
                    bag[i] = bag[j]; bag[j] = value;
                }
                if (bag.Length > 1 && bag[0] == CurrentTrackIndex) {
                    int j = random.Next(1, bag.Length), value = bag[0];
                    bag[0] = bag[j]; bag[j] = value;
                }
                bagPosition = 0;
            }
            StopFade();
            OutputSource.Stop();
            CurrentTrackIndex = bag[bagPosition++];
            OutputSource.clip = tracks[CurrentTrackIndex];
            OutputSource.volume = 0;
            resumeSample = 0;
            started = paused = false;
            fadeFactor = 0;
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
                if (started && !paused) {
                    // Streaming voices can reset timeSamples at their natural end.
                    if (Playhead() < OutputSource.clip.samples - 1) return;
                    SelectNextTrack();
                }
                StopFade();
                OutputSource.volume = 0;
                if (started && paused) OutputSource.UnPause();
                if (!OutputSource.isPlaying) {
                    // Deactivating the GameObject can stop its native audio voice.
                    if (started) OutputSource.timeSamples = resumeSample;
                    OutputSource.Play();
                }
                started = true;
                paused = false;
                startedAt = AudioSettings.dspTime;
                fadeFactor = 0;
                fade = StartCoroutine(FadeIn());
            }
            // The audio thread may finish between isPlaying and this sample read.
            // Keep the last position if the native voice resets to zero.
            int sample = OutputSource.timeSamples;
            if (sample >= resumeSample) {
                resumeSample = sample;
                startedAt = AudioSettings.dspTime;
            }
            UpdateVolume();
        }

        int Playhead()
        {
            int sample = OutputSource.timeSamples;
            if (OutputSource.isPlaying) return sample;
            if (paused) return resumeSample;
            var clip = OutputSource.clip;
            if (clip == null || clip.samples <= 0) return 0;
            double elapsed = System.Math.Max(0, AudioSettings.dspTime - startedAt);
            return (int)System.Math.Min(clip.samples - 1, System.Math.Max(sample, resumeSample + elapsed * clip.frequency));
        }

        void UpdateVolume()
        {
            var clip = OutputSource.clip;
            float outro = Mathf.Clamp01((clip.samples - Playhead()) / (clip.frequency * .5f));
            OutputSource.volume = VolumePercent / 100f * fadeFactor * outro;
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
                UpdateVolume();
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            fadeFactor = 1;
            UpdateVolume();
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
