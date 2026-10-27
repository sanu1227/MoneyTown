using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        // Read only for migrating an unfinished pre-0.30 practice session.
        const string TutorialModeKey="MoneyTown.Tutorial.v2.Mode",TutorialStepKey="MoneyTown.Tutorial.v2.Step";
        bool TutorialActive => state!=null&&TutorialGuide.Active(state);
        RectTransform guideBanner;
        readonly List<Outline> guideOutlines=new List<Outline>();
        static readonly TutorialAction[] GuideActions={TutorialAction.OpenCompanies,TutorialAction.SelectCompany,TutorialAction.Buy,
            TutorialAction.OpenInformation,TutorialAction.OpenOffer,TutorialAction.BuyInformation,TutorialAction.ReturnExchange,
            TutorialAction.NextDay,TutorialAction.CloseSettlement,TutorialAction.Sell,TutorialAction.OpenAssets,TutorialAction.Deposit,TutorialAction.Finish};
        static readonly string[] GuideTitles={"관심 있는 기업 찾기","첫 종목 고르기","첫 주식 1주 매수","정보 가판대 둘러보기","가격과 신뢰도 확인","정보를 직접 구매","읽은 단서를 생각해 보기","다음 날 시장 열기","변동 원인 확인","보유 주식 1주 매도","내 자산 확인","다른 투자처 알아보기","첫 투자를 마쳤어요"};
        static readonly string[] GuideBodies={
            "실제 게임에서 시작합니다. 금빛 ‘종목 선택’을 눌러 보세요. 거래한 돈과 주식은 그대로 남습니다.",
            "금빛 기업 중 하나를 골라 보세요. 첫 거래는 현재 현금으로 살 수 있는 종목 1주로 시작합니다.",
            "차트와 가격을 보고 ‘매수’를 누르세요. 수수료가 포함된 표시 금액이 실제 현금에서 빠져나갑니다.",
            "보유 주식을 확인했습니다. 아래 ‘정보 가판대’를 눌러 투자 판단에 쓸 자료를 살펴보세요.",
            "오늘 가장 저렴한 자료를 강조했습니다. 카드를 눌러 가격과 신뢰도부터 확인하세요.",
            "아래 ‘봉인 해제’를 누르면 표시된 비용을 내고 내용을 읽습니다. 신뢰도는 수익 확률이 아닙니다.",
            "기사를 읽고 어떤 기업과 연결되는지 생각해 보세요. 다 읽었다면 아래 ‘거래소’를 누르세요.",
            "위의 ‘다음 날’을 눌러 보세요. 하루가 지나고 사건이 반영된 새 주가와 전날 결산이 나옵니다.",
            "뉴스의 원인과 실제 변동률을 살펴보세요. 오를 수도, 내릴 수도 있습니다. 확인했으면 결산을 닫으세요.",
            "내 보유 수량과 손익을 확인하고 ‘매도’를 누르세요. 보유한 1주를 팔아 실제 현금으로 돌려받습니다.",
            "첫 매매를 마쳤습니다. 아래 ‘내 자산’을 눌러 현금과 투자 결과를 확인하세요.",
            "예금은 10만원을 3일 맡기면 10만500원을 받습니다. 가입하거나 ‘안내 마치기’를 선택하세요.",
            "이제 직접 기업과 정보를 골라 보세요. 거래 내역은 내 자산에 남으며 도움말에서 안내를 다시 켤 수 있습니다."};
        void RestoreTutorial()
        {
            ResetGuideExplanation();
            if(blocked||store==null)return;
            var changed=state.Copy();
            if(store.LoadedVersion<13&&GameRuntime.GetInt(TutorialModeKey,2)==1)changed=TutorialGuide.Start(changed,true);
            NormalizeTutorial(changed);
            if(changed.TutorialActive!=state.TutorialActive||changed.TutorialStep!=state.TutorialStep||changed.TutorialOffer!=state.TutorialOffer) {
                try{store.Save(changed);state=changed;}
                catch(Exception){message="투자 안내를 복원하지 못했습니다. 도움말에서 다시 시작해 주세요.";feedbackError=true;}
            }
            PrepareTutorialView();
        }
        void NormalizeTutorial(GameState changed)
        {
            if(!TutorialGuide.Active(changed))return;
            if(changed.TutorialStep==5&&Intelligence.Owned(changed,changed.TutorialDay,changed.TutorialOffer)!=null)changed.TutorialStep=6;
            if(changed.TutorialStep==5&&changed.TutorialDay!=changed.Day){changed.TutorialStep=4;changed.TutorialOffer=-1;}
            if(changed.TutorialStep==6&&Intelligence.Owned(changed,changed.TutorialDay,changed.TutorialOffer)==null)changed.TutorialStep=7;
            if(changed.TutorialStep==8&&!changed.SettlementPending)changed.TutorialStep=9;
            if(changed.TutorialStep==9&&changed.Stocks[changed.TutorialCompany].Quantity==0)changed.TutorialStep=10;
            if(changed.TutorialStep==11&&changed.DepositDue>0)changed.TutorialStep=12;
        }
        void StartTutorial(bool restart)
        {
            if(blocked||StoryActive||state.SettlementPending||GuideExplanationOpen)return;
            var changed=TutorialGuide.Start(state,restart);NormalizeTutorial(changed);
            if(!SaveGuideState(changed))return;
            ResetGuideExplanation();
            resetPending=false;RenderTab();
        }
        bool SaveGuideState(GameState changed)
        {
            try{store.Save(changed);state=changed;return true;}
            catch(Exception){Feedback("안내 진행을 저장하지 못했습니다. 다시 눌러 주세요.",true);ShowGuideExplanationSaveError();return false;}
        }
        void LeaveTutorial(bool help)
        {
            if(TutorialActive&&!SaveGuideState(TutorialGuide.Pause(state)))return;
            ResetGuideExplanation();
            tab=help?3:0;RenderTab();scroll.verticalNormalizedPosition=1;
        }
        void FinishTutorial()
        {
            if(!TutorialActive||state.TutorialStep<11||GuideExplanationOpen)return;
            var changed=state.Copy();changed.TutorialStep=TutorialGuide.Count;changed.TutorialActive=false;
            if(!SaveGuideState(changed))return;
            ResetGuideExplanation();
            message="첫 투자 안내 완료! 이제 직접 판단하며 투자해 보세요.";flashUntil=Time.unscaledTime+5;
            RenderTab();scroll.verticalNormalizedPosition=1;
        }
        void OpenHelp()
        {
            if(StoryActive||state.SettlementPending||GuideExplanationOpen)return;
            if(TutorialActive){LeaveTutorial(true);return;}
            tab=3;resetPending=false;RenderTab();scroll.verticalNormalizedPosition=1;
        }
        int GuideOfferIndex => Intelligence.Offers(state).OrderBy(item=>item.Price).ThenBy(item=>item.Index).First().Index;
        bool TutorialAllows(TutorialAction action,int value=-1)
        {
            if(!TutorialActive)return true;
            if(GuideExplanationOpen)return false;
            if(state.SettlementPending)return action==TutorialAction.CloseSettlement;
            if(GuideActions[state.TutorialStep]!=action)return false;
            if(action==TutorialAction.SelectCompany)return value>=0&&value<state.Stocks.Length&&OrderGuidance.MaximumBuy(state,value)>0;
            if(action==TutorialAction.OpenOffer)return value==GuideOfferIndex;
            if(action==TutorialAction.BuyInformation)return value==state.TutorialOffer;
            if(action==TutorialAction.Buy||action==TutorialAction.Sell)return value==state.TutorialCompany&&Quantity()==1;
            return true;
        }
        bool MoveTutorial(TutorialAction action,int value=-1)
        {
            if(!TutorialActive||!TutorialAllows(action,value))return false;
            var changed=state.Copy();TutorialGuide.Record(changed,action,value);
            if(action==TutorialAction.OpenOffer&&Intelligence.Owned(changed,changed.Day,value)!=null)
                TutorialGuide.Record(changed,TutorialAction.BuyInformation,value);
            NormalizeTutorial(changed);
            if(!SaveGuideState(changed))return false;
            RenderTab();return true;
        }
        void PrepareTutorialView()
        {
            if(!TutorialActive)return;
            int step=state.TutorialStep;selected=state.TutorialCompany;quantityText="1";
            choosingCompany=step==1;resetPending=false;openedInfo=null;
            if(state.SettlementPending){tab=4;return;}
            tab=step>=11?2:step>=4&&step<=6?1:0;
            infoShelf=0;informationBand=0;
            openedClue=step==5||step==6?(int?)state.TutorialOffer:null;
            openedClueDay=state.TutorialDay;openedClueVersion=Intelligence.CurrentVersion;
        }
        void DrawTutorialGuide()
        {
            foreach(var outline in guideOutlines)if(outline!=null){outline.enabled=false;Destroy(outline);}
            guideOutlines.Clear();Clear(guideBanner);
            guideBanner.gameObject.SetActive(TutorialActive);
            if(!TutorialActive)return;
            int step=state.SettlementPending?8:state.TutorialStep;
            var row=Row(guideBanner,74,10);
            var heading=Column(row,0,1);Flexible(heading);
            Label(heading,"첫 투자 안내  "+(state.TutorialStep+1)+" / "+TutorialGuide.Count,17,Mint,24);
            Label(heading,GuideTitles[step],25,Gold,42).name="GuideTitle";
            var review=Button(row,"설명\n다시 보기",OpenGuideExplanation,Raised,74);review.name="GuideExplainReview";Width((RectTransform)review.transform,100);
            var pause=Button(row,step>=11?"안내 마치기":"나중에",step>=11?(Action)FinishTutorial:()=>LeaveTutorial(false),Raised,74);
            pause.name=step>=11?"GuideFinish":"GuidePause";Width((RectTransform)pause.transform,144);
            string body=GuideBodies[step];
            if(step==2&&OrderGuidance.MaximumBuy(state,selected)==0)body="1주를 살 현금이 부족합니다. ‘나중에’를 누르고 내 자산의 물류센터 알바로 자금을 모은 뒤 이어 하세요.";
            if(step==5&&state.Cash<Intelligence.Offer(state.Day,state.Seed,state.TutorialOffer,eventEdition:state.EventEdition).Price)
                body="이 정보의 구매 비용이 부족합니다. ‘나중에’를 누르고 내 자산에서 알바한 뒤 이어 하세요. 구매는 아직 이루어지지 않았습니다.";
            Label(guideBanner,body,20,Color.white,68).name="GuideBody";
        }
        bool GuideTarget(Selectable control)
        {
            if(state.SettlementPending)return control.name=="SettlementClose"||control.name=="SettlementConfirm";
            switch(state.TutorialStep) {
                case 0:return control.name=="CompanyPicker";
                case 1:return control.name.StartsWith("Company-",StringComparison.Ordinal)&&int.TryParse(control.name.Substring(8),out int company)&&company>=0&&company<state.Stocks.Length&&OrderGuidance.MaximumBuy(state,company)>0;
                case 2:return control==buy;
                case 3:return control==navigation[1];
                case 4:return control.name=="Offer-"+GuideOfferIndex;
                case 5:return control.name=="BuyClue";
                case 6:return control==navigation[0];
                case 7:return control==next;
                case 9:return control==sell;
                case 10:return control==navigation[2];
                case 11:return control.name=="DepositMoney";
                case 12:return control.name=="GuideFinish";
                default:return false;
            }
        }
        void ApplyTutorialGuide()
        {
            if(!TutorialActive)return;
            RectTransform focus=null;
            var controls=safeArea.GetComponentsInChildren<Selectable>().Concat(settlementWindow.GetComponentsInChildren<Selectable>());
            foreach(var control in controls) {
                if(!control.gameObject.activeInHierarchy)continue;
                bool target=GuideTarget(control);
                control.interactable=control.interactable&&(target||control==help||control.name=="GuidePause"||control.name=="GuideFinish"||control.name=="GuideExplainReview");
                if(!target||!control.interactable)continue;
                var outline=control.gameObject.AddComponent<Outline>();outline.effectColor=Gold;outline.effectDistance=new Vector2(3,-3);guideOutlines.Add(outline);
                if(focus==null&&control.transform.IsChildOf(content))focus=(RectTransform)control.transform;
            }
            if(focus!=null) {
                Canvas.ForceUpdateCanvases();
                float distance=content.rect.height-scroll.viewport.rect.height;
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(content,focus);
                scroll.StopMovement();
                scroll.verticalNormalizedPosition=distance<=0?1:1-Mathf.Clamp01((-bounds.center.y-scroll.viewport.rect.height*.5f)/distance);
            } else scroll.verticalNormalizedPosition=1;
        }
        void GuideText(Transform parent,string value,Color? tint=null)
        {var text=Label(parent,value,23,tint??Muted,0,TextAnchor.UpperLeft);text.resizeTextForBestFit=false;}
        void LessonNote(string title,string text,int icon=7)
        {
            var note=Card(content,20,10);var row=Row(note,48);Icon(row,icon,Gold,42);Flexible(Label(row,title,25,Gold,42).rectTransform);GuideText(note,text);
        }
        void DrawHelp()
        {
            Label(content,"플레이 도움말",30,null,52);
            var practice=Card(content,22,10);Label(practice,"실제 화면에서 첫 투자",27,Mint,44);GuideText(practice,"금빛으로 강조한 버튼을 직접 누르며 배웁니다. 매매·정보 구매·예금은 실제 게임 자산에 반영됩니다. 안내를 닫아도 거래는 유지됩니다.");
            int step=state.TutorialStep;
            if(step>0&&step<TutorialGuide.Count)Button(practice,"안내 이어하기 · "+(step+1)+" / "+TutorialGuide.Count,()=>StartTutorial(false),Hex("266957"));
            Button(practice,step==0?"첫 투자 안내 시작":"안내 처음부터",()=>StartTutorial(true),Raised);
            DrawStoryLibrary(content);
            LessonNote("현금과 총자산","현금은 지금 쓸 수 있는 돈입니다. 총자산은 현금에 보유 주식의 현재 가치와 예금 원금을 더한 금액입니다.",6);
            LessonNote("주식을 사고팔기","거래소의 ‘종목 선택’을 눌러 "+Market.Companies.Length+"개 기업을 둘러보고 종목과 수량을 고르세요. 버튼 금액에는 0.1% 수수료가 포함되며 1원 미만은 올림합니다.",9);
            LessonNote("봉인 정보와 기업 분석","봉인 정보는 대상을 숨기고 판매합니다. 1~10등급 중 10등급이 가장 구체적이고 비쌉니다. 기업 분석은 원하는 기업을 지정하는 고가 정보입니다.");
            LessonNote("정보와 개장 시세","정보가 다룬 사건은 다음 날 개장 시세에 반영됩니다. 장중 자동 갱신은 사건 확인 전 움직임입니다. 다음 날 보관함의 ‘정보와 주가 대조’에서 실제 사건과 종가·시가를 함께 확인하세요.");
            LessonNote("신뢰도와 후속 보도","신뢰도는 내용이 사실일 확률입니다. 정보가 틀리면 실제 사건은 반대일 수 있습니다. 맞는 정보도 수급·장중 변동·수수료·정보 비용에 따라 손실로 이어질 수 있습니다.");
            LessonNote("시세 간격과 결산","상단 ‘설정’에서 30초·1분·3분·5분·15분·30분·1시간을 선택하세요. 남은 시간은 거래소 차트 위에 표시합니다. 장중 1회에 약 0.1~0.6% 움직입니다. 다음 날은 전날 결산 뒤 최소 1.5%의 큰 개장 변동을 적용합니다. 최저가와 가격 반올림에서는 예외가 있습니다. 도움말·첫 투자 안내·결산·백그라운드에서는 자동 시세가 멈춥니다.",9);
            LessonNote("다음 날과 예금","날짜는 상단 ‘다음 날’ 버튼으로 진행합니다. 10만원 예금은 3일 뒤 10만 500원을 돌려주며 만기 전 해지는 불가합니다.",8);
            LessonNote("물류센터 알바","내 자산에서 알바할 수 있습니다. 보유 금액 제한 없이 하루를 보내고 일당 "+Money(Market.WarehouseWage)+"을 받습니다. 상자를 옮기는 작업 장면 뒤 다음 날 결산이 열리며, 주가와 정보·예금 만기도 함께 진행됩니다.",4);
            Button(content,"거래소로 돌아가기",()=>SwitchTab(0),Raised);
        }
    }
}
