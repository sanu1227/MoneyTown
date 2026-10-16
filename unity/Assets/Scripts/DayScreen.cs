using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        readonly MarketClock marketClock=new MarketClock();
        readonly List<Action> liveQuotes=new List<Action>();
        const string MarketIntervalKey="MoneyTown.Market.IntervalSeconds";
        Text tradeClock;
        Button clockSettings;
        int clockReturnTab;
        bool appPaused,appFocused,marketWasRunning;
        bool showAllSettlementNews,showSettlementDetails;
        long settlementNewsDay;
        RectTransform settlementOverlay,settlementWindow;
        Text settlementTitle,settlementNotice;
        double lastMarketTime;
        bool MarketRunning => state!=null&&!blocked&&!resetPending&&tutorial==null&&tab<3&&!state.SettlementPending&&appFocused&&!appPaused;
        void OnApplicationFocus(bool focused){appFocused=focused;marketWasRunning=false;lastMarketTime=Time.realtimeSinceStartupAsDouble;UpdateFeedbackActivity();}
        void OnApplicationPause(bool paused){appPaused=paused;marketWasRunning=false;lastMarketTime=Time.realtimeSinceStartupAsDouble;UpdateFeedbackActivity();}
        void Watch(Action refresh){liveQuotes.Add(refresh);refresh();}
        void RestoreMarketInterval()
        {
            int saved=GameRuntime.GetInt(MarketIntervalKey,MarketClock.DefaultInterval);
            marketClock.SetInterval(MarketClock.Supports(saved)?saved:MarketClock.DefaultInterval);
        }
        void OpenClockSettings()
        {
            if(blocked||tutorial!=null||state.SettlementPending||tab==5)return;
            clockReturnTab=tab;SwitchTab(5);
        }
        void SelectMarketInterval(int seconds)
        {
            if(!MarketClock.Supports(seconds)||seconds==marketClock.Interval)return;
            int previous=GameRuntime.GetInt(MarketIntervalKey,MarketClock.DefaultInterval);
            bool existed=GameRuntime.HasKey(MarketIntervalKey);
            try {GameRuntime.SetInt(MarketIntervalKey,seconds);GameRuntime.SavePreferences();}
            catch(Exception) {
                if(existed)GameRuntime.SetInt(MarketIntervalKey,previous);else GameRuntime.DeleteKey(MarketIntervalKey);
                Feedback("간격 설정을 저장하지 못했습니다. 다시 시도하세요.",true);return;
            }
            marketClock.SetInterval(seconds);marketWasRunning=false;
            Feedback(MarketClock.IntervalText(seconds)+" 간격으로 저장했습니다.");RenderTab();
        }
        void DrawClockSettings()
        {
            Label(content,"게임 설정",32,null,58);
            var settings=Card(content,22,14);
            Label(settings,"현재 설정 · "+MarketClock.IntervalText(marketClock.Interval),25,Mint,44);
            Label(settings,"다음 갱신까지 "+marketClock.RemainingText,30,Gold,56);
            RectTransform row=null;
            for(int i=0;i<MarketClock.Intervals.Count;i++) {
                if(i==0||i==4)row=Row(settings,84,8);
                int seconds=MarketClock.Intervals[i];bool chosen=seconds==marketClock.Interval;
                Button(row,MarketClock.IntervalText(seconds)+(chosen?"\n선택됨":""),()=>SelectMarketInterval(seconds),chosen?Hex("266957"):Raised,84);
            }
            Label(settings,"간격을 바꾸면 새 카운트다운을 시작합니다.\n같은 간격을 다시 누르면 남은 시간을 유지합니다.",20,Muted,80);
            Label(settings,"설정은 자동 저장됩니다. 앱을 다시 열면 선택한 간격부터 시작합니다.",20,Muted,72);
            Label(settings,"설정·도움말·결산·연습·백그라운드에서는 시간이 멈춥니다. ‘다음 날’도 선택한 간격을 유지합니다.",20,Muted,100);
            DrawFeedbackSettings(content);
            Height(tradeDock,108);tradeDock.gameObject.SetActive(true);
            Button(tradeDock,"설정 완료 · 게임으로 돌아가기",()=>{tab=clockReturnTab;RenderTab();scroll.verticalNormalizedPosition=1;},Hex("266957"),76);
        }
        void UpdateMarket()
        {
            bool running=MarketRunning;
            // Unity's first resumed delta can include the entire inactive interval.
            // Measure our own active frame interval and reset it on focus/pause callbacks.
            double now=Time.realtimeSinceStartupAsDouble;
            double seconds=running&&marketWasRunning?Math.Max(0,now-lastMarketTime):0;
            lastMarketTime=now;marketWasRunning=running;
            if(marketClock.Advance(seconds,running)) {
                try {
                    var changed=Market.Intraday(state);store.Save(changed);state=changed;
                    UpdateSummary();foreach(var refresh in liveQuotes)refresh();Estimate();
                    AnimateQuote();
                } catch(Exception) {Feedback("시세 갱신을 취소했습니다. 저장 공간과 자산 한도를 확인하세요.",true);}
            }
            if(tradeClock!=null) {
                tradeClock.text=(running?"다음 시세 갱신까지  ":"시세 갱신 일시정지  ")+marketClock.RemainingText;
                tradeClock.color=running?Mint:Muted;
            }
        }
        void CloseSettlement()
        {
            if(warehouseWorking)return;
            if(state.SettlementPending) {
                try {var changed=Market.AcknowledgeSettlement(state);store.Save(changed);state=changed;}
                catch(Exception){
                    const string error="확인 내용을 저장하지 못했습니다. 다시 눌러 주세요.";
                    if(settlementNotice!=null){settlementNotice.text=error;Height(settlementNotice.rectTransform,32);Height(tradeDock,138);}
                    Feedback(error,true);return;
                }
                marketClock.Reset();
            }
            bool historical=selectedReportDay.HasValue;selectedReportDay=null;
            tab=historical?6:0;RenderTab();scroll.verticalNormalizedPosition=1;
        }
        void BuildSettlementWindow(Transform parent)
        {
            settlementOverlay=Rect("SettlementOverlay",parent);Stretch(settlementOverlay);
            Surface(settlementOverlay,new Color(0,0,0,.78f),false);
            settlementWindow=Column(settlementOverlay,18,12);settlementWindow.name="SettlementWindow";
            Surface(settlementWindow,Hex("10283B"));
            var border=settlementWindow.gameObject.AddComponent<Outline>();border.effectColor=Hex("397E7A");border.effectDistance=new Vector2(1,-1);
            var title=Row(settlementWindow,82,12);Icon(title,9,Mint,42);
            var heading=Column(title,0,2);Flexible(heading);
            Label(heading,"장 마감 · 개장 브리핑",17,Muted,24);
            settlementTitle=Label(heading,"",34,Mint,48);settlementTitle.fontStyle=FontStyle.Bold;
            var close=Button(title,"닫기",CloseSettlement,Raised,54);Width((RectTransform)close.transform,74);
            PositionSettlementWindow();settlementOverlay.gameObject.SetActive(false);
        }
        void PositionSettlementWindow()
        {
            if(settlementWindow==null)return;
            settlementWindow.anchorMin=new Vector2(Screen.safeArea.xMin/Screen.width,Screen.safeArea.yMin/Screen.height);
            settlementWindow.anchorMax=new Vector2(Screen.safeArea.xMax/Screen.width,Screen.safeArea.yMax/Screen.height);
            settlementWindow.offsetMin=new Vector2(14,14);settlementWindow.offsetMax=new Vector2(-14,-14);
        }
        void SetSettlementWindow(bool visible)
        {
            if(settlementOverlay.gameObject.activeSelf==visible)return;
            // Reuse the same scroll view and fixed action dock; the rest of the game stays behind the modal.
            scroll.transform.SetParent(visible?settlementWindow:safeArea,false);
            tradeDock.SetParent(visible?settlementWindow:safeArea,false);
            if(!visible){scroll.transform.SetSiblingIndex(3);tradeDock.SetSiblingIndex(4);}
            scroll.StopMovement();scroll.verticalNormalizedPosition=1;
            safeArea.gameObject.SetActive(!visible);settlementOverlay.gameObject.SetActive(visible);
            if(visible){showAllSettlementNews=false;showSettlementDetails=false;}
            if(visible)effects?.Animate(settlementWindow,FeedbackMotion.Reveal);
        }
        void ReportLine(Transform parent,string title,string amount,Color? tint=null)
        {
            var row=Row(parent,36);Flexible(Label(row,title,20,Muted,36).rectTransform);
            var value=Label(row,amount,22,tint??Color.white,36,TextAnchor.MiddleRight);Width(value.rectTransform,220);
        }
        static string OpeningNewsSummary(MarketNews news)
        {
            int divider=news.Headline.IndexOf('…');
            string fact=(divider>=0?news.Headline.Substring(divider+1):news.Headline).Trim().TrimEnd('.');
            string cause=fact+(fact.EndsWith("다",StringComparison.Ordinal)?"는 소식":" 소식");
            if(news.OpeningPrice==news.PreviousPrice)
                return cause+(news.OpeningPrice==100?"에도 최저가 제한으로 보합(0.00%)을 유지했습니다.":"에도 주가는 보합(0.00%)을 유지했습니다.");
            bool up=news.OpeningPrice>news.PreviousPrice;
            double change=Math.Abs((news.OpeningPrice/(double)news.PreviousPrice-1)*100);
            string result=change.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"% "+(up?"상승":"하락")+"했습니다.";
            if(news.EventImpact!=0&&up!=(news.EventImpact>0))
                return cause+"에도 "+(up?"매수세":"매도세")+"가 우세해 "+result;
            return cause+"에 "+result;
        }
        void DrawOpeningNews(DayReport report)
        {
            var section=Row(content,38);
            Flexible(Label(section,"시장 핵심 뉴스",25,Mint,38).rectTransform);
            var badge=Label(section,showAllSettlementNews?"전체 기업":"변동폭 상위 3곳",17,Muted,38,TextAnchor.MiddleRight);Width(badge.rectTransform,170);
            if(report.News.Length==0) {
                Label(content,"이전 결산에는 주가 변동 원인 뉴스가 보관되어 있지 않습니다.",20,Muted,64);
                return;
            }
            int count=showAllSettlementNews?report.News.Length:Math.Min(3,report.News.Length);
            for(int i=0;i<count;i++) {
                var news=report.News[i];
                double change=(news.OpeningPrice/(double)news.PreviousPrice-1)*100;
                var card=Card(content,14,4);card.name="SettlementNews-"+news.Company;
                var heading=Row(card,40);CompanyIcon(heading,news.Company,SectorColors[news.Company],34);
                Flexible(Label(heading,Market.Companies[news.Company].Name,23,null,40).rectTransform);
                var movement=Label(heading,Percent(change),26,Movement(change),40,TextAnchor.MiddleRight);
                Width(movement.rectTransform,132);movement.fontStyle=FontStyle.Bold;
                var headline=Label(card,OpeningNewsSummary(news),21,null,76);headline.name="SettlementNewsSummary";
            }
            if(report.News.Length>3)Button(content,showAllSettlementNews?"주요 뉴스만 보기":"전체 뉴스 "+report.News.Length+"건 보기",()=>{
                showAllSettlementNews=!showAllSettlementNews;RenderTab();
                if(!showAllSettlementNews)scroll.verticalNormalizedPosition=1;
            },Raised,52);
        }
        void DrawSettlement()
        {
            var report=selectedReportDay.HasValue?System.Linq.Enumerable.First(state.DayReports,item=>item.Day==selectedReportDay.Value):state.LastReport;
            if(settlementNewsDay!=report.Day){settlementNewsDay=report.Day;showAllSettlementNews=false;showSettlementDetails=false;}
            settlementTitle.text=(report.Day+1)+"일차 개장";
            var summary=Card(content,18,6);summary.name="SettlementSummary";
            Label(summary,"개장 총자산",19,Muted,28);
            var assets=Label(summary,Money(report.OpeningAssets),40,Gold,56);assets.fontStyle=FontStyle.Bold;
            var figures=Row(summary,74,16);
            var previous=Column(figures,0,4);Flexible(previous);
            Label(previous,report.Day+"일차 결산 손익",18,Muted,26);Label(previous,Signed(report.Profit),27,Movement(report.Profit),40);
            var opening=Column(figures,0,4);Flexible(opening);
            Label(opening,"개장 보유주식 손익",18,Muted,26);Label(opening,Signed(report.OpeningProfit),27,Movement(report.OpeningProfit),40);
            if(report.OpeningInterest>0)Label(summary,"오늘 만기 이자 "+Signed(report.OpeningInterest),18,Mint,28);
            if(report.WorkIncome>0){var wage=Label(summary,"물류센터 알바 수입 "+Signed(report.WorkIncome),22,Gold,36);wage.name="SettlementWorkIncome";}
            int up=0,down=0;
            for(int i=0;i<report.ClosePrices.Length;i++){if(report.OpeningPrices[i]>report.ClosePrices[i])up++;else if(report.OpeningPrices[i]<report.ClosePrices[i])down++;}
            Label(content,$"상승 {up}  ·  하락 {down}  ·  보합 {report.ClosePrices.Length-up-down}    |    전일 종가 대비",18,Muted,30);
            DrawOpeningNews(report);
            Button(content,showSettlementDetails?"상세 결산 접기":"상세 결산 보기",()=>{
                showSettlementDetails=!showSettlementDetails;RenderTab();
                if(!showSettlementDetails)scroll.verticalNormalizedPosition=1;
            },Raised,52);
            if(showSettlementDetails)DrawSettlementDetails(report);
            Height(tradeDock,100);tradeDock.gameObject.SetActive(true);
            settlementNotice=Label(tradeDock,"",18,Red,0);
            Button(tradeDock,state.SettlementPending?"결산 확인 · "+state.Day+"일차 시작":selectedReportDay.HasValue?"기록 목록으로 돌아가기":"거래소로 돌아가기",CloseSettlement,Hex("266957"),72);
        }
        void DrawSettlementDetails(DayReport report)
        {
            var result=Card(content,20,8);
            Label(result,"하루 자산 변화",21,Muted,30);
            Label(result,Signed(report.Profit),40,Movement(report.Profit),62);
            ReportLine(result,"시작 기준 자산",Money(report.StartAssets));
            ReportLine(result,"마감 자산",Money(report.CloseAssets));
            Label(result,"주식 평가 변동·비용·이자·알바 수입을 포함합니다.",17,Muted,30);
            ReportLine(result,"거래 실현손익",Signed(report.RealizedProfit),Movement(report.RealizedProfit));
            ReportLine(result,"정보 구매비",Money(report.InformationCost));
            ReportLine(result,"거래 수수료",Money(report.Fees));
            ReportLine(result,"예금 이자",Signed(report.Interest),Gold);
            if(report.WorkIncome>0)ReportLine(result,"물류센터 알바 수입",Signed(report.WorkIncome),Gold);
            Label(result,"실현손익에 수수료 반영 · 위 금액을 다시 더하거나 빼지 마세요.",16,Muted,42);
            Label(result,"장중 자동 갱신 "+report.Ticks+"회",18,Muted,30);

            Label(content,(report.Day+1)+"일차 개장",30,Mint,54);
            var opening=Card(content,20,8);
            ReportLine(opening,"개장 총자산",Money(report.OpeningAssets));
            ReportLine(opening,"보유 주식 개장 손익",Signed(report.OpeningProfit),Movement(report.OpeningProfit));
            if(report.OpeningInterest>0)ReportLine(opening,"오늘 만기 이자",Signed(report.OpeningInterest),Gold);
            Label(opening,"아래 변동은 새 하루의 성과에 포함됩니다.",18,Mint,34);
            Label(opening,"전일 종가 → 오늘 시가 · 장중보다 큰 변동",18,Muted,32);
            for(int i=0;i<report.ClosePrices.Length;i++) {
                var row=Row(opening,76);CompanyIcon(row,i,SectorColors[i],42);
                var company=Column(row,0,0);Flexible(company);
                Label(company,Market.Companies[i].Name,20,null,30);
                Label(company,Money(report.ClosePrices[i])+" → "+Money(report.OpeningPrices[i]),18,Muted,34);
                double change=(report.OpeningPrices[i]/(double)report.ClosePrices[i]-1)*100;
                var percent=Label(row,Percent(change),24,Movement(change),50,TextAnchor.MiddleRight);Width(percent.rectTransform,112);
            }
            Label(opening,"장중 1회 약 ±0.1~0.6% · 개장 최소 1.5%\n정수 반올림과 최저가 100원에서는 예외입니다.",17,Muted,56);
        }
    }
}
