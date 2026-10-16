using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        const string SoundKey="MoneyTown.Feedback.Sound",VolumeKey="MoneyTown.Feedback.Volume",ReducedMotionKey="MoneyTown.Feedback.ReducedMotion";
        const string MusicKey="MoneyTown.Music.Enabled",MusicVolumeKey="MoneyTown.Music.Volume";
        GameFeedback effects;
        GameMusic music;
        Text musicNowPlaying;
        void InitializeFeedback()
        {
            effects=gameObject.AddComponent<GameFeedback>();effects.Initialize();
            music=gameObject.AddComponent<GameMusic>();music.Initialize();
            RestoreFeedbackSettings();RestoreMusicSettings();UpdateFeedbackActivity();
        }
        void RestoreFeedbackSettings()
        {
            int volume=GameRuntime.GetInt(VolumeKey,50);
            effects.Configure(GameRuntime.GetInt(SoundKey,1)==1,volume>=0&&volume<=100?volume:50,GameRuntime.GetInt(ReducedMotionKey)==1);
        }
        void RestoreMusicSettings()
        {
            int volume=GameRuntime.GetInt(MusicVolumeKey,25);
            music.Configure(GameRuntime.GetInt(MusicKey,1)==1,volume>=0&&volume<=100?volume:25);
        }
        void UpdateFeedbackActivity()
        {
            bool suspended=appPaused||!appFocused&&!GameRuntime.Verifying;
            effects?.SetSuspended(suspended);music?.SetSuspended(suspended);
            if(musicNowPlaying!=null&&musicNowPlaying.text!=music.CurrentTitle)musicNowPlaying.text=music.CurrentTitle;
        }
        void BindButtonFeedback(Button button,Action action)
        {
            button.onClick.AddListener(()=>{
                if(!button.interactable)return;
                effects?.Play(GameCue.Tap);effects?.Animate((RectTransform)button.transform,FeedbackMotion.Press);
                action();
            });
        }
        void CompleteFeedback(GameCue cue)
        {
            effects?.Play(cue);
            effects?.Animate(cash.rectTransform,FeedbackMotion.Pop);effects?.Animate(total.rectTransform,FeedbackMotion.Pop);
            if(cue==GameCue.Buy||cue==GameCue.Sell)effects?.Animate(tradeDock,FeedbackMotion.Pop);
            if(cue==GameCue.Paper)RevealPaper();
        }
        void RevealPaper()
        {
            var paper=content.GetComponentInChildren<PaperSheet>();
            if(paper!=null)effects?.Animate(paper.rectTransform,FeedbackMotion.Reveal);
        }
        void AnimateQuote()
        {
            var quote=content.GetComponentsInChildren<Text>().FirstOrDefault(label=>label.name=="TradingPrice");
            if(quote!=null)effects?.Animate(quote.rectTransform,FeedbackMotion.Pop);
        }
        void SetFeedbackSettings(bool sound,int volume,bool reduced)
        {
            string[] keys={SoundKey,VolumeKey,ReducedMotionKey};
            int[] previous=keys.Select(key=>GameRuntime.GetInt(key)).ToArray();bool[] existed=keys.Select(GameRuntime.HasKey).ToArray();
            try {
                GameRuntime.SetInt(SoundKey,sound?1:0);GameRuntime.SetInt(VolumeKey,volume);GameRuntime.SetInt(ReducedMotionKey,reduced?1:0);
                GameRuntime.SavePreferences();
            } catch(Exception) {
                for(int i=0;i<keys.Length;i++){if(existed[i])GameRuntime.SetInt(keys[i],previous[i]);else GameRuntime.DeleteKey(keys[i]);}
                Feedback("설정을 저장하지 못했습니다. 다시 시도하세요.",true);return;
            }
            RestoreFeedbackSettings();RenderTab();effects.Play(GameCue.Success);
        }
        void DrawFeedbackSettings(Transform parent)
        {
            var card=Card(parent,20,10);card.name="FeedbackSettings";
            Label(card,"소리와 애니메이션",27,Mint,44);
            Button(card,"효과음 · "+(effects.SoundEnabled?"켜짐":"꺼짐"),()=>SetFeedbackSettings(!effects.SoundEnabled,effects.VolumePercent,effects.ReducedMotion),Raised,60).name="FeedbackSound";
            Label(card,"효과음 음량 · "+effects.VolumePercent+"%",21,Muted,36);
            var row=Row(card,58,6);
            foreach(int level in new[]{0,25,50,75,100}) {
                int volume=level;
                Button(row,volume+"%",()=>SetFeedbackSettings(effects.SoundEnabled,volume,effects.ReducedMotion),effects.VolumePercent==volume?Hex("266957"):Raised,58).name="FeedbackVolume-"+volume;
            }
            Button(card,"동작 줄이기 · "+(effects.ReducedMotion?"켜짐":"꺼짐"),()=>SetFeedbackSettings(effects.SoundEnabled,effects.VolumePercent,!effects.ReducedMotion),Raised,60).name="FeedbackReducedMotion";
            Label(card,"동작 줄이기를 켜면 버튼·창·시세의 움직임을 끄고 물류센터를 정지 장면으로 표시합니다. 작업 시간과 보상은 같습니다.",18,Muted,84);
            Button(card,"효과음 미리듣기",()=>effects.Play(GameCue.Buy),Hex("266957"),58).name="FeedbackPreview";
        }
        void SetMusicSettings(bool enabled,int volume)
        {
            string[] keys={MusicKey,MusicVolumeKey};
            int[] previous=keys.Select(key=>GameRuntime.GetInt(key)).ToArray();bool[] existed=keys.Select(GameRuntime.HasKey).ToArray();
            try {
                GameRuntime.SetInt(MusicKey,enabled?1:0);GameRuntime.SetInt(MusicVolumeKey,volume);GameRuntime.SavePreferences();
            } catch(Exception) {
                for(int i=0;i<keys.Length;i++){if(existed[i])GameRuntime.SetInt(keys[i],previous[i]);else GameRuntime.DeleteKey(keys[i]);}
                Feedback("음악 설정을 저장하지 못했습니다. 다시 시도하세요.",true);return;
            }
            RestoreMusicSettings();RenderTab();effects.Play(GameCue.Success);
        }
        void DrawMusicSettings(Transform parent)
        {
            var card=Card(parent,20,10);card.name="MusicSettingsCard";
            Label(card,"배경음악",27,Mint,44);
            musicNowPlaying=Label(card,music.CurrentTitle,22,Gold,36);musicNowPlaying.name="MusicNowPlaying";
            Label(card,music.TrackCount+"곡 랜덤 재생 · 한 바퀴마다 순서 변경",18,Muted,32);
            Button(card,"다음 곡",()=>music.NextTrack(),Raised,58).name="MusicNext";
            Button(card,"배경음악 · "+(music.MusicEnabled?"켜짐":"꺼짐"),()=>SetMusicSettings(!music.MusicEnabled,music.VolumePercent),Raised,60).name="MusicEnabled";
            Label(card,"음악 음량 · "+music.VolumePercent+"%",21,Muted,36);
            var row=Row(card,58,6);
            foreach(int level in new[]{0,25,50,75,100}) {
                int volume=level;
                Button(row,volume+"%",()=>SetMusicSettings(music.MusicEnabled,volume),music.VolumePercent==volume?Hex("266957"):Raised,58).name="MusicVolume-"+volume;
            }
            Label(card,"같은 곡이 연속으로 나오지 않습니다. 효과음과 따로 조절하며, 다른 화면에서도 이어집니다.",18,Muted,66);
        }
    }
}
