using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        static readonly Color Ink=Hex("243343"),PaperMuted=Hex("536171"),StampRed=Hex("A9413E"),PrintBlue=Hex("29577C");
        InfoKind? openedInfo;
        int infoShelf,readerCompany,informationBand;
        int? openedClue;
        int openedClueVersion=Intelligence.CurrentVersion;
        long openedClueDay;

        Text Print(Transform parent,string text,int size=22,float height=36,Color? tint=null,TextAnchor align=TextAnchor.MiddleLeft,bool bold=false)
        {
            var label=Label(parent,text,size,tint??Ink,height,align);
            if(bold)label.fontStyle=FontStyle.Bold;return label;
        }
        void Article(Transform parent,string text,Color? tint=null)
        {
            foreach(string paragraph in text.Split(new[]{"\n\n"},StringSplitOptions.RemoveEmptyEntries)) {
                int divider=paragraph.IndexOf(" | ",StringComparison.Ordinal);
                string body=paragraph;
                if(divider>0&&divider<36&&!paragraph.Substring(0,divider).Contains("\n")) {
                    var heading=Print(parent,paragraph.Substring(0,divider),22,0,PrintBlue,TextAnchor.UpperLeft,true);
                    heading.resizeTextForBestFit=false;
                    body=paragraph.Substring(divider+3);
                }
                var label=Print(parent,body,22,0,tint,TextAnchor.UpperLeft);
                label.resizeTextForBestFit=false;label.lineSpacing=1.2f;
            }
        }
        RectTransform Paper(Transform parent,InfoKind kind,int padding=22,bool mini=false)
        {
            var rect=Column(parent,padding,8);rect.name=kind+"Paper";
            var sheet=rect.gameObject.AddComponent<PaperSheet>();sheet.Kind=kind;sheet.Mini=mini;sheet.raycastTarget=false;
            return rect;
        }
        void Rule(Transform parent,Color color,float height=2)
        {var rect=Rect("PrintedRule",parent);Height(rect,height);Surface(rect,color,false).raycastTarget=false;}
        void OpenInfo(InfoKind kind,int company)
        {openedClue=null;openedInfo=kind;readerCompany=company;RenderTab();scroll.verticalNormalizedPosition=1;}
        void InfoHome(int shelf)
        {infoShelf=shelf;openedInfo=null;openedClue=null;choosingCompany=false;RenderTab();scroll.verticalNormalizedPosition=1;}
        void OpenClue(long day,int index,int version=Intelligence.CurrentVersion)
        {openedInfo=null;openedClue=index;openedClueDay=day;openedClueVersion=version;RenderTab();scroll.verticalNormalizedPosition=1;}
        void DrawInformation()
        {
            if(openedClue.HasValue){DrawClueReader();return;}
            if(openedInfo.HasValue){DrawLegacyReader(openedInfo.Value);return;}
            var tabs=Row(content,62);
            Button(tabs,"봉인 정보",()=>InfoHome(0),infoShelf==0?Hex("266957"):Raised,62);
            Button(tabs,"기업 분석",()=>InfoHome(2),infoShelf==2?Hex("266957"):Raised,62);
            Button(tabs,"내 보관함",()=>InfoHome(1),infoShelf==1?Hex("266957"):Raised,62);
            if(infoShelf==1){DrawCollection();return;}
            if(infoShelf==2) {
                var companyRow=Row(content,58);
                Flexible(Label(companyRow,"기업 지정 분석",26,null,44).rectTransform);
                var choose=Button(companyRow,choosingCompany?"목록 접기":"기업 선택",()=>{choosingCompany=!choosingCompany;RenderTab();},Raised,58);choose.name="CompanyPicker";Width((RectTransform)choose.transform,140);
                if(choosingCompany)CompanyStrip();
                Label(content,Market.Companies[selected].Name+" · 오늘의 지정 분석",24,SectorColors[selected],44);
                Label(content,"기업 지정료 포함 · 같은 날 자료는 같은 사건",17,Muted,32);
                foreach(var offer in Intelligence.CompanyOffers(state,selected))DrawOfferCard(offer);
            } else {
                Label(content,"오늘의 봉인 정보 · "+state.Day+"일차",26,null,44);
                Label(content,InformationCatalog.All.Count.ToString("N0")+"개 정보 · 10등급이 최고 · 매일 "+Intelligence.DailyCount+"건 입고",18,Muted,32);
                var bands=Row(content,46);
                string[] names={"전체","1–3등급","4–6등급","7–10등급"};
                for(int i=0;i<4;i++){int band=i;Button(bands,names[i],()=>{informationBand=band;RenderTab();},informationBand==i?Hex("476348"):Raised,46);}
                foreach(var offer in Intelligence.Offers(state))
                    if(informationBand==0 || informationBand==(offer.Grade<=3?1:offer.Grade<=6?2:3))DrawOfferCard(offer);
            }
            Label(content,"신뢰도는 사건 내용이 사실일 확률입니다. 등급은 정보의 깊이이며 수익 확률이 아닙니다.",18,Muted,68);
        }
        void DrawOfferCard(IntelOffer offer)
        {
            var tier=offer.Tier;var owned=Intelligence.Owned(state,state.Day,offer.Index);
            var tile=Paper(content,offer.Kind,14,true);Height(tile,210);tile.GetComponent<VerticalLayoutGroup>().spacing=5;
            var button=tile.gameObject.AddComponent<Button>();var graphic=tile.GetComponent<PaperSheet>();graphic.raycastTarget=true;button.targetGraphic=graphic;
            long day=state.Day;button.onClick.AddListener(()=>OpenClue(day,offer.Index));
            var heading=Row(tile,38);var tint=offer.Grade>=8?Hex("9C6B1F"):PrintBlue;
            if(offer.Targeted)CompanyIcon(heading,offer.Company,tint,32);else Icon(heading,7,tint,32);
            Flexible(Print(heading,tier.Name,24,38,null,TextAnchor.MiddleLeft,true).rectTransform);
            Print(tile,offer.Source+" / "+(owned==null?"내용 비공개":"구매 완료"),17,26,PaperMuted);
            Print(tile,"신뢰도 "+tier.Confidence+"%  ·  "+Money(offer.Price),24,40,PrintBlue,TextAnchor.MiddleLeft,true);
            Print(tile,tier.Detail,18,30,PaperMuted);
            Print(tile,owned!=null?"구매한 자료 읽기  ›":offer.Targeted?"분석 범위 보기  ›":"대상·사건 비공개 · 자세히  ›",18,28,null,TextAnchor.MiddleRight,true);
        }
        void DrawCollection()
        {
            Label(content,"내가 모은 단서",28,null,44);
            if(state.Clues.Length==0 && state.Information.All(r=>r==null)) {
                var empty=Card(content,24);Icon(empty,7,Gold,90);Label(empty,"아직 구매한 자료가 없습니다.",24,null,48);
                Label(empty,"가격과 신뢰도를 비교해 첫 단서를 골라보세요.",20,Muted,64);Button(empty,"정보 고르기",()=>InfoHome(0));return;
            }
            foreach(var item in state.Clues.OrderByDescending(r=>r.IssuedDay).ThenByDescending(r=>r.Version).ThenBy(r=>r.Index)) {
                var clue=item;var offer=clue.Offer;
                var card=Paper(content,offer.Kind,18,true);Height(card,178);
                var button=card.gameObject.AddComponent<Button>();var graphic=card.GetComponent<PaperSheet>();graphic.raycastTarget=true;button.targetGraphic=graphic;
                button.onClick.AddListener(()=>OpenClue(clue.IssuedDay,clue.Index,clue.Version));
                Print(card,ClueContent.Headline(offer),23,68,null,TextAnchor.MiddleLeft,true);
                Print(card,clue.IssuedDay+"일차 · "+offer.Tier.Name+(offer.Targeted?" · 지정":""),18,28,PaperMuted);
                Print(card,state.Day>clue.IssuedDay?"후속 보도 도착 · 대조하기  ›":"단서 다시 읽기  ›",19,30,PrintBlue,TextAnchor.MiddleRight,true);
            }
            if(state.Information.Any(r=>r!=null))Label(content,"이전 버전에서 구매한 자료",22,Muted,44);
            foreach(var item in state.Information.Where(r=>r!=null).OrderByDescending(r=>r.IssuedDay).ThenBy(r=>r.Kind)) {
                var record=item;var product=InfoMarket.Products[(int)record.Kind];
                var card=Paper(content,record.Kind,18,true);Height(card,158);
                var button=card.gameObject.AddComponent<Button>();var graphic=card.GetComponent<PaperSheet>();graphic.raycastTarget=true;button.targetGraphic=graphic;
                button.onClick.AddListener(()=>OpenInfo(record.Kind,record.Company));
                Print(card,product.Name+" · "+Market.Companies[record.Company].Name,25,40,null,TextAnchor.MiddleLeft,true);
                Print(card,"발행 "+record.IssuedDay+"일차 · 이전 구매본",18,30,PaperMuted);
                Print(card,"펼쳐 읽기  ›",18,30,PrintBlue,TextAnchor.MiddleRight);
            }
            Label(content,"새 단서는 최근 "+Intelligence.ArchiveLimit+"건 보관 · 이전 버전 자료도 계속 열람",17,Muted,60);
        }
        void DrawClueReader()
        {
            int index=openedClue.Value;
            var owned=Intelligence.Owned(state,openedClueDay,index,openedClueVersion);
            var top=Row(content,66);var back=Button(top,"‹ 목록",()=>InfoHome(infoShelf),Raised,66);Width((RectTransform)back.transform,112);
            Flexible(Label(top,owned==null?"구매 전 확인":"구매한 정보",27,null,54).rectTransform);
            if(owned!=null){DrawClue(owned);return;}
            if(openedClueDay!=state.Day||openedClueVersion!=Intelligence.CurrentVersion) {
                Label(content,"이 정보의 판매가 끝났습니다. 오늘 입고된 자료를 확인하세요.",23,Gold,100);
                Button(content,"오늘의 정보 보기",()=>InfoHome(0));return;
            }
            var offer=Intelligence.Offer(state.Day,state.Seed,index,eventEdition:state.EventEdition);var tier=offer.Tier;
            var paper=Paper(content,offer.Kind,24);
            Print(paper,offer.Source,25,44,PrintBlue,TextAnchor.MiddleCenter,true);Rule(paper,PrintBlue,3);
            Print(paper,tier.Name,31,62,Ink,TextAnchor.MiddleCenter,true);
            var art=Row(paper,98);if(offer.Targeted)CompanyIcon(art,offer.Company,PrintBlue,80);else Icon(art,7,PrintBlue,80);
            Flexible(Print(art,offer.Targeted?Market.Companies[offer.Company].Name+"\n기업 지정 분석":"기업도 사건도\n아직 알 수 없습니다",24,88,null,TextAnchor.MiddleCenter,true).rectTransform);
            Print(paper,"신뢰도 "+tier.Confidence+"%",32,54,PrintBlue,TextAnchor.MiddleCenter,true);
            Bar(paper,tier.Confidence/100f,PrintBlue,12);
            Print(paper,tier.Detail,23,62,null,TextAnchor.MiddleCenter,true);
            Article(paper,offer.Targeted?"포함: 기업의 사건·운영 지표·구매 당시 시세. 같은 날 이 기업의 봉인 정보와 사건이 겹칠 수 있습니다. 지정 열람료가 가격에 포함됩니다.":"대상 기업·주제·방향은 개봉 후 공개됩니다.",PaperMuted);
            Article(paper,"사건은 다음 날 개장 시세에 반영됩니다. 장중 자동 갱신만으로 정보의 사실 여부를 확인할 수는 없습니다.",PrintBlue);
            Rule(paper,PaperMuted,1);
            Print(paper,"정보의 정확도와 투자 수익은 다를 수 있습니다.",19,54,PaperMuted);
            var related=PurchaseGuidance.RelatedClues(state,offer);
            if(related.Length>0) {
                Rule(paper,PrintBlue,1);Print(paper,"이미 보유한 같은 사건 자료",23,42,PrintBlue,TextAnchor.MiddleLeft,true);
                Article(paper,PurchaseGuidance.OverlapSummary(state,offer),PaperMuted);
                foreach(var item in related) {
                    var record=item;var previous=record.Offer;
                    Button(content,record.IssuedDay+"일차 · "+previous.Grade+"등급 · "+(previous.Targeted?"기업 분석":"구매한 봉인 정보")+" 읽기",
                        ()=>OpenClue(record.IssuedDay,record.Index,record.Version),Raised,66);
                }
            }
            var budget=Card(content,20);
            Article(budget,PurchaseGuidance.BudgetSummary(state.Cash,offer.Price),state.Cash>=offer.Price?Gold:Red);
            var purchase=Button(content,(offer.Targeted?"기업 분석 구매   ":"봉인 해제   ")+Money(offer.Price),()=>{
                Act(()=>Intelligence.Buy(state,index,openedClueDay),"정보를 열었습니다. 근거와 조건을 살펴보세요.");scroll.verticalNormalizedPosition=1;
            },Hex("8C6630"),74);
            purchase.interactable=!blocked&&state.Cash>=offer.Price;
        }
        void DrawClue(ClueRecord record)
        {
            var offer=record.Offer;
            var paper=Paper(content,offer.Kind,24);
            Print(paper,offer.Source,27,48,PrintBlue,TextAnchor.MiddleCenter,true);
            Print(paper,offer.Tier.Name+" · 신뢰도 "+offer.Tier.Confidence+"%",18,32,PaperMuted,TextAnchor.MiddleCenter);
            Print(paper,"취재 "+record.IssuedDay+"일차 · 사건 예정 "+(record.IssuedDay+1)+"일차",18,32,PaperMuted,TextAnchor.MiddleCenter);Rule(paper,PrintBlue,3);
            Print(paper,ClueContent.Headline(offer),30,106,Ink,TextAnchor.MiddleLeft,true);
            if(record.Version>=2&&record.ContentEdition<InformationCatalog.CurrentEdition)Print(paper,"이전 발행판 · 구매 당시 본문 보존",18,32,PaperMuted);
            string reaction=ClueContent.MarketReaction(state,record);
            if(reaction.Length>0) {
                var comparison=Column(paper,16,8);comparison.name="InformationReaction";
                Surface(comparison,Hex("E2ECF3")).raycastTarget=false;
                Print(comparison,"정보와 주가 대조",25,40,PrintBlue,TextAnchor.MiddleLeft,true).name="InformationReactionTitle";
                var result=Print(comparison,reaction,21,0,Ink,TextAnchor.UpperLeft);
                result.name="InformationReactionBody";result.resizeTextForBestFit=false;result.lineSpacing=1.2f;
            } else if(state.Day==record.IssuedDay) {
                Print(paper,"다음 날 개장에 반영 · 장중 갱신은 사건 확인 전 시세입니다.",18,58,PrintBlue);
            }
            Article(paper,ClueContent.Body(offer));
            if(offer.Targeted) {
                Rule(paper,PrintBlue,2);Print(paper,"구매 당시 기업 시세",24,42,PrintBlue,TextAnchor.MiddleLeft,true);
                var chartRect=Rect("PurchasedPriceChart",paper);Height(chartRect,165);var chart=chartRect.gameObject.AddComponent<PriceChart>();chart.Prices=record.Prices;chart.color=PrintBlue;chart.raycastTarget=false;
                Article(paper,ClueContent.Snapshot(record));
            }
            Article(paper,ClueContent.PriceScenario(record),PrintBlue);
            Rule(paper,PaperMuted,1);
            Print(paper,ClueContent.Evidence(offer),19,36,PrintBlue,TextAnchor.MiddleLeft,true);
            if(state.Day>record.IssuedDay) {
                Rule(paper,PrintBlue,2);Print(paper,"후속 보도 · 사건 확인",24,44,PrintBlue,TextAnchor.MiddleLeft,true);
                Article(paper,ClueContent.FollowUp(record));
            } else Print(paper,"다음 날 보관함에서 후속 보도와 대조할 수 있습니다.",18,64,PaperMuted);
            Print(paper,"가상 사건 · 비용과 위험을 함께 판단하세요",16,32,PaperMuted);
            Button(content,offer.Targeted?Market.Companies[offer.Company].Name+" 거래 화면":"거래소에서 직접 종목 고르기",()=>{if(offer.Targeted)selected=offer.Company;SwitchTab(0);},Raised,70);
        }
        void DrawLegacyReader(InfoKind kind)
        {
            var owned=InfoMarket.Owned(state,kind,readerCompany);
            Button(content,"‹ 이전 자료 목록",()=>InfoHome(1),Raised,64);
            Label(content,"이전 버전 구매본 · 발행 당시 내용",18,Gold,34);
            if(kind==InfoKind.Newspaper)DrawNewspaper(owned);
            else if(kind==InfoKind.Report)DrawResearch(owned);
            else if(kind==InfoKind.Briefing)DrawBriefing(owned);
            else DrawRumor(owned);
        }
        void PaperFooter(Transform paper,InfoRecord record)
        {
            Rule(paper,PaperMuted,1);
            Print(paper,"발행 "+record.IssuedDay+"일차  /  유효 "+record.ExpiresDay+"일차",17,32,PaperMuted);
            Print(paper,"게임 속 가상 자료 · 수익 보장 없음",16,32,PaperMuted);
        }
        void DrawNewspaper(InfoRecord r)
        {
            var paper=Paper(content,InfoKind.Newspaper,24);string company=Market.Companies[r.Company].Name;
            Print(paper,"MONEY TOWN  |  ECONOMY DAILY",16,28,PaperMuted,TextAnchor.MiddleCenter);
            Print(paper,"머니타운 경제신문",38,64,Ink,TextAnchor.MiddleCenter,true);Rule(paper,Ink,3);
            Print(paper,"제 "+r.IssuedDay+"호 · 기업·시장면 · 500원",17,30,PaperMuted,TextAnchor.MiddleCenter);Rule(paper,Ink,1);
            Print(paper,company+"\n"+(r.PositiveNews?"늘어나는 수요, 실적에 청신호":"불어나는 비용, 수익성에 경고등"),31,110,Ink,TextAnchor.MiddleLeft,true);
            var picture=Row(paper,130,18);CompanyIcon(picture,r.Company,PrintBlue,112);
            var caption=Column(picture,0,4);Flexible(caption);Print(caption,Market.Companies[r.Company].Sector+" 업종 현장",21,36,PrintBlue,TextAnchor.MiddleLeft,true);
            Print(caption,r.PositiveNews?"주문 확대에 기대감\n매출 개선 가능성 주목":"비용 부담에 우려\n이익 방어 여부 주목",20,80);
            Rule(paper,Ink,1);
            var columns=Row(paper,184,18);
            var article=Column(columns,0,5);Flexible(article);Print(article,"기업 소식",21,34,null,TextAnchor.MiddleLeft,true);
            Print(article,r.PositiveNews?"수요가 늘며 실적 개선에 대한 기대가 커졌다. 다음 시세 갱신에 긍정적 재료가 되지만 다른 변동이 이를 상쇄할 수 있다.":"비용이 늘며 수익성에 대한 우려가 커졌다. 다음 시세 갱신에 부담이 되지만 다른 변동이 이를 상쇄할 수 있다.",20,144);
            var market=Column(columns,0,5);Flexible(market);Print(market,"시장 한눈에",21,34,null,TextAnchor.MiddleLeft,true);
            Print(market,"전일보다 오른 종목은 6개 중 "+r.RisingCount+"개.\n\n이 기업의 발행 시점 가격은 "+Money(r.Prices.Last())+". 기사 방향과 실제 수익은 다를 수 있다.",20,144);
            PaperFooter(paper,r);
        }
        void DrawResearch(InfoRecord r)
        {
            var paper=Paper(content,InfoKind.Report,26);var company=Market.Companies[r.Company];
            Print(paper,"MT RESEARCH  /  COMPANY NOTE",17,30,PrintBlue,TextAnchor.MiddleLeft,true);Rule(paper,PrintBlue,4);
            Print(paper,company.Name,36,62,Ink,TextAnchor.MiddleLeft,true);Print(paper,"기업분석 보고서  ·  "+company.Sector,22,42,PaperMuted);
            var art=Row(paper,110);CompanyIcon(art,r.Company,PrintBlue,96);Flexible(Print(art,"기업의 성향을 읽고\n나의 투자 속도를 정한다",24,96,null,TextAnchor.MiddleCenter,true).rectTransform);
            Print(paper,"01  성장 성향",23,42,PrintBlue,TextAnchor.MiddleLeft,true);
            Print(paper,company.Growth==.002?"높음":"보통",28,44);Bar(paper,company.Growth==.002?1:.55f,PrintBlue,14);
            Print(paper,"기업별 기본 성장 성향입니다. 매일의 상승을 보장하지 않습니다.",20,70,PaperMuted);
            Print(paper,"02  가격 변동성",23,42,PrintBlue,TextAnchor.MiddleLeft,true);
            Print(paper,company.Volatility>=.035?"높음":"보통",28,44);Bar(paper,company.Volatility>=.035?1:.55f,StampRed,14);
            Print(paper,company.Volatility>=.035?"가격 움직임이 큰 편입니다. 매수 수량과 현금 여유를 함께 살펴보세요.":"상대적으로 완만한 편입니다. 뉴스에 따라 평소보다 크게 움직일 수 있습니다.",20,85,PaperMuted);
            PaperFooter(paper,r);
        }
        void DrawBriefing(InfoRecord r)
        {
            var paper=Paper(content,InfoKind.Briefing,24);var prices=r.Prices;long low=prices.Min(),high=prices.Max();
            double change=(prices.Last()/(double)prices.First()-1)*100;
            Print(paper,"MARKET DATA ROOM",18,32,PrintBlue,TextAnchor.MiddleLeft,true);Rule(paper,PrintBlue,3);
            Print(paper,Market.Companies[r.Company].Name,34,58,Ink,TextAnchor.MiddleLeft,true);Print(paper,"시세 브리핑 / "+prices.Length+"일 기록",23,42,PaperMuted);
            var chartRect=Rect("SnapshotChart",paper);Height(chartRect,205);var chart=chartRect.gameObject.AddComponent<PriceChart>();chart.Prices=prices;chart.color=PrintBlue;chart.raycastTarget=false;
            if(prices.Length==1)Print(paper,"기록이 1개라 아직 추세를 판단할 수 없습니다.",20,65,PaperMuted);
            var row=Row(paper,86);var left=Column(row);Flexible(left);Print(left,"기간 최저",18,30,PaperMuted);Print(left,Money(low),28,46);
            var right=Column(row);Flexible(right);Print(right,"기간 최고",18,30,PaperMuted);Print(right,Money(high),28,46);
            Rule(paper,PrintBlue,1);Print(paper,"기록 기간 변화  "+Percent(change),25,48,PrintBlue,TextAnchor.MiddleLeft,true);
            Print(paper,prices.Length==1?"새로운 거래일이 쌓이면 더 긴 가격 흐름을 분석할 수 있습니다.":change>0?"발행 시점에는 기록 시작보다 높은 가격입니다. 최근 상승이 이후에도 이어진다는 뜻은 아닙니다.":change<0?"발행 시점에는 기록 시작보다 낮은 가격입니다. 저점과 현재 가격의 거리를 함께 살펴보세요.":"기록 시작과 마지막 가격이 같습니다. 중간 변동 폭은 차트와 고점·저점에서 확인하세요.",21,116);
            Print(paper,"구매 시점의 기록입니다. 이후 시세가 반영되지 않습니다.",18,65,PaperMuted);PaperFooter(paper,r);
        }
        void DrawRumor(InfoRecord r)
        {
            var paper=Paper(content,InfoKind.Rumor,30);
            Print(paper,"받는 사람: 개인 투자자",18,34,PaperMuted);
            var stamp=Print(paper,"미 확 인",32,64,StampRed,TextAnchor.MiddleRight,true);stamp.rectTransform.localRotation=Quaternion.Euler(0,0,-4);
            Print(paper,"돌아다니는 이야기",31,58,Ink,TextAnchor.MiddleLeft,true);Rule(paper,StampRed,2);
            Print(paper,Market.Companies[r.Company].Name+" 쪽 소식인데…",24,52);
            Print(paper,r.RumorUp?"“내일 매수세가\n붙을 거라는 말이 있어.”":"“내일 매도 물량이\n나올 거라는 말이 있어.”",30,124,Ink,TextAnchor.MiddleCenter,true);
            Print(paper,r.RumorUp?"분위기: 상승 쪽":"분위기: 하락 쪽",24,48,StampRed,TextAnchor.MiddleCenter,true);
            Print(paper,"전달만 받은 거야. 출처는 익명이고,\n반대로 움직여도 이상하지 않아.",23,96,PaperMuted);
            Print(paper,"확인된 기사와 함께 비교할 것!",22,52,StampRed,TextAnchor.MiddleCenter,true);
            PaperFooter(paper,r);
        }
    }
}
