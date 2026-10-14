using System;
using System.Globalization;

namespace MoneyTown
{
    [Serializable]
    public sealed class MarketNews
    {
        public int Company;
        public long PreviousPrice,OpeningPrice;
        public string Headline,Body;
        public double EventImpact,MarketImpact;
        public MarketNews Copy()=>(MarketNews)MemberwiseClone();

        internal static MarketNews Create(IntelOffer offer,long previous,long opening,double eventImpact,double marketImpact)
        {
            var company=Market.Companies[offer.Company];
            bool up=opening>previous,flat=opening==previous;
            string fact=offer.ActualPositive?offer.Content.ActualPositive:offer.Content.ActualNegative;
            string movement=flat?"보합":up?"상승":"하락";
            string interpretation;
            if(flat)interpretation=opening==100?"하락 압력이 이어졌지만 최저 거래가격인 100원에 머물렀다.":"관련 소식과 개장 수급이 맞물렸지만 정수 가격으로 반영된 주가는 전일 종가와 같았다.";
            else if(up==offer.ActualPositive)interpretation=up
                ?"이번 소식이 사업 전망에 대한 기대를 높이며 개장 가격에 반영됐다."
                :"이번 소식으로 사업 차질에 대한 우려가 커지면서 개장 가격에 부담을 줬다.";
            else interpretation=up
                ?"사업에 부담이 되는 소식에도 개장 매수세가 이를 상쇄하면서 주가는 올랐다."
                :"사업에 긍정적인 소식이 전해졌지만 개장 매도세가 더 크게 작용하면서 주가는 내렸다.";
            string flow=marketImpact>0?"사업 소식 외 시장 여건과 개장 수급은 주가를 끌어올리는 쪽으로 작용했다.":"사업 소식 외 시장 여건과 개장 수급은 주가를 누르는 쪽으로 작용했다.";
            double percent=((double)opening/previous-1)*100;
            string quote=$"{company.Name}의 개장 가격은 {opening.ToString("N0",CultureInfo.GetCultureInfo("ko-KR"))}원으로 형성됐다. "+(flat?"전일 종가와 같다.":$"전일 종가 {previous.ToString("N0",CultureInfo.GetCultureInfo("ko-KR"))}원 대비 {Math.Abs(percent):0.00}% {movement}했다.");
            return new MarketNews{Company=offer.Company,PreviousPrice=previous,OpeningPrice=opening,
                Headline=$"{company.Name} {movement}… {fact}",
                Body=quote+"\n\n"+fact.TrimEnd('.',' ')+". "+interpretation+" "+flow,
                EventImpact=eventImpact,MarketImpact=marketImpact};
        }
    }
}
