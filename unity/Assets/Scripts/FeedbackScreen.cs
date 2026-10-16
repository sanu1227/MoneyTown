using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        const string SoundKey="MoneyTown.Feedback.Sound",VolumeKey="MoneyTown.Feedback.Volume",ReducedMotionKey="MoneyTown.Feedback.ReducedMotion";
        GameFeedback effects;
        void InitializeFeedback()
        {
            effects=gameObject.AddComponent<GameFeedback>();effects.Initialize();
            RestoreFeedbackSettings();UpdateFeedbackActivity();
        }
        void RestoreFeedbackSettings()
        {
            int volume=GameRuntime.GetInt(VolumeKey,50);
            effects.Configure(GameRuntime.GetInt(SoundKey,1)==1,volume>=0&&volume<=100?volume:50,GameRuntime.GetInt(ReducedMotionKey)==1);
        }
        void UpdateFeedbackActivity()=>effects?.SetSuspended(appPaused||!appFocused&&!GameRuntime.Verifying);
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
    }
}
