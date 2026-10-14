using System;
using System.Globalization;
using System.Linq;

namespace MoneyTown
{
    public static class PurchaseGuidance
    {
        static readonly CultureInfo Korean=CultureInfo.GetCultureInfo("ko-KR");
        static string Money(long value)=>value.ToString("N0",Korean)+"원";

        public static string BudgetSummary(long cash,long price)
        {
            if(cash<0||price<=0)throw new ArgumentOutOfRangeException();
            decimal percent=cash==0?0:(decimal)price*100/cash;
            string share=cash==0?"현금 없음":percent<.01m?"0.01% 미만":percent.ToString("0.##",Korean)+"%";
            string remaining=cash>=price?Money(cash-price):"구매 불가 · "+Money(price-cash)+" 부족";
            return "정보 비용  "+Money(price)+"\n현재 현금 대비  "+share+"\n구매 후 현금  "+remaining;
        }

        public static ClueRecord[] RelatedClues(GameState state,IntelOffer offer)
        {
            // Sealed offers and lower-grade purchases must not reveal their company by association.
            if(!offer.Targeted)return Array.Empty<ClueRecord>();
            return state.Clues.Where(r=>r.Version>=2&&r.IssuedDay==state.Day)
                .Where(r=>r.Offer.Grade>=6&&r.Offer.Company==offer.Company&&(r.Version!=offer.Version||r.Index!=offer.Index))
                .OrderByDescending(r=>r.Offer.Grade).ThenBy(r=>r.Index).ToArray();
        }

        public static string OverlapSummary(GameState state,IntelOffer offer)
        {
            var related=RelatedClues(state,offer);if(related.Length==0)return "";
            string summary="이미 가진 오늘의 자료 "+related.Length+"건과 같은 사건을 다룹니다.";
            if(related.Any(r=>r.Offer.Grade==offer.Grade))
                summary+="\n같은 등급은 사실 여부에 대한 취재 판단도 같습니다. 구매 채널을 바꿔도 다시 뽑지 않습니다.";
            int highest=related.Max(r=>r.Offer.Grade);
            if(offer.Grade>highest)summary+="\n이번 등급은 "+offer.Tier.Detail+"까지 취재 깊이를 더합니다.";
            else if(offer.Grade<highest)summary+="\n더 높은 등급을 이미 보유해 사건의 취재 깊이가 늘어나지는 않습니다.";
            if(related.Any(r=>r.Offer.Grade!=offer.Grade))
                summary+="\n등급이 다르면 취재 깊이와 사실 여부에 대한 판단이 달라질 수 있습니다.";
            return summary+"\n기업 분석에는 보고서 본문과 이번 구매 시점의 차트·기간 고저가 포함됩니다. 새로운 사건을 추가하는 구매는 아닙니다.";
        }
    }
}
