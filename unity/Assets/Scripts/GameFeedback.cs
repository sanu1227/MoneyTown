using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoneyTown
{
    public enum GameCue { Tap, Buy, Sell, Paper, Settlement, Work, Success, Error }
    public enum FeedbackMotion { Press, Pop, Reveal }

    public sealed class GameFeedback : MonoBehaviour
    {
        sealed class Motion
        {
            public RectTransform Target;
            public FeedbackMotion Kind;
            public Vector3 Scale;
            public CanvasGroup Group;
            public float Alpha, Elapsed, Duration;
        }

        readonly AudioClip[] clips = new AudioClip[8];
        readonly List<Motion> motions = new List<Motion>();
        double lastTapTime = double.NegativeInfinity;
        public GameCue? LastCue { get; private set; }
        public long PlayedCount { get; private set; }
        public int ActiveMotions => motions.Count;
        public bool SoundEnabled { get; private set; } = true;
        public int VolumePercent { get; private set; } = 50;
        public bool ReducedMotion { get; private set; }
        public bool IsSuspended { get; private set; }
        public AudioSource OutputSource { get; private set; }

        public void Initialize()
        {
            if (OutputSource != null) return;
            OutputSource = gameObject.AddComponent<AudioSource>();
            OutputSource.playOnAwake = false;
            OutputSource.spatialBlend = 0;
            OutputSource.loop = false;
            OutputSource.volume = VolumePercent / 100f;
            if (FindFirstObjectByType<AudioListener>() == null) {
                var listener = new GameObject("FeedbackListener");
                listener.transform.SetParent(transform, false);
                listener.AddComponent<AudioListener>();
            }
            foreach (GameCue cue in Enum.GetValues(typeof(GameCue)))
                clips[(int)cue] = Resources.Load<AudioClip>("Audio/" + cue.ToString().ToLowerInvariant());
            enabled = motions.Count > 0;
        }

        public void Configure(bool soundEnabled, int volumePercent, bool reducedMotion)
        {
            Initialize();
            SoundEnabled = soundEnabled;
            VolumePercent = Mathf.Clamp(volumePercent, 0, 100);
            ReducedMotion = reducedMotion;
            OutputSource.volume = VolumePercent / 100f;
            if (!SoundEnabled || VolumePercent == 0) OutputSource.Stop();
            if (ReducedMotion) CancelAnimations();
        }

        public void SetSuspended(bool suspended)
        {
            if (IsSuspended == suspended) return;
            IsSuspended = suspended;
            if (!suspended) return;
            if (OutputSource != null) OutputSource.Stop();
            CancelAnimations();
        }

        public void Play(GameCue cue)
        {
            Initialize();
            int index = (int)cue;
            if (!SoundEnabled || VolumePercent == 0 || IsSuspended || !gameObject.activeInHierarchy ||
                index < 0 || index >= clips.Length || clips[index] == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (cue == GameCue.Tap && now - lastTapTime < .06) return;
            if (cue == GameCue.Tap) lastTapTime = now;
            // The latest cue replaces the previous one; repeated inputs cannot stack voices.
            OutputSource.Stop();
            OutputSource.clip = clips[index];
            OutputSource.Play();
            LastCue = cue;
            PlayedCount++;
        }

        public void Animate(RectTransform target, FeedbackMotion kind)
        {
            if (target == null) return;
            CancelTarget(target);
            if (ReducedMotion || IsSuspended || !gameObject.activeInHierarchy || !target.gameObject.activeInHierarchy) {
                if (motions.Count == 0) enabled = false;
                return;
            }
            var motion = new Motion { Target = target, Kind = kind, Scale = target.localScale,
                Duration = kind == FeedbackMotion.Press ? .12f : kind == FeedbackMotion.Pop ? .25f : .22f };
            var lifecycle = target.GetComponent<FeedbackMotionTarget>();
            if (lifecycle == null) lifecycle = target.gameObject.AddComponent<FeedbackMotionTarget>();
            lifecycle.Owner = this;
            lifecycle.Target = target;
            if (kind == FeedbackMotion.Reveal) {
                motion.Group = target.GetComponent<CanvasGroup>();
                if (motion.Group == null) motion.Group = target.gameObject.AddComponent<CanvasGroup>();
                motion.Alpha = motion.Group.alpha;
            }
            motions.Add(motion);
            Apply(motion, 0);
            enabled = true;
        }

        public void CancelAnimations()
        {
            foreach (var motion in motions) Restore(motion);
            motions.Clear();
            enabled = false;
        }

        internal void CancelTarget(RectTransform target)
        {
            for (int i = motions.Count - 1; i >= 0; i--)
                if (motions[i].Target == target) { Restore(motions[i]); motions.RemoveAt(i); }
            if (motions.Count == 0) enabled = false;
        }

        void Update()
        {
            for (int i = motions.Count - 1; i >= 0; i--) {
                var motion = motions[i];
                if (motion.Target == null || !motion.Target.gameObject.activeInHierarchy) {
                    Restore(motion); motions.RemoveAt(i); continue;
                }
                motion.Elapsed += Time.unscaledDeltaTime;
                if (motion.Elapsed >= motion.Duration) { Restore(motion); motions.RemoveAt(i); }
                else Apply(motion, motion.Elapsed / motion.Duration);
            }
            if (motions.Count == 0) enabled = false;
        }

        static void Apply(Motion motion, float progress)
        {
            float ease = 1 - Mathf.Pow(1 - progress, 3);
            if (motion.Kind == FeedbackMotion.Reveal) {
                motion.Target.localScale = Vector3.Scale(motion.Scale,
                    new Vector3(Mathf.Lerp(.96f, 1, ease), Mathf.Lerp(.90f, 1, ease), 1));
                if (motion.Group != null) motion.Group.alpha = motion.Alpha * ease;
            } else {
                float scale = motion.Kind == FeedbackMotion.Press ? Mathf.Lerp(.95f, 1, ease) :
                    1 + .035f * Mathf.Sin(Mathf.PI * progress);
                motion.Target.localScale = motion.Scale * scale;
            }
        }

        static void Restore(Motion motion)
        {
            if (motion.Target != null) motion.Target.localScale = motion.Scale;
            if (motion.Group != null) motion.Group.alpha = motion.Alpha;
        }

        void OnDisable()
        {
            if (motions.Count > 0) CancelAnimations();
            if (!gameObject.activeInHierarchy && OutputSource != null) OutputSource.Stop();
        }
        void OnDestroy() => CancelAnimations();
    }

    // Restore before an animated UI object is hidden or destroyed, without an Update per target.
    public sealed class FeedbackMotionTarget : MonoBehaviour
    {
        internal GameFeedback Owner;
        internal RectTransform Target;
        void OnDisable() { if (Owner != null) Owner.CancelTarget(Target); }
    }
}
