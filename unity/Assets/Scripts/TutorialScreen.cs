using System;
using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        const string TutorialModeKey="MoneyTown.Tutorial.v2.Mode",TutorialStepKey="MoneyTown.Tutorial.v2.Step";
        TutorialSession tutorial;
        string tutorialHint="";
        int SavedTutorialStep => Mathf.Clamp(GameRuntime.GetInt(TutorialStepKey,0),0,TutorialSession.Count);
        static readonly string[] LessonTitles={"첫 투자를 연습해 볼까요?","30초마다 시세가 움직여요","주식 1주를 사 봐요","정보는 열기 전까지 봉인","단서에서 기업을 찾아요","시장을 하루 진행해요","주식을 팔아 정산해요","예금에 돈을 맡겨요","만기까지 기다려요","연습을 마쳤어요"};
        static readonly string[] LessonActions={"연습 시작","30초 갱신 체험 · 연습용","1주 매수","봉인 해제","기업 고르기","다음 날로 이동","보유 1주 매도","100,000원 예금 가입","3일 지나기 · 연습용","내 게임으로 돌아가기"};
        void RestoreTutorial()
        {
            if(blocked||state.SettlementPending)return;
            int mode=GameRuntime.GetInt(TutorialModeKey,0),step=SavedTutorialStep;
            if(mode==0 || (mode==1&&step<TutorialSession.Count))tutorial=TutorialSession.Restore(mode==0?0:step);
        }
        bool SaveTutorialProgress(int step,int mode)
        {
            int oldStep=GameRuntime.GetInt(TutorialStepKey,0),oldMode=GameRuntime.GetInt(TutorialModeKey,0);
            try {
                GameRuntime.SetInt(TutorialStepKey,step);GameRuntime.SetInt(TutorialModeKey,mode);GameRuntime.SavePreferences();return true;
            } catch(Exception) {
                GameRuntime.SetInt(TutorialStepKey,oldStep);GameRuntime.SetInt(TutorialModeKey,oldMode);
                tutorialHint="연습 진행을 저장하지 못했습니다. 저장 공간을 확인하세요.";return false;
            }
        }
        void StartTutorial(bool restart)
        {
            int step=restart?0:SavedTutorialStep;if(step>=TutorialSession.Count)step=0;
            if(!SaveTutorialProgress(step,1)){Feedback(tutorialHint,true);return;}
            tutorial=TutorialSession.Restore(step);tutorialHint="";resetPending=false;RenderTab();scroll.verticalNormalizedPosition=1;
        }
        void LeaveTutorial(bool help)
        {
            if(tutorial!=null&&!SaveTutorialProgress(tutorial.Step,2)){RenderTab();return;}
            tutorial=null;tutorialHint="";tab=help?3:0;RenderTab();scroll.verticalNormalizedPosition=1;
        }
        void OpenHelp() {if(StoryActive||state.SettlementPending)return;if(tutorial!=null){LeaveTutorial(true);return;}tab=3;resetPending=false;RenderTab();scroll.verticalNormalizedPosition=1;}
        void AdvanceTutorial(int expectedStep,int answer=-1)
        {
            if(tutorial==null||tutorial.Step!=expectedStep)return;
            // Commit progress before replacing the displayed practice state. Never call the real GameStore.
            var changed=TutorialSession.Restore(tutorial.Step);
            try {changed.Advance(expectedStep,answer);}
            catch(InvalidOperationException ex){tutorialHint=ex.Message;RenderTab();return;}
            if(!SaveTutorialProgress(changed.Step,changed.Step==TutorialSession.Count?2:1)){RenderTab();return;}
            tutorialHint="";
            if(changed.Step==TutorialSession.Count){tutorial=null;tab=0;message="연습 완료! 도움말에서 언제든 다시 연습하세요.";}
            else tutorial=changed;
            RenderTab();scroll.verticalNormalizedPosition=1;
        }
        void GuideText(Transform parent,string value,Color? tint=null)
        {var text=Label(parent,value,23,tint??Muted,0,TextAnchor.UpperLeft);text.resizeTextForBestFit=false;}
        void TutorialChart()
        {
            var stock=tutorial.State.Stocks[TutorialSession.Company];
            var card=Card(content,20,8);var head=Row(card,66);Icon(head,1,SectorColors[1],58);
            var info=Column(head,0,2);Flexible(info);Label(info,"픽셀게임즈 · 게임",25,null,36);Label(info,"보유 "+stock.Quantity+"주",19,Muted,26);
            Label(card,Money(stock.Price),36,null,52);
            var rect=Rect("PracticeChart",card);Height(rect,120);var chart=rect.gameObject.AddComponent<PriceChart>();chart.Prices=stock.History;chart.color=Mint;chart.raycastTarget=false;
            var range=Row(card,34);Flexible(Label(range,"최근 "+stock.History.Length+"개 시세",18,Muted,34).rectTransform);
            Flexible(Label(range,Percent(Change(stock)),24,Movement(Change(stock)),34,TextAnchor.MiddleRight).rectTransform);
            if(stock.Quantity>0)Label(card,"평가손익  "+Signed(stock.Price*stock.Quantity-stock.Cost),23,Movement(stock.Price*stock.Quantity-stock.Cost),44);
        }
        void LessonNote(string title,string text,int icon=7)
        {
            var note=Card(content,20,10);var row=Row(note,48);Icon(row,icon,Gold,42);Flexible(Label(row,title,25,Gold,42).rectTransform);GuideText(note,text);
        }
        void DrawTutorial()
        {
            int step=tutorial.Step;var practice=tutorial.State;
            var heading=Row(content,46);Flexible(Label(heading,"투자 연습  "+(step+1)+" / "+TutorialSession.Count,24,Mint,42).rectTransform);
            var skip=Button(heading,step==0?"건너뛰기":"나중에",()=>LeaveTutorial(false),Raised,46);Width((RectTransform)skip.transform,120);
            Bar(content,(step+1)/(float)TutorialSession.Count,Mint,8);
            Label(content,LessonTitles[step],30,null,68);
            switch(step) {
                case 0:
                    var welcome=Card(content,24,14);var icons=Row(welcome,116,20);Icon(icons,9,Mint,92);Icon(icons,7,Gold,92);Icon(icons,8,Blue,92);
                    Label(welcome,"연습 자금 1,000,000원",30,Gold,62);
                    GuideText(welcome,"본격적으로 투자하기 전에 첫 거래를 연습해 봅시다. 주식을 사고, 기사 속 단서를 살펴본 뒤 팔아 보세요. 약 2~3분이면 됩니다.");
                    LessonNote("본게임 자산은 그대로","연습용 시장을 따로 사용합니다. 중간에 나가도 도움말에서 이어할 수 있습니다.",6);break;
                case 1:
                    TutorialChart();LessonNote("현재가와 흐름","본게임 기본 간격은 30초입니다. 상단 ‘간격 설정’에서 1시간까지 바꿀 수 있습니다. 연습에서는 아래 버튼으로 바로 체험합니다. 과거 상승이 미래 상승을 보장하지 않습니다.",9);break;
                case 2:
                    TutorialChart();var buyQuote=Market.GetQuote(practice,1,1);
                    LessonNote("먼저 1주만","1주 가격과 수수료 "+Money(buyQuote.Fee)+"를 합쳐 "+Money(buyQuote.Buy)+"이 듭니다. 현금은 줄고 보유 주식은 늘어납니다.",6);break;
                case 3:
                    var offer=tutorial.Offer;var sealedPaper=Paper(content,offer.Kind,24);
                    Print(sealedPaper,"연습용 봉인 정보",27,48,PrintBlue,TextAnchor.MiddleCenter,true);Rule(sealedPaper,PrintBlue,2);
                    Print(sealedPaper,offer.Tier.Name,28,56,Ink,TextAnchor.MiddleCenter,true);Icon(sealedPaper,7,PrintBlue,88);
                    Print(sealedPaper,"신뢰도 "+offer.Tier.Confidence+"%   ·   "+Money(offer.Price),26,54,PrintBlue,TextAnchor.MiddleCenter,true);
                    Print(sealedPaper,offer.Tier.Detail,23,64,PaperMuted,TextAnchor.MiddleCenter);
                    LessonNote("정보도 비용이에요","기업·내용은 열어야 알 수 있습니다. 신뢰도는 내용이 사실일 확률이며, 돈을 벌 확률이 아닙니다.");break;
                case 4:
                    var clue=tutorial.Clue;var paper=Paper(content,clue.Offer.Kind,24);
                    Print(paper,"연습 취재 자료",24,42,PrintBlue,TextAnchor.MiddleCenter,true);Rule(paper,PrintBlue,2);
                    Print(paper,ClueContent.Headline(clue.Offer),29,92,Ink,TextAnchor.MiddleLeft,true);Article(paper,ClueContent.Body(clue.Offer));
                    Label(content,"어떤 기업과 연결될까요?",24,Gold,46);
                    var choices=Row(content,110,10);
                    foreach(int candidate in new[]{0,1,4}) {
                        int company=candidate;var button=Button(choices,"",()=>AdvanceTutorial(step,company),Raised,110);
                        var body=Column(button.transform,6,4);Stretch(body);Icon(body,company,SectorColors[company],46);
                        Label(body,Market.Companies[company].Name,20,null,28,TextAnchor.MiddleCenter);Label(body,Market.Companies[company].Sector,16,Muted,22,TextAnchor.MiddleCenter);
                    }
                    break;
                case 5:
                    TutorialChart();LessonNote("단서는 다음 날 개장에 반영돼요","게임박람회는 게임 기업과 연결됩니다. ‘다음 날’을 누르면 실제 사건이 개장 시세에 반영됩니다. 장중 자동 갱신만으로는 정보의 사실 여부를 알 수 없습니다.",9);break;
                case 6:
                    TutorialChart();var sellQuote=Market.GetQuote(practice,1,1);
                    LessonNote("손익을 확정해요","기사가 실제 사건과 일치했습니다.\n매도하면 수수료 "+Money(sellQuote.Fee)+"를 빼고 "+Money(sellQuote.Sell)+"을 받습니다.",6);break;
                case 7:
                    var result=Card(content,24,12);Icon(result,6,Gold,88);Label(result,"이번 거래 실현손익",25,null,46);Label(result,Signed(practice.Realized),38,Movement(practice.Realized),62);
                    GuideText(result,"매수·매도 수수료는 반영됐습니다. 정보 구매비 "+Money(tutorial.Offer.Price)+"는 별도 지출입니다.");
                    LessonNote("다른 투자처: 예금","100,000원을 3일 맡기면 100,500원을 받습니다. 만기 전에는 예치금을 꺼낼 수 없습니다.",8);break;
                case 8:
                    var deposit=Card(content,24,14);Icon(deposit,8,Gold,110);Label(deposit,"만기까지 3일",32,Gold,60);Bar(deposit,0,Gold,16);
                    GuideText(deposit,"현금은 줄었지만 예금 원금은 총자산에 포함됩니다. 연습에서는 3일을 한 번에 진행해 만기를 확인합니다.");
                    Label(deposit,"반환 예정 100,500원",27,null,54);break;
                case 9:
                    var done=Card(content,24,12);Icon(done,9,Mint,100);Label(done,"예금 만기 +500원",30,Mint,58);
                    var totals=Row(done,88);var left=Column(totals);Flexible(left);Label(left,"시작 연습 자산",18,Muted,30);Label(left,"1,000,000원",25,null,42);
                    var right=Column(totals);Flexible(right);Label(right,"마지막 연습 자산",18,Muted,30);Label(right,Money(Market.Total(practice)),25,Gold,42);
                    GuideText(done,"최종 결과에는 거래 손익, 정보 비용, 예금 이자가 모두 포함됩니다.");
                    LessonNote("이제 직접 판단해 보세요","정보는 10등급이 최고입니다. 특정 기업의 구체적인 분석은 기업 분석 탭에서 더 비싸게 구매합니다. 막히면 상단 도움말을 열어보세요.");break;
            }
            if(tutorialHint!="")Label(content,tutorialHint,22,Gold,76);
            // Keep the current action visible even on short mobile screens.
            Height(tradeDock,108);tradeDock.gameObject.SetActive(true);
            if(step==4)Label(tradeDock,"위에서 기업을 선택하면 이어집니다.",22,Gold,68,TextAnchor.MiddleCenter);
            else Button(tradeDock,LessonActions[step],()=>AdvanceTutorial(step),step==2?Hex("BD4F61"):step==6?Hex("2861A2"):Hex("266957"),76);
        }
        void DrawHelp()
        {
            Label(content,"플레이 도움말",30,null,52);
            var practice=Card(content,22,10);Label(practice,"직접 해보는 투자 연습",27,Mint,44);GuideText(practice,"별도 연습 자금으로 거래·정보·예금을 체험합니다. 본게임 자산은 바뀌지 않습니다.");
            int step=SavedTutorialStep;
            if(step>0&&step<TutorialSession.Count)Button(practice,"이어서 연습 · "+(step+1)+" / "+TutorialSession.Count,()=>StartTutorial(false),Hex("266957"));
            Button(practice,step==0?"튜토리얼 시작":"처음부터 연습",()=>StartTutorial(true),Raised);
            DrawStoryLibrary(content);
            LessonNote("현금과 총자산","현금은 지금 쓸 수 있는 돈입니다. 총자산은 현금에 보유 주식의 현재 가치와 예금 원금을 더한 금액입니다.",6);
            LessonNote("주식을 사고팔기","거래소의 ‘종목 선택’을 눌러 "+Market.Companies.Length+"개 기업을 둘러보고 종목과 수량을 고르세요. 버튼 금액에는 0.1% 수수료가 포함되며 1원 미만은 올림합니다.",9);
            LessonNote("봉인 정보와 기업 분석","봉인 정보는 대상을 숨기고 판매합니다. 1~10등급 중 10등급이 가장 구체적이고 비쌉니다. 기업 분석은 원하는 기업을 지정하는 고가 정보입니다.");
            LessonNote("정보와 개장 시세","정보가 다룬 사건은 다음 날 개장 시세에 반영됩니다. 장중 자동 갱신은 사건 확인 전 움직임입니다. 다음 날 보관함의 ‘정보와 주가 대조’에서 실제 사건과 종가·시가를 함께 확인하세요.");
            LessonNote("신뢰도와 후속 보도","신뢰도는 내용이 사실일 확률입니다. 정보가 틀리면 실제 사건은 반대일 수 있습니다. 맞는 정보도 수급·장중 변동·수수료·정보 비용에 따라 손실로 이어질 수 있습니다.");
            LessonNote("시세 간격과 결산","상단 ‘간격 설정’에서 30초·1분·3분·5분·15분·30분·1시간을 선택하세요. 남은 시간은 거래소 차트 위에 표시합니다. 장중 1회에 약 0.1~0.6% 움직입니다. 다음 날은 전날 결산 뒤 최소 1.5%의 큰 개장 변동을 적용합니다. 최저가와 가격 반올림에서는 예외가 있습니다. 결산 화면에는 두 결과를 따로 보여줍니다. 도움말·연습·결산·백그라운드에서는 시세가 멈춥니다.",9);
            LessonNote("다음 날과 예금","날짜는 상단 ‘다음 날’ 버튼으로 진행합니다. 10만원 예금은 3일 뒤 10만 500원을 돌려주며 만기 전 해지는 불가합니다.",8);
            LessonNote("물류센터 알바","내 자산에서 알바할 수 있습니다. 보유 금액 제한 없이 하루를 보내고 일당 "+Money(Market.WarehouseWage)+"을 받습니다. 상자를 옮기는 작업 장면 뒤 다음 날 결산이 열리며, 주가와 정보·예금 만기도 함께 진행됩니다.",4);
            Button(content,"거래소로 돌아가기",()=>SwitchTab(0),Raised);
        }
    }
}
