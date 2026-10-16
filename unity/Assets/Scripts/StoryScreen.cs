using System;
using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        RectTransform storyOverlay,storySafe,storyArt;
        RawImage storyIllustration;
        CanvasGroup storyTextGroup;
        Text storyBody,storyTitle,storyRequirement,storyPageLabel,storyNotice;
        Button storyNext,storySkip;
        int storyChapter,storyPage;
        bool storyReplay;
        float storyMotionTime,storyPageTime;
        bool StoryActive => storyOverlay!=null&&storyOverlay.gameObject.activeSelf;

        bool RestoreStoryProgress()
        {
            if(blocked)return false;
            var changed=state.Copy();Story.Record(changed);
            if(changed.StoryUnlocked==state.StoryUnlocked)return true;
            try {store.Save(changed);state=changed;return true;}
            catch(Exception){message="이야기 해금 내용을 저장하지 못했습니다. 저장 공간을 확인하세요.";feedbackError=true;return false;}
        }
        void BuildStoryWindow(Transform parent)
        {
            storyOverlay=Rect("StoryOverlay",parent);Stretch(storyOverlay);Surface(storyOverlay,Background,false);
            storyOverlay.gameObject.AddComponent<RectMask2D>();
            storyArt=Rect("StoryArt",storyOverlay);Stretch(storyArt);
            var art=Rect("StoryIllustration",storyArt);Stretch(art);
            storyIllustration=art.gameObject.AddComponent<RawImage>();storyIllustration.raycastTarget=false;
            storySafe=Rect("StorySafeArea",storyOverlay);PositionStoryWindow();
            var top=Rect("StoryHeader",storySafe);top.anchorMin=new Vector2(0,1);top.anchorMax=Vector2.one;top.pivot=new Vector2(.5f,1);
            top.offsetMin=new Vector2(22,-92);top.offsetMax=new Vector2(-22,-18);Surface(top,new Color(.04f,.08f,.13f,.88f));
            var brand=Label(top,"MONEY TOWN  /  STORY",20,Mint,0);brand.rectTransform.anchorMin=Vector2.zero;brand.rectTransform.anchorMax=new Vector2(.56f,1);
            brand.rectTransform.offsetMin=new Vector2(16,0);brand.rectTransform.offsetMax=Vector2.zero;
            storySkip=Button(top,"이번 장 건너뛰기",SkipStory,Raised,0);storySkip.name="StorySkip";
            var skip=(RectTransform)storySkip.transform;skip.anchorMin=new Vector2(.57f,0);skip.anchorMax=Vector2.one;skip.offsetMin=new Vector2(0,7);skip.offsetMax=new Vector2(-7,-7);
            var panel=Column(storySafe,24,10);panel.name="StoryDialogue";
            panel.anchorMin=Vector2.zero;panel.anchorMax=new Vector2(1,0);panel.pivot=new Vector2(.5f,0);
            panel.offsetMin=new Vector2(22,24);panel.offsetMax=new Vector2(-22,480);Surface(panel,new Color(.035f,.075f,.12f,.96f));
            storyRequirement=Label(panel,"",19,Mint,28);
            storyTitle=Label(panel,"",32,Gold,46);storyTitle.name="StoryTitle";
            storyBody=Label(panel,"",27,Color.white,160,TextAnchor.UpperLeft);storyBody.name="StoryBody";
            storyTextGroup=storyBody.gameObject.AddComponent<CanvasGroup>();
            storyNotice=Label(panel,"",18,Red,48);storyNotice.name="StoryNotice";
            var footer=Row(panel,76,12);
            storyPageLabel=Label(footer,"",21,Muted,76);storyPageLabel.name="StoryPage";Width(storyPageLabel.rectTransform,100);
            storyNext=Button(footer,"다음",AdvanceStory,Hex("266957"),76);storyNext.name="StoryNext";
            storyOverlay.gameObject.SetActive(false);
        }
        void PositionStoryWindow()
        {
            if(storySafe==null)return;
            storySafe.anchorMin=new Vector2(Screen.safeArea.xMin/Screen.width,Screen.safeArea.yMin/Screen.height);
            storySafe.anchorMax=new Vector2(Screen.safeArea.xMax/Screen.width,Screen.safeArea.yMax/Screen.height);
            storySafe.offsetMin=storySafe.offsetMax=Vector2.zero;
            if(storyIllustration==null||storyIllustration.texture==null)return;
            float screenAspect=Screen.width/(float)Screen.height;
            float imageAspect=storyIllustration.texture.width/(float)storyIllustration.texture.height;
            float width=Mathf.Min(1,screenAspect/imageAspect),height=Mathf.Min(1,imageAspect/screenAspect);
            storyIllustration.uvRect=new UnityEngine.Rect((1-width)/2,(1-height)/2,width,height);
        }
        void TryShowStory()
        {
            if(storyOverlay==null||StoryActive||state==null||blocked||warehouseWorking||state.SettlementPending||resetPending)return;
            int chapter=Story.Next(state);
            if(chapter<0||tab>=3||(chapter>0&&tutorial!=null))return;
            ShowStory(chapter,false);
        }
        void ShowStory(int chapter,bool replay)
        {
            if(StoryActive||blocked||warehouseWorking||state.SettlementPending||chapter<0||chapter>=state.StoryUnlocked)return;
            storyChapter=chapter;storyPage=0;storyReplay=replay;storyMotionTime=0;
            var entry=StoryChapters.All[chapter];
            storyIllustration.texture=Resources.Load<Texture2D>("StoryArt/"+entry.ArtName);
            storyTitle.text=entry.Title;storyRequirement.text=(replay?"다시 보는 이야기 · ":"")+entry.Requirement;
            storySkip.GetComponentInChildren<Text>().text=replay?"닫기":"이번 장 건너뛰기";
            storyNotice.text="";safeArea.gameObject.SetActive(false);storyOverlay.gameObject.SetActive(true);storyOverlay.SetAsLastSibling();
            marketWasRunning=false;scroll.StopMovement();PositionStoryWindow();ShowStoryPage();
        }
        void ShowStoryPage()
        {
            var pages=StoryChapters.All[storyChapter].Pages;
            storyBody.text=pages[storyPage];storyPageLabel.text=(storyPage+1).ToString("00")+" / "+pages.Length.ToString("00");
            storyNext.GetComponentInChildren<Text>().text=storyPage<pages.Length-1?"다음":storyReplay?"다시 보기 마치기":"이야기 마치기";
            storyPageTime=0;storyTextGroup.alpha=effects.ReducedMotion?1:0;storyNotice.text="";
            Canvas.ForceUpdateCanvases();UpdateStoryAnimation();
        }
        void AdvanceStory()
        {
            if(!StoryActive)return;
            if(storyPage<StoryChapters.All[storyChapter].Pages.Length-1){storyPage++;ShowStoryPage();}
            else FinishStory();
        }
        void SkipStory(){if(StoryActive)FinishStory();}
        void FinishStory()
        {
            if(!storyReplay) {
                try {var changed=state.Copy();Story.Record(changed);changed=Story.Acknowledge(changed,storyChapter);store.Save(changed);state=changed;}
                catch(Exception){storyNotice.text="이야기 진행을 저장하지 못했습니다.\n저장 공간을 확인한 뒤 다시 눌러 주세요.";effects.Play(GameCue.Error);return;}
            }
            CloseStoryScreen();RenderTab();
        }
        void CloseStoryScreen()
        {
            if(storyOverlay==null)return;
            storyOverlay.gameObject.SetActive(false);storyArt.localScale=Vector3.one;storyArt.anchoredPosition=Vector2.zero;
            storyTextGroup.alpha=1;storyMotionTime=storyPageTime=0;marketWasRunning=false;
            safeArea.gameObject.SetActive(!settlementOverlay.gameObject.activeSelf);
        }
        void UpdateStoryAnimation()
        {
            if(!StoryActive)return;
            if(effects.ReducedMotion){storyArt.localScale=Vector3.one;storyArt.anchoredPosition=Vector2.zero;storyTextGroup.alpha=1;return;}
            if(appPaused||!appFocused&&!GameRuntime.Verifying)return;
            float elapsed=Mathf.Min(Time.unscaledDeltaTime,.1f);
            storyMotionTime+=elapsed;storyPageTime+=elapsed;
            storyArt.localScale=Vector3.one*(1.02f+.025f*(.5f-.5f*Mathf.Cos(storyMotionTime*.2f)));
            storyArt.anchoredPosition=new Vector2(6*Mathf.Sin(storyMotionTime*.15f),0);
            storyTextGroup.alpha=Mathf.Clamp01(storyPageTime/.3f);
        }
        void DrawStoryLibrary(Transform parent)
        {
            var card=Card(parent,20,10);card.name="StoryLibrary";
            Label(card,"나의 이야기",27,Mint,44);
            Label(card,"총자산으로 새로운 장을 엽니다. 해금한 이야기는 언제든 다시 볼 수 있습니다.",20,Muted,70);
            for(int i=0;i<StoryChapters.All.Length;i++) {
                int chapter=i;var entry=StoryChapters.All[i];bool unlocked=i<state.StoryUnlocked;
                var button=Button(card,entry.Title+"\n"+(unlocked?"다시 보기":entry.Requirement+"에 열림"),()=>ShowStory(chapter,true),unlocked?Raised:Panel,88);
                button.name="StoryReplay-"+i;button.interactable=unlocked&&!blocked;
            }
        }
    }
}
