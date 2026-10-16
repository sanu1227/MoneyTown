using UnityEngine;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        AudioSource storyOpeningAudio;
        bool storyAudioRequested,storyAudioStarted,storyAudioPaused;

        void BeginOpeningAudio()
        {
            StopOpeningAudio();
            if(storyOpeningAudio==null) {
                storyOpeningAudio=gameObject.AddComponent<AudioSource>();
                storyOpeningAudio.playOnAwake=false;storyOpeningAudio.loop=false;
                storyOpeningAudio.spatialBlend=0;storyOpeningAudio.pitch=1;
                storyOpeningAudio.clip=Resources.Load<AudioClip>("Audio/story-opening");
            }
            if(storyOpeningAudio.clip==null)return;
            storyAudioRequested=true;
            UpdateOpeningAudio();
        }
        void UpdateOpeningAudio()
        {
            if(!storyAudioRequested||storyOpeningAudio==null)return;
            if(!StoryOpeningActive||!isActiveAndEnabled){StopOpeningAudio();return;}
            storyOpeningAudio.volume=effects.VolumePercent/100f;
            bool allowed=effects.SoundEnabled&&effects.VolumePercent>0&&!effects.IsSuspended&&
                UnityEngine.Rendering.SplashScreen.isFinished;
            if(!allowed) {
                if(storyOpeningAudio.isPlaying) {
                    storyOpeningAudio.Pause();storyAudioPaused=true;
                }
                music.SetDucked(false);return;
            }
            if(!storyAudioStarted) {
                storyOpeningAudio.Play();storyAudioStarted=true;
            } else if(storyAudioPaused) {
                // UnPause preserves position; Play would restart a voice that ended during Pause.
                storyOpeningAudio.UnPause();
                storyAudioPaused=false;
            }
            music.SetDucked(storyOpeningAudio.isPlaying);
        }
        void StopOpeningAudio()
        {
            if(storyOpeningAudio!=null)storyOpeningAudio.Stop();
            storyAudioRequested=storyAudioStarted=storyAudioPaused=false;
            music?.SetDucked(false);
        }
        void OnDisable(){StopOpeningAudio();}
    }
}
