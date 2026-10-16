using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        bool warehouseWorking;
        RectTransform warehouseOverlay;
        void DrawWarehouseOffer()
        {
            var card=Card(content,14,4);card.name="WarehouseOffer";
            Label(card,"물류센터 일일 알바",27,Gold,40);
            Label(card,"일당 "+Money(Market.WarehouseWage)+" · 하루 소모",21,Gold,34);
            if(!blocked&&Progress.FundsExhausted(state))Label(card,"거래를 이어갈 자금이 없습니다",19,Muted,30);
            Label(card,"작업 후 다음 날 결산으로 이동합니다.",18,Muted,30);
            var button=Button(card,"물류센터 알바하기",StartWarehouseWork,Hex("8C6630"),60);
            button.name="WarehouseWorkButton";button.interactable=!blocked&&!state.SettlementPending;
        }
        void StartWarehouseWork()
        {
            if(warehouseWorking||blocked||tutorial!=null||state.SettlementPending)return;
            GameState changed;
            try{changed=Market.WorkWarehouse(state);}
            catch(Exception error){Feedback(error.Message,true);return;}
            // Commit wage and day together before the visual, so closing the app cannot duplicate pay.
            try{store.Save(changed);}
            catch(Exception){Feedback("저장 실패로 알바를 취소했습니다. 저장 공간을 확인하세요.",true);return;}
            state=changed;warehouseWorking=true;marketClock.Reset();marketWasRunning=false;
            effects?.Play(GameCue.Work);
            safeArea.gameObject.SetActive(false);
            StartCoroutine(AnimateWarehouseWork());
        }
        IEnumerator AnimateWarehouseWork()
        {
            warehouseOverlay=Rect("WarehouseOverlay",safeArea.parent);Stretch(warehouseOverlay);Surface(warehouseOverlay,Background,false);
            var panel=Column(warehouseOverlay,24,12);Stretch(panel);
            panel.anchorMin=new Vector2(Screen.safeArea.xMin/Screen.width,Screen.safeArea.yMin/Screen.height);
            panel.anchorMax=new Vector2(Screen.safeArea.xMax/Screen.width,Screen.safeArea.yMax/Screen.height);
            panel.GetComponent<VerticalLayoutGroup>().childAlignment=TextAnchor.MiddleCenter;
            Label(panel,"MONEY TOWN  /  물류센터",19,Mint,34,TextAnchor.MiddleCenter);
            Label(panel,"오늘은 현장에서!",38,Color.white,58,TextAnchor.MiddleCenter);
            Label(panel,(state.Day-1)+"일차 근무 · 일당 "+Money(Market.WarehouseWage),23,Gold,40,TextAnchor.MiddleCenter);
            var sceneRect=Rect("WarehouseScene",panel);Height(sceneRect,350);
            var scene=sceneRect.gameObject.AddComponent<WarehouseScene>();scene.raycastTarget=false;
            var stage=Label(panel,"입고된 상자를 분류하고 있어요",24,null,46,TextAnchor.MiddleCenter);
            var track=Rect("ShiftProgress",panel);Height(track,12);Surface(track,Raised);
            var fill=Rect("Fill",track);Stretch(fill);Surface(fill,Mint).raycastTarget=false;
            Label(panel,"근무를 마치면 다음 날 결산을 확인합니다",18,Muted,46,TextAnchor.MiddleCenter);
            float elapsed=0;int soundStage=0;
            while(elapsed<4.2f) {
                if(!appPaused&&(appFocused||GameRuntime.Verifying))elapsed+=Time.unscaledDeltaTime;
                int currentStage=Math.Min(2,(int)(elapsed/1.4f));
                if(currentStage>soundStage){soundStage=currentStage;effects?.Play(GameCue.Work);}
                scene.Progress=effects!=null&&effects.ReducedMotion?1:Mathf.Clamp01(elapsed/4.2f);scene.SetVerticesDirty();
                fill.anchorMax=new Vector2(Mathf.Clamp01(elapsed/4.2f),1);
                stage.text=elapsed<1.4f?"입고된 상자를 분류하고 있어요":elapsed<3.4f?"상자를 옮겨 출고 준비 중이에요":"근무 완료! 일당 "+Signed(Market.WarehouseWage);
                yield return null;
            }
            warehouseOverlay.gameObject.SetActive(false);Destroy(warehouseOverlay.gameObject);warehouseOverlay=null;
            warehouseWorking=false;safeArea.gameObject.SetActive(true);
            Feedback("물류센터 일당 "+Money(Market.WarehouseWage)+"을 받았습니다. 다음 날 결산을 확인하세요.");RenderTab();scroll.verticalNormalizedPosition=1;
            CompleteFeedback(GameCue.Success);
        }
    }

    // Small original uGUI illustration; motion stays independent of market timing.
    public sealed class WarehouseScene : MaskableGraphic
    {
        public float Progress;
        VertexHelper mesh;
        Rect bounds;
        static Color Tint(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var result);return result;}
        Vector2 Point(float x,float y)=>new Vector2(bounds.xMin+x/600*bounds.width,bounds.yMin+y/350*bounds.height);
        void Box(float x,float y,float w,float h,Color tint)
        {
            int i=mesh.currentVertCount;
            mesh.AddVert(Point(x,y),tint,Vector2.zero);mesh.AddVert(Point(x+w,y),tint,Vector2.zero);
            mesh.AddVert(Point(x+w,y+h),tint,Vector2.zero);mesh.AddVert(Point(x,y+h),tint,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
        void Limb(float x,float y,float endX,float endY,float width,Color tint)
        {
            Vector2 a=new Vector2(x,y),b=new Vector2(endX,endY),n=new Vector2(-(b-a).y,(b-a).x).normalized*width/2;
            int i=mesh.currentVertCount;
            foreach(var p in new[]{a-n,b-n,b+n,a+n})mesh.AddVert(Point(p.x,p.y),tint,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
        void Disc(float x,float y,float radius,Color tint)
        {
            int i=mesh.currentVertCount;mesh.AddVert(Point(x,y),tint,Vector2.zero);
            for(int k=0;k<=24;k++){float a=k*Mathf.PI*2/24;mesh.AddVert(Point(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius),tint,Vector2.zero);if(k>0)mesh.AddTriangle(i,i+k,i+k+1);}
        }
        void Parcel(float x,float y,float size)
        {Box(x,y,size,size,Tint("B47B44"));Box(x+2,y+3,size-4,size-5,Tint("E7AC65"));Box(x+size*.44f,y+3,size*.16f,size-5,Tint("F9D8A0"));Box(x+5,y+size*.35f,size*.23f,8,Tint("FCF3DB"));}
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            mesh=helper;mesh.Clear();bounds=GetPixelAdjustedRect();
            var steel=Tint("365567");var dark=Tint("142B3B");var skin=Tint("F2BE8C");var navy=Tint("213E5C");
            Box(0,0,600,350,dark);Box(0,0,600,80,Tint("244252"));
            for(int i=0;i<4;i++){Box(26+i*150,90,6,260,steel);Box(48+i*145,279,100,39,Tint("436574"));Box(52+i*145,283,92,31,Tint("85C6C8"));}
            Box(25,244,550,7,steel);Box(25,329,550,7,steel);
            for(int i=0;i<7;i++)Parcel(44+i*76,251,41);
            Box(30,76,179,19,steel);Box(47,27,9,49,steel);Box(181,27,9,49,steel);
            for(int i=0;i<7;i++)Disc(41+i*26,84,6,Tint("7194A3"));
            float cycle=Mathf.Repeat(Progress*3,1);int delivered=Mathf.Min(3,(int)(Progress*3));
            Parcel(55+Mathf.Repeat(Progress*90,68),96,42);
            Box(401,41,156,12,Tint("C48D58"));Box(412,24,13,17,steel);Box(530,24,13,17,steel);
            for(int i=0;i<3+delivered;i++)Parcel(413+i%3*44,54+i/3*44,41);
            float travel=cycle<.65f?Mathf.SmoothStep(0,1,cycle/.65f):1-Mathf.SmoothStep(0,1,(cycle-.65f)/.35f);
            float x=213+travel*148,bob=Mathf.Sin(cycle*Mathf.PI*12)*3;
            Disc(x,32,30,Tint("1B3444"));
            float stride=Mathf.Sin(cycle*Mathf.PI*8)*13;
            Limb(x-10,90,x-12+stride,38,13,navy);Limb(x+10,90,x+12-stride,38,13,navy);
            Box(x-22+stride,30,25,10,Tint("101F31"));Box(x+3-stride,30,25,10,Tint("101F31"));
            Box(x-23,88+bob,46,66,Tint("F1B845"));Box(x-18,108+bob,36,7,Tint("F6E9BB"));Box(x-5,89+bob,10,62,navy);
            Disc(x,178+bob,21,skin);Box(x-23,185+bob,46,12,Tint("54C6B7"));Box(x-17,196+bob,34,10,Tint("54C6B7"));
            Disc(x+8,180+bob,2.5f,dark);
            Limb(x-20,145+bob,x+22,130+bob,12,skin);Limb(x+20,145+bob,x+44,132+bob,12,skin);
            if(cycle<.65f)Parcel(x+10,120+bob,40);
            Box(24,14,154,5,Tint("57BEB2"));Box(422,14,154,5,Tint("E2B858"));
        }
    }
}
