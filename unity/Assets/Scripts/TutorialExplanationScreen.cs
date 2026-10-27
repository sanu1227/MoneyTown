using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        Text guideActionText;
        int guideExplanationStep=-1;
        bool guideExplanationExpanded=true;
        string guideExplanationError="";
        int GuideExplanationStage => state.SettlementPending?8:state.TutorialStep;
        static readonly string[] GuideLessons={
            "거래소는 기업의 주식을 사고파는 곳이에요. 먼저 회사를 살펴볼게요. 안내 중 거래한 돈과 주식도 실제 게임에 남아요.",
            "종목은 거래할 회사의 주식을 뜻해요. 선택만으로 돈이 나가지는 않아요. 살 수 있는 기업을 골라 1주부터 시작해 볼게요.",
            "매수는 현금을 내고 주식을 사는 거예요. 결제액에는 0.1% 수수료가 포함돼요. 현금이 줄고 보유 주식이 늘어요.",
            "주가가 오르면 보유 주식의 가치가 늘고, 내리면 줄어요. 기업에 영향을 줄 사건을 찾아볼게요. 정보를 살 때도 비용이 들어요.",
            "봉인 정보는 기업과 내용이 숨겨져 있어요. 등급은 정보의 깊이, 신뢰도는 내용이 맞을 가능성이에요. 가장 싼 자료부터 살펴볼게요.",
            "신뢰도는 정보가 맞을 가능성이지, 수익 확률은 아니에요. 봉인을 풀면 비용을 내고 내용을 읽어요. 정보비는 지출로 남아요.",
            "이 사건이 어느 업종에 기회나 부담이 될지 생각해 보세요. 보유 회사와 무관할 수도 있어요. 정보를 산다고 주가가 오르지는 않아요.",
            "자동 갱신은 같은 날의 시세 변화예요. ‘다음 날’은 날짜·정보를 바꾸고 새 주가와 결산을 보여줘요. 안내 중 자동 갱신은 멈춰요.",
            "결산은 비용과 자산 변화를, 개장 뉴스는 주가 변동 원인을 보여줘요. 아직 팔지 않은 주식의 평가손익은 현금으로 확정된 수익이 아니에요.",
            "매도는 주식을 팔아 현금으로 바꾸는 거예요. 수수료를 뺀 금액을 받고 매매손익이 확정돼요. 보유 수량과 받을 금액을 확인하세요.",
            "현금은 지금 쓸 돈이고, 총자산은 현금·주식 가치·예금 원금의 합이에요. 매매손익과 정보비는 별개예요. 전체 결과를 살펴볼게요.",
            "예금은 10만원을 맡겨 3일 뒤 100,500원을 받아요. 만기 전에는 꺼낼 수 없지만 총자산에 포함돼요. 가입 없이 마쳐도 괜찮아요.",
            "안내에서 한 거래와 정보 구매는 그대로 남아요. 이제 직접 기업과 정보를 골라보세요. 필요하면 도움말에서 안내를 다시 켤 수 있어요."};
        static readonly string[] GuideTasks={
            "‘종목 선택’을 눌러 기업 목록을 열어보세요.","금빛으로 표시한 기업 중 하나를 골라보세요.",
            "수수료를 포함한 금액을 확인하고 ‘매수’를 누르세요.","아래 ‘정보 가판대’를 눌러보세요.",
            "강조된 정보 카드를 눌러 구매 전 설명을 보세요.","비용을 확인하고 ‘봉인 해제’를 누르세요.",
            "본문을 읽은 뒤 아래 ‘거래소’를 눌러보세요.","상단 ‘다음 날’을 눌러 하루를 진행해 보세요.",
            "뉴스와 금액을 살펴본 뒤 ‘결산 확인’을 누르세요.","보유 주식 1주를 ‘매도’해 보세요.",
            "아래 ‘내 자산’에서 전체 결과를 확인해 보세요.","예금에 가입하거나 ‘안내 마치기’를 누르세요.",
            "‘안내 마치기’를 눌러 자유롭게 투자해 보세요."};
        void ResetGuideExplanation()
        {
            guideExplanationStep=-1;guideExplanationExpanded=true;guideExplanationError="";
        }
        void ToggleGuideExplanation()
        {
            if(!TutorialActive||StoryActive)return;
            float readingOffset=content.anchoredPosition.y;
            guideExplanationExpanded=!guideExplanationExpanded;RenderTab();
            if(GuideExplanationStage==6) {
                float distance=content.rect.height-scroll.viewport.rect.height;
                scroll.verticalNormalizedPosition=distance<=0?1:1-Mathf.Clamp01(readingOffset/distance);
            }
        }
        void PrepareGuideExplanation()
        {
            int step=GuideExplanationStage;
            if(guideExplanationStep==step)return;
            guideExplanationStep=step;guideExplanationExpanded=true;guideExplanationError="";
        }
        void DrawGuideExplanation(int step)
        {
            if(guideExplanationExpanded) {
                Label(guideBanner,GuideLessons[step],22,null,92).name="GuideExplanationBody";
                Label(guideBanner,GuideCurrentResult(step),22,Mint,66).name="GuideExplanationResult";
            }
            string task=GuideTasks[step];
            if(step==2&&OrderGuidance.MaximumBuy(state,selected)==0)task="현금이 부족해요. ‘나중에’로 닫고 내 자산에서 알바한 뒤 이어 하세요.";
            if(step==5&&state.Cash<Intelligence.Offer(state.Day,state.Seed,state.TutorialOffer,eventEdition:state.EventEdition).Price)
                task="정보 비용이 부족해요. ‘나중에’로 닫고 알바한 뒤 이어 하세요. 아직 구매하지 않았어요.";
            guideActionText=Label(guideBanner,guideExplanationError.Length>0?guideExplanationError:task,21,Gold,54);guideActionText.name="GuideBody";
        }
        void ShowGuideExplanationSaveError()
        {
            if(!TutorialActive||guideActionText==null)return;
            guideExplanationError="안내 진행을 저장하지 못했어요.\n같은 버튼을 다시 눌러 주세요.";
            guideActionText.text=guideExplanationError;
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
