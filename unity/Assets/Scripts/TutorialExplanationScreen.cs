using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        RectTransform guideExplanationOverlay,guideExplanationSafe,guideExplanationWindow;
        CanvasGroup guideGameInput,guideSettlementInput;
        Text guideExplanationTitle,guideExplanationBody,guideExplanationResult,guideExplanationAction,guideExplanationProgress;
        Button guideExplainContinue;
        int guideExplanationStep=-1;
        bool guideExplanationRequested;
        string guideExplanationError="";
        bool GuideExplanationOpen => TutorialActive&&!StoryActive&&guideExplanationRequested;
        int GuideExplanationStage => state.SettlementPending?8:state.TutorialStep;
        static readonly string[] GuideLessons={
            "거래소는 기업의 주식을 사고파는 곳이에요. 먼저 기업 목록을 열어 어떤 회사가 있는지 함께 살펴볼게요. 이 안내에서 거래한 돈과 주식은 실제 게임에 남아요.",
            "종목은 거래할 회사의 주식을 뜻해요. 회사를 고르면 가격과 차트를 볼 수 있고, 선택만으로 돈이 나가지는 않아요. 처음에는 부담을 줄여 1주부터 시작해 볼게요.",
            "매수는 현금을 내고 주식을 사는 거예요. 아래 금액에는 0.1% 거래 수수료가 이미 포함돼 있어요. 매수하면 현금이 줄고, 그만큼 주식을 보유하게 돼요.",
            "이제 보유 주식은 가격이 오르면 가치가 늘고, 내리면 줄어요. 어떤 일이 기업에 영향을 줄지 알아보기 위해 정보 가판대를 살펴볼게요. 정보를 살 때도 비용이 들어요.",
            "봉인 정보는 사기 전에는 기업과 내용이 숨겨져 있어요. 등급은 정보의 깊이, 신뢰도는 내용이 맞을 가능성을 뜻해요. 지금은 구매 방법을 익히려고 가장 싼 자료를 골랐어요.",
            "신뢰도는 정보가 맞을 가능성이지, 돈을 벌 확률이 아니에요. 봉인을 풀면 표시된 비용을 내고 내용을 읽게 돼요. 투자 결과와 관계없이 정보 비용은 지출로 남아요.",
            "어떤 일이 생기는지, 어떤 업종에 기회나 부담이 될지 생각하며 읽어 보세요. 보유한 회사와 무관한 정보일 수도 있어요. 정보를 사는 행동 자체가 주가를 올리지는 않아요.",
            "자동 갱신은 같은 날짜 안에서 주가를 바꿔요. ‘다음 날’은 날짜와 판매 정보도 바꾸고, 사건이 반영된 새 주가와 전날 결산을 보여줘요. 안내 중 자동 갱신은 잠시 멈춰 있어요.",
            "결산은 지난날의 비용과 자산 변화를, 개장 뉴스는 새 주가가 움직인 원인을 보여줘요. 아직 팔지 않은 주식의 가격 변화는 평가손익이에요. 매도하기 전에는 현금으로 확정된 수익이 아니에요.",
            "매도는 보유 주식을 팔아 현금으로 바꾸는 거예요. 수수료를 뺀 금액을 받고, 판 주식의 매매손익이 확정돼요. 아래 받을 금액과 실제 보유 수량을 확인해 보세요.",
            "현금은 지금 쓸 수 있는 돈이고, 총자산은 현금에 주식의 현재 가치와 예금 원금을 더한 값이에요. 매매손익과 정보 구매비는 별개예요. 내 자산에서 전체 결과와 기록을 살펴볼게요.",
            "예금은 10만원을 맡기고 게임 날짜로 3일 뒤 100,500원을 받는 방식이에요. 그동안 돈을 꺼낼 수는 없지만 원금은 총자산에 포함돼요. 가입하지 않고 안내를 마쳐도 괜찮아요.",
            "여기서 한 거래와 정보 구매는 현재 게임에 그대로 남아요. 이제 기업과 정보를 직접 골라 보세요. 필요하면 도움말에서 안내를 다시 켜고 거래 내역도 확인할 수 있어요."};
        static readonly string[] GuideTasks={
            "‘종목 선택’을 눌러 기업 목록을 열어보세요.","금빛으로 표시한 기업 중 하나를 골라보세요.",
            "수수료를 포함한 금액을 확인하고 ‘매수’를 누르세요.","아래 ‘정보 가판대’를 눌러보세요.",
            "강조된 정보 카드를 눌러 구매 전 설명을 보세요.","비용을 확인하고 ‘봉인 해제’를 누르세요.",
            "본문을 읽은 뒤 아래 ‘거래소’를 눌러보세요.","상단 ‘다음 날’을 눌러 하루를 진행해 보세요.",
            "뉴스와 금액을 살펴본 뒤 ‘결산 확인’을 누르세요.","보유 주식 1주를 ‘매도’해 보세요.",
            "아래 ‘내 자산’에서 전체 결과를 확인해 보세요.","예금에 가입하거나 ‘안내 마치기’를 누르세요.",
            "‘안내 마치기’를 눌러 자유롭게 투자해 보세요."};
        void BuildGuideExplanation(Transform parent)
        {
            guideGameInput=safeArea.GetComponent<CanvasGroup>()??safeArea.gameObject.AddComponent<CanvasGroup>();
            guideSettlementInput=settlementWindow.GetComponent<CanvasGroup>()??settlementWindow.gameObject.AddComponent<CanvasGroup>();
            guideExplanationOverlay=Rect("GuideExplanationOverlay",parent);Stretch(guideExplanationOverlay);
            Surface(guideExplanationOverlay,new Color(0,0,0,.76f),false);
            guideExplanationSafe=Rect("GuideExplanationSafe",guideExplanationOverlay);
            guideExplanationWindow=Column(guideExplanationSafe,24,12);guideExplanationWindow.name="GuideExplanationWindow";
            Surface(guideExplanationWindow,Panel);
            var outline=guideExplanationWindow.gameObject.AddComponent<Outline>();outline.effectColor=Mint;outline.effectDistance=new Vector2(1,-1);
            guideExplanationProgress=Label(guideExplanationWindow,"",21,Mint,34);
            guideExplanationTitle=Label(guideExplanationWindow,"",34,Gold,72);guideExplanationTitle.name="GuideExplanationTitle";
            guideExplanationTitle.fontStyle=FontStyle.Bold;
            guideExplanationBody=Label(guideExplanationWindow,"",25,null,0,TextAnchor.UpperLeft);guideExplanationBody.name="GuideExplanationBody";
            var bodyLayout=Layout(guideExplanationBody.rectTransform);bodyLayout.minHeight=150;bodyLayout.flexibleHeight=1;
            guideExplanationBody.lineSpacing=1.15f;
            var result=Card(guideExplanationWindow,14,4);Height(result,140);result.GetComponent<Image>().color=Background;
            Label(result,"현재 상황과 금액",18,Mint,26);
            guideExplanationResult=Label(result,"",24,null,82);guideExplanationResult.name="GuideExplanationResult";
            guideExplanationAction=Label(guideExplanationWindow,"",22,Gold,68);guideExplanationAction.name="GuideExplanationAction";
            var buttons=Row(guideExplanationWindow,82,12);
            var later=Button(buttons,"나중에",()=>LeaveTutorial(false),Raised,82);later.name="GuideExplainPause";Width((RectTransform)later.transform,120);
            guideExplainContinue=Button(buttons,"직접 해보기",ConfirmGuideExplanation,Hex("266957"),82);guideExplainContinue.name="GuideExplainContinue";
            PositionGuideExplanation();guideExplanationOverlay.gameObject.SetActive(false);
        }
        void PositionGuideExplanation()
        {
            if(guideExplanationSafe==null)return;
            guideExplanationSafe.anchorMin=new Vector2(Screen.safeArea.xMin/Screen.width,Screen.safeArea.yMin/Screen.height);
            guideExplanationSafe.anchorMax=new Vector2(Screen.safeArea.xMax/Screen.width,Screen.safeArea.yMax/Screen.height);
            guideExplanationSafe.offsetMin=guideExplanationSafe.offsetMax=Vector2.zero;
            guideExplanationWindow.anchorMin=guideExplanationWindow.anchorMax=new Vector2(.5f,.5f);
            guideExplanationWindow.pivot=new Vector2(.5f,.5f);guideExplanationWindow.anchoredPosition=Vector2.zero;
            guideExplanationWindow.sizeDelta=new Vector2(Mathf.Min(560,guideExplanationSafe.rect.width-24),Mathf.Min(780,guideExplanationSafe.rect.height-24));
        }
        void ResetGuideExplanation()
        {
            guideExplanationStep=-1;guideExplanationRequested=false;guideExplanationError="";UpdateGuideExplanationVisibility();
        }
        void OpenGuideExplanation()
        {
            if(!TutorialActive||StoryActive)return;
            guideExplanationError="";guideExplanationRequested=true;RenderTab();
        }
        void ConfirmGuideExplanation()
        {
            if(!GuideExplanationOpen)return;
            guideExplanationError="";guideExplanationRequested=false;RenderTab();
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
        }
        void RefreshGuideExplanation()
        {
            if(TutorialActive&&!StoryActive) {
                int step=GuideExplanationStage;
                if(guideExplanationStep!=step){guideExplanationStep=step;guideExplanationRequested=true;guideExplanationError="";}
                guideExplanationProgress.text="투자 도우미  ·  "+(step+1)+" / "+TutorialGuide.Count;
                guideExplanationTitle.text=GuideTitles[step];guideExplanationBody.text=GuideLessons[step];
                guideExplanationResult.text=GuideCurrentResult(step);
                guideExplanationAction.text=guideExplanationError.Length>0?guideExplanationError:"이번에 해볼 일\n"+GuideTasks[step];
            }
            PositionGuideExplanation();UpdateGuideExplanationVisibility();
            if(GuideExplanationOpen){Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(guideExplanationWindow);}
        }
        void ShowGuideExplanationSaveError()
        {
            if(!GuideExplanationOpen)return;
            guideExplanationError="안내 중단을 저장하지 못했어요.\n‘나중에’를 다시 눌러 주세요.";
            guideExplanationAction.text=guideExplanationError;
        }
        void UpdateGuideExplanationVisibility()
        {
            if(guideExplanationOverlay==null)return;
            bool visible=GuideExplanationOpen;
            bool opening=visible&&!guideExplanationOverlay.gameObject.activeSelf;
            guideExplanationOverlay.gameObject.SetActive(visible);
            guideGameInput.interactable=guideGameInput.blocksRaycasts=!visible;
            guideSettlementInput.interactable=guideSettlementInput.blocksRaycasts=!visible;
            if(opening&&EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(guideExplainContinue.gameObject);
        }
        string GuideCurrentResult(int step)
        {
            int company=state.TutorialCompany;var stock=state.Stocks[company];string name=Market.Companies[company].Name;
            switch(step) {
                case 0:return "시작할 수 있는 현금  "+Money(state.Cash)+"\n기업을 고르는 것부터 함께 해볼게요.";
                case 1:return "현재 현금  "+Money(state.Cash)+"\n금빛 기업은 지금 1주를 살 수 있는 종목이에요.";
                case 2:
                    var buyQuote=Market.GetQuote(state,company,1);
                    return name+" 1주  "+Money(stock.Price)+"\n수수료 "+Money(buyQuote.Fee)+" 포함 · 결제액 "+Money(buyQuote.Buy);
                case 3:return name+" 보유  "+stock.Quantity.ToString("N0")+"주\n현재 현금  "+Money(state.Cash);
                case 4:
                    var offer=Intelligence.Offer(state.Day,state.Seed,GuideOfferIndex,eventEdition:state.EventEdition);
                    return offer.Grade+"등급 · 신뢰도 "+offer.Tier.Confidence+"%\n"+(Intelligence.Owned(state,state.Day,offer.Index)==null?"열람 비용  "+Money(offer.Price)+" · 아직 구매하지 않았어요.":"이미 보유한 자료 · 추가 비용 없이 읽을 수 있어요.");
                case 5:
                    var sealedOffer=Intelligence.Offer(state.Day,state.Seed,state.TutorialOffer,eventEdition:state.EventEdition);
                    return "정보 비용  "+Money(sealedOffer.Price)+"\n"+(state.Cash>=sealedOffer.Price?"구매 후 남을 현금  "+Money(state.Cash-sealedOffer.Price):"현금 부족 · ‘나중에’로 닫고 알바할 수 있어요.");
                case 6:
                    var owned=Intelligence.Owned(state,state.TutorialDay,state.TutorialOffer);
                    return owned==null?"구매한 자료는 정보 가판대의 내 보관함에 남아요.":"구매한 정보 비용  "+Money(owned.PaidPrice)+"\n다시 읽을 때는 비용을 내지 않아요.";
                case 7:return "현재 "+state.Day+"일차 · 다음은 "+(state.Day+1)+"일차\n날짜를 넘기면 되돌릴 수는 없어요.";
                case 8:
                    var news=state.LastReport?.News.FirstOrDefault(item=>item.Company==company);
                    return news==null?"현재 총자산  "+Money(Market.Total(state)):name+"  "+Money(news.PreviousPrice)+" → "+Money(news.OpeningPrice)+"\n개장 변동  "+Percent((news.OpeningPrice/(double)news.PreviousPrice-1)*100);
                case 9:
                    var sellQuote=Market.GetQuote(state,company,1);
                    return "보유 "+stock.Quantity.ToString("N0")+"주 중 1주 매도\n수수료 "+Money(sellQuote.Fee)+" 제외 · 받을 금액 "+Money(sellQuote.Sell);
                case 10:
                    var sale=state.Trades.LastOrDefault(item=>!item.Buy&&item.Company==company);
                    return (sale==null?"현재 현금  "+Money(state.Cash):sale.Day+"일차 최근 매도 손익  "+Signed(sale.RealizedProfit))+"\n현재 총자산  "+Money(Market.Total(state));
                case 11:return "예치금 100,000원 · 만기 "+(state.Day+3)+"일차\n"+(state.Cash>=100000?"만기에 100,500원 반환 · 가입은 선택이에요.":"현금 부족 · 가입 없이 안내를 마칠 수 있어요.");
                default:return "현재 현금  "+Money(state.Cash)+"\n"+(state.DepositDue>0?"예금은 "+state.DepositDue+"일차에 자동으로 돌아와요.":"현재 총자산  "+Money(Market.Total(state)));
            }
        }
    }
}
