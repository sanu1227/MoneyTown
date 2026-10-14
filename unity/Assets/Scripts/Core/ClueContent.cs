using System;
using System.Globalization;
using System.Linq;

namespace MoneyTown
{
    public static class ClueContent
    {
        public static string Headline(IntelOffer offer)=>offer.Version==1?LegacyClueContent.Headline(offer):
            offer.ClaimedPositive?offer.Content.PositiveTitle:offer.Content.NegativeTitle;
        public static string Body(IntelOffer offer)=>offer.Version==1?LegacyClueContent.Body(offer):
            offer.Targeted?(offer.ClaimedPositive?offer.Content.ReportPositiveBody:offer.Content.ReportNegativeBody):
            offer.ClaimedPositive?offer.Content.PositiveBody:offer.Content.NegativeBody;
        public static string Evidence(IntelOffer offer)=>offer.Version==1?LegacyClueContent.Evidence(offer):
            offer.Grade<4?"출처 교차 확인 부족":offer.Grade<7?"사건 취재 · 일부 조건 미확인":"원문·담당자·조건 교차 확인";
        public static string FollowUp(ClueRecord record)
        {
            if(record.Version==1)return LegacyClueContent.FollowUp(record);
            var offer=record.Offer;
            string result=(offer.ActualPositive==offer.ClaimedPositive?"취재 내용과 일치\n":"취재 내용과 불일치\n")+
                (offer.ActualPositive?offer.Content.ActualPositive:offer.Content.ActualNegative)+".";
            if(record.ContentEdition>=2)result+="\n\n추가 점검 | 주요 변수는 "+offer.Content.Risk+"이다. 이번 확인은 앞서 전한 사안의 진행 여부에 한정된다. 기업 실적에 미친 영향은 후속 자료를 통해 확인할 필요가 있다.";
            return result;
        }
        static string Money(long value)=>value.ToString("N0",CultureInfo.GetCultureInfo("ko-KR"))+"원";
        static string Signed(double value)=>value.ToString("+0.00;-0.00;0.00",CultureInfo.GetCultureInfo("ko-KR"));
        public static string MarketReaction(GameState state,ClueRecord record)
        {
            // Only settled, newly purchased articles have this comparison. Never reveal today's target.
            if(record.MarketVersion<3||state.Day<=record.IssuedDay)return "";
            var offer=record.Offer;
            var report=state.DayReports.FirstOrDefault(r=>r.Day==record.IssuedDay);
            var news=report?.News.FirstOrDefault(n=>n.Company==offer.Company);
            if(news==null)return "해당 날짜의 개장 가격 기록이 없습니다. 주가 대조는 최근 30일 기록이 있는 경우에 제공됩니다. 사건 확인은 아래 후속 보도에서 볼 수 있습니다.";
            bool flat=news.OpeningPrice==news.PreviousPrice;
            bool up=news.OpeningPrice>news.PreviousPrice;
            string fact=offer.ActualPositive?"호재":"악재";
            string reaction=flat?"최저 거래가격 또는 가격 반올림으로 시가는 종가와 같습니다.":
                up==offer.ActualPositive?$"확인된 {fact}가 {(up?"상승":"하락")}에 반영됐습니다.":
                $"확인된 {fact}보다 반대 방향의 개장 수급이 강해 {(up?"상승":"하락")}했습니다.";
            return (offer.ClaimedPositive==offer.ActualPositive?"취재 내용 일치":"취재 내용 불일치")+
                $" · 실제 사건은 {fact}\n관련 기업 · {Market.Companies[offer.Company].Name}\n"+
                $"{record.IssuedDay}일차 종가 {Money(news.PreviousPrice)} → {record.IssuedDay+1}일차 시가 {Money(news.OpeningPrice)}\n"+
                $"개장 등락 {Signed(((double)news.OpeningPrice/news.PreviousPrice-1)*100)}%\n\n"+reaction+
                $"\n사건 영향 {Signed(news.EventImpact*100)}%p · 수급·개장 조정 {Signed(news.MarketImpact*100)}%p\n"+
                "종가 대비 개장 반응입니다. 구매 이후 장중 변동·거래 수수료·정보 비용은 포함하지 않습니다.";
        }
        public static string Snapshot(ClueRecord record)
        {
            var offer=record.Offer;if(record.Version==1||!offer.Targeted)return "";
            var p=record.Prices;var company=Market.Companies[offer.Company];
            string period=record.MarketVersion==1?p.Length+"일 기록":"최근 "+p.Length+"개 시세";
            string snapshot=$"구매 당시 {company.Name} | 시세 {Money(p.Last())} · {period}\n기간 최저 {Money(p.Min())} / 최고 {Money(p.Max())}";
            string volatility=(Market.OpeningVolatility(offer.Company,record.MarketVersion)*100).ToString(record.MarketVersion>=3?"0.00":"0.0",CultureInfo.GetCultureInfo("ko-KR"));
            if(offer.Grade>=8)snapshot+=$"\n\n가격 성향 | {(record.MarketVersion==1?"평소 변동 범위":"개장 무작위 변동 성향")} ±{volatility}% · 기본 성장 성향 +{company.Growth*100:0.0}%. 사건과 무작위 변동이 더해져 최종 시세가 정해진다.";
            return snapshot;
        }
        public static string PriceScenario(ClueRecord record)
        {
            var offer=record.Offer;if(record.Version==1||offer.Grade<10)return "";
            var company=Market.Companies[offer.Company];
            double center=company.Growth+(offer.ClaimedPositive?1:-1)*offer.Content.Impact/10000.0;
            long price=record.Prices.Last();
            double volatility=Market.OpeningVolatility(offer.Company,record.MarketVersion);
            double lowChange=Math.Max(-.12,center-volatility),highChange=Math.Min(.12,center+volatility);
            if(record.MarketVersion>=2){lowChange=Market.OpeningChange(lowChange);highChange=Market.OpeningChange(highChange);}
            long low=Math.Max(100,(long)Math.Floor(price*(1+lowChange)+.5));
            long high=Math.Max(100,(long)Math.Floor(price*(1+highChange)+.5));
            if(record.MarketVersion>=2)return $"고정 기준 개장 시나리오 | {Money(low)}~{Money(high)}\n구매 당시 {Money(price)} 이후 장중 추가 변동이 없고 취재가 사실이라는 가정입니다. 장중 갱신마다 기준 시세가 달라질 수 있어 실제 익일 가격 범위를 보장하지 않습니다. 수수료는 제외합니다.";
            return $"조건부 시세 범위 | {Money(low)}~{Money(high)}\n구매 당시 {Money(price)}에서 계산. 이 취재 내용이 사실인 경우의 다음 거래일 범위다. 거짓 정보라면 이 범위도 무효이며, 수수료는 포함하지 않는다.";
        }
    }
}
