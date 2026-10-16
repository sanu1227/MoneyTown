using System;
using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        RectTransform storyOverlay,storySafe,storyArt,storyDialogue;
        RawImage storyIllustration,storyDetail;
        CanvasGroup storyTextGroup;
        readonly CanvasGroup[] storyCutGroups=new CanvasGroup[2];
        Text storyBody,storyCaption,storyTitle,storyRequirement,storyPageLabel,storyNotice,storySoundWord;
        Button storyNext,storySkip;
        int storyChapter,storyPage;
        bool storyReplay;
        float storyMotionTime,storyPageTime;
        static readonly Color StoryInk=new Color(.12f,.12f,.11f);
        static readonly Color StoryPaper=new Color(.97f,.945f,.88f);
        static readonly float[] StoryDetailFocus={.75f,.66f,.70f,.75f,.68f,.61f};
        bool StoryActive => storyOverlay!=null&&storyOverlay.gameObject.activeSelf;
        bool StoryOpeningActive => StoryActive&&storyChapter==0&&storyPage==0;

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
            storyOverlay=Rect("StoryOverlay",parent);Stretch(storyOverlay);Surface(storyOverlay,StoryPaper,false);
            storyOverlay.gameObject.AddComponent<RectMask2D>();
            storySafe=Rect("StorySafeArea",storyOverlay);PositionStoryWindow();
            var header=Rect("StoryHeader",storySafe);header.anchorMin=new Vector2(0,1);header.anchorMax=Vector2.one;
            header.offsetMin=new Vector2(28,-150);header.offsetMax=new Vector2(-28,-20);
            var brand=Label(header,"MONEY TOWN  /  나의 이야기",18,StoryInk,0);
            brand.rectTransform.anchorMin=new Vector2(0,1);brand.rectTransform.anchorMax=new Vector2(.76f,1);
            brand.rectTransform.offsetMin=new Vector2(0,-28);brand.rectTransform.offsetMax=Vector2.zero;
            storyPageLabel=Label(header,"",21,StoryInk,0,TextAnchor.MiddleRight);storyPageLabel.name="StoryPage";
            storyPageLabel.rectTransform.anchorMin=new Vector2(.77f,1);storyPageLabel.rectTransform.anchorMax=Vector2.one;
            storyPageLabel.rectTransform.offsetMin=new Vector2(0,-28);storyPageLabel.rectTransform.offsetMax=Vector2.zero;
            storyTitle=Label(header,"",36,StoryInk,0);storyTitle.name="StoryTitle";storyTitle.fontStyle=FontStyle.Bold;
            Stretch(storyTitle.rectTransform);storyTitle.rectTransform.offsetMin=new Vector2(0,42);storyTitle.rectTransform.offsetMax=new Vector2(0,-36);
            storyRequirement=Label(header,"",19,Hex("6A6357"),0);storyRequirement.name="StoryRequirement";
            storyRequirement.rectTransform.anchorMin=Vector2.zero;storyRequirement.rectTransform.anchorMax=new Vector2(1,0);
            storyRequirement.rectTransform.offsetMin=new Vector2(0,8);storyRequirement.rectTransform.offsetMax=new Vector2(0,38);
            var rule=Rect("StoryRule",header);rule.anchorMin=Vector2.zero;rule.anchorMax=new Vector2(1,0);
            rule.offsetMin=Vector2.zero;rule.offsetMax=new Vector2(0,3);Surface(rule,StoryInk,false);

            storyDialogue=Rect("StoryDialogue",storySafe);Stretch(storyDialogue);
            storyDialogue.offsetMin=new Vector2(28,206);storyDialogue.offsetMax=new Vector2(-28,-174);
            storyArt=BuildStoryCut(storyDialogue,0,new Vector2(0,.43f),Vector2.one,new Vector2(0,10),Vector2.zero);
            storyIllustration=BuildStoryIllustration(storyArt,"StoryIllustration");
            var lower=BuildStoryCut(storyDialogue,1,Vector2.zero,new Vector2(1,.43f),Vector2.zero,new Vector2(0,-10));
            storyDetail=BuildStoryIllustration(lower,"StoryDetail");

            var captionFrame=Rect("StoryCaptionFrame",storyArt);captionFrame.anchorMin=new Vector2(0,1);captionFrame.anchorMax=Vector2.one;
            captionFrame.offsetMin=new Vector2(18,-102);captionFrame.offsetMax=new Vector2(-18,-18);Surface(captionFrame,StoryInk,false);
            var captionPaper=Rect("StoryCaptionPaper",captionFrame);Stretch(captionPaper);
            captionPaper.offsetMin=Vector2.one*3;captionPaper.offsetMax=-Vector2.one*3;Surface(captionPaper,StoryPaper,false);
            storyCaption=Label(captionPaper,"",25,StoryInk,0);storyCaption.name="StoryCaption";
            Stretch(storyCaption.rectTransform);storyCaption.rectTransform.offsetMin=new Vector2(16,7);storyCaption.rectTransform.offsetMax=new Vector2(-16,-7);

            var bubble=Rect("StoryBubble",lower);bubble.anchorMin=Vector2.zero;bubble.anchorMax=new Vector2(1,0);
            bubble.offsetMin=new Vector2(24,22);bubble.offsetMax=new Vector2(-24,164);
            storyTextGroup=bubble.gameObject.AddComponent<CanvasGroup>();
            var tail=Rect("StoryBubbleTail",bubble);tail.anchorMin=tail.anchorMax=new Vector2(.23f,1);
            tail.sizeDelta=new Vector2(34,34);tail.anchoredPosition=new Vector2(0,-1);tail.localRotation=Quaternion.Euler(0,0,45);Surface(tail,StoryInk,false);
            var bubbleFrame=Rect("StoryBubbleFrame",bubble);Stretch(bubbleFrame);Surface(bubbleFrame,StoryInk);
            var bubblePaper=Rect("StoryBubblePaper",bubbleFrame);Stretch(bubblePaper);bubblePaper.offsetMin=Vector2.one*3;bubblePaper.offsetMax=-Vector2.one*3;Surface(bubblePaper,Color.white);
            var tailPaper=Rect("StoryBubbleTailPaper",bubble);tailPaper.anchorMin=tailPaper.anchorMax=new Vector2(.23f,1);
            tailPaper.sizeDelta=new Vector2(28,28);tailPaper.anchoredPosition=new Vector2(0,-1);tailPaper.localRotation=Quaternion.Euler(0,0,45);Surface(tailPaper,Color.white,false);
            storyBody=Label(bubblePaper,"",29,StoryInk,0,TextAnchor.MiddleCenter);storyBody.name="StoryBody";
            Stretch(storyBody.rectTransform);storyBody.rectTransform.offsetMin=new Vector2(18,12);storyBody.rectTransform.offsetMax=new Vector2(-18,-12);
            storySoundWord=Label(lower,"",25,Color.white,0,TextAnchor.MiddleRight);storySoundWord.name="StorySoundWord";
            storySoundWord.fontStyle=FontStyle.Bold;storySoundWord.rectTransform.anchorMin=new Vector2(.72f,1);storySoundWord.rectTransform.anchorMax=Vector2.one;
            storySoundWord.rectTransform.offsetMin=new Vector2(0,-64);storySoundWord.rectTransform.offsetMax=new Vector2(-22,-18);
            storySoundWord.rectTransform.localRotation=Quaternion.Euler(0,0,-8);
            var ink=storySoundWord.gameObject.AddComponent<Outline>();ink.effectColor=StoryInk;ink.effectDistance=new Vector2(2,-2);

            storyNotice=Label(storySafe,"",19,Hex("A02C2C"),0,TextAnchor.MiddleCenter);storyNotice.name="StoryNotice";
            storyNotice.rectTransform.anchorMin=Vector2.zero;storyNotice.rectTransform.anchorMax=new Vector2(1,0);
            storyNotice.rectTransform.offsetMin=new Vector2(28,126);storyNotice.rectTransform.offsetMax=new Vector2(-28,190);
            var footer=Rect("StoryFooter",storySafe);footer.anchorMin=Vector2.zero;footer.anchorMax=new Vector2(1,0);
            footer.offsetMin=new Vector2(28,28);footer.offsetMax=new Vector2(-28,116);
            storySkip=BuildStoryButton(footer,"StorySkip","건너뛰기",SkipStory,0,.35f,StoryPaper);
            storyNext=BuildStoryButton(footer,"StoryNext","다음",AdvanceStory,.38f,1,Hex("F3C853"));
            storyOverlay.gameObject.SetActive(false);
        }
        RectTransform BuildStoryCut(Transform parent,int index,Vector2 min,Vector2 max,Vector2 insetMin,Vector2 insetMax)
        {
            var cut=Rect("StoryCut-"+index,parent);cut.anchorMin=min;cut.anchorMax=max;cut.offsetMin=insetMin;cut.offsetMax=insetMax;
            Surface(cut,StoryInk,false);storyCutGroups[index]=cut.gameObject.AddComponent<CanvasGroup>();return cut;
        }
        RawImage BuildStoryIllustration(RectTransform cut,string name)
        {
            var rect=Rect(name,cut);Stretch(rect);rect.offsetMin=Vector2.one*4;rect.offsetMax=-Vector2.one*4;
            var image=rect.gameObject.AddComponent<RawImage>();image.raycastTarget=false;return image;
        }
        Button BuildStoryButton(Transform parent,string name,string title,Action action,float left,float right,Color tint)
        {
            var frame=Rect(name+"Frame",parent);frame.anchorMin=new Vector2(left,0);frame.anchorMax=new Vector2(right,1);
            frame.offsetMin=frame.offsetMax=Vector2.zero;Surface(frame,StoryInk);
            var button=Button(frame,title,action,tint,0);button.name=name;var rect=(RectTransform)button.transform;
            Stretch(rect);rect.offsetMin=Vector2.one*3;rect.offsetMax=-Vector2.one*3;
            var label=button.GetComponentInChildren<Text>();label.color=StoryInk;label.fontStyle=FontStyle.Bold;return button;
        }
        void PositionStoryWindow()
        {
            if(storySafe==null)return;
            storySafe.anchorMin=new Vector2(Screen.safeArea.xMin/Screen.width,Screen.safeArea.yMin/Screen.height);
            storySafe.anchorMax=new Vector2(Screen.safeArea.xMax/Screen.width,Screen.safeArea.yMax/Screen.height);
            storySafe.offsetMin=storySafe.offsetMax=Vector2.zero;
            Canvas.ForceUpdateCanvases();
            CropStoryIllustration(storyIllustration,1,new Vector2(.5f,.72f));
            CropStoryIllustration(storyDetail,.72f,new Vector2(.45f,StoryDetailFocus[storyChapter]));
        }
        static void CropStoryIllustration(RawImage image,float zoom,Vector2 focus)
        {
            if(image==null||image.texture==null)return;
            var bounds=image.rectTransform.rect;if(bounds.height<=0||bounds.width<=0)return;
            float aspect=bounds.width/bounds.height,imageAspect=image.texture.width/(float)image.texture.height;
            float width=Mathf.Min(1,aspect/imageAspect)*zoom,height=Mathf.Min(1,imageAspect/aspect)*zoom;
            image.uvRect=new UnityEngine.Rect(Mathf.Clamp(focus.x-width/2,0,1-width),Mathf.Clamp(focus.y-height/2,0,1-height),width,height);
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
            storyIllustration.texture=storyDetail.texture=Resources.Load<Texture2D>("StoryArt/"+entry.ArtName);
            storyTitle.text=entry.Title;storyRequirement.text=(replay?"다시 보는 이야기 · ":"")+entry.Requirement;
            storySkip.GetComponentInChildren<Text>().text=replay?"닫기":"건너뛰기";
            storyNotice.text="";safeArea.gameObject.SetActive(false);storyOverlay.gameObject.SetActive(true);storyOverlay.SetAsLastSibling();
            marketWasRunning=false;scroll.StopMovement();PositionStoryWindow();ShowStoryPage();
        }
        void ShowStoryPage()
        {
            var pages=StoryChapters.All[storyChapter].Pages;string page=pages[storyPage];int split=page.IndexOf('\n');
            storyCaption.text=split<0?"아직, 작은 방에서.":page.Substring(0,split);
            storyBody.text=split<0?page:page.Substring(split+1);
            storyPageLabel.text=(storyPage+1).ToString("00")+" / "+pages.Length.ToString("00");
            storySoundWord.text=storyPage==0?"두근":"";
            storyNext.GetComponentInChildren<Text>().text=storyPage<pages.Length-1?"다음":storyReplay?"다시 보기 마치기":"이야기 마치기";
            storyPageTime=0;storyNotice.text="";
            ApplyStoryCutAnimation(effects.ReducedMotion ? .4f : 0);
            Canvas.ForceUpdateCanvases();
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
            storyOverlay.gameObject.SetActive(false);ApplyStoryCutAnimation(.4f);
            storyMotionTime=storyPageTime=0;marketWasRunning=false;
            safeArea.gameObject.SetActive(!settlementOverlay.gameObject.activeSelf);
        }
        void ApplyStoryCutAnimation(float time)
        {
            for(int i=0;i<storyCutGroups.Length;i++) {
                float progress=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-i*.15f)/.25f));
                storyCutGroups[i].alpha=progress;storyCutGroups[i].transform.localScale=Vector3.one*Mathf.Lerp(.975f,1,progress);
            }
            storyTextGroup.alpha=Mathf.Clamp01((time-.15f)/.25f);
        }
        void UpdateStoryAnimation()
        {
            if(!StoryActive)return;
            if(effects.ReducedMotion){ApplyStoryCutAnimation(.4f);return;}
            if(!UnityEngine.Rendering.SplashScreen.isFinished)return;
            if(appPaused||!appFocused&&!GameRuntime.Verifying)return;
            float elapsed=Mathf.Min(Time.unscaledDeltaTime,.1f);
            storyMotionTime+=elapsed;storyPageTime+=elapsed;ApplyStoryCutAnimation(storyPageTime);
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
