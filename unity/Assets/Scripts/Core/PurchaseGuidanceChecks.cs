using System;
using System.Linq;

namespace MoneyTown
{
    public static class PurchaseGuidanceChecks
    {
        static void Require(bool valid,string message){if(!valid)throw new Exception(message);}
        static void Contains(string text,params string[] expected)
        {Require(expected.All(text.Contains),"Purchase guidance omitted expected budget or overlap details: "+text);}
        static void InvalidBudget(long cash,long price)
        {
            try{PurchaseGuidance.BudgetSummary(cash,price);}catch(ArgumentOutOfRangeException){return;}
            throw new Exception("Invalid purchase budget was accepted.");
        }

        public static int Run()
        {
            Contains(PurchaseGuidance.BudgetSummary(1000000,720000),"720,000원","72%","280,000원");
            Contains(PurchaseGuidance.BudgetSummary(720000,720000),"100%","구매 후 현금  0원");
            Contains(PurchaseGuidance.BudgetSummary(719999,720000),"구매 불가","1원 부족");
            Contains(PurchaseGuidance.BudgetSummary(0,720000),"현금 없음","720,000원 부족");
            Contains(PurchaseGuidance.BudgetSummary(Market.Limit,Market.Limit),"100%","구매 후 현금  0원");
            Contains(PurchaseGuidance.BudgetSummary(Market.Limit,4000),"0.01% 미만");
            InvalidBudget(-1,720000);InvalidBudget(1000000,0);

            var state=Market.Create(42);state.Cash=1000000000;
            var hidden=Intelligence.Offers(state).First(o=>o.Grade<6);
            state=Intelligence.Buy(state,hidden.Index,state.Day);
            var targeted=Intelligence.CompanyOffers(state,hidden.Company).First();
            Require(PurchaseGuidance.RelatedClues(state,targeted).Length==0,"A lower-grade purchase revealed its company.");
            state=Intelligence.Buy(state,targeted.Index,state.Day);
            Require(PurchaseGuidance.RelatedClues(state,hidden).Length==0&&PurchaseGuidance.OverlapSummary(state,hidden)=="",
                "A sealed offer revealed a connection before purchase.");
            Require(PurchaseGuidance.RelatedClues(state,Intelligence.CompanyOffers(state,(hidden.Company+1)%Market.Companies.Length).Last()).Length==0,
                "Unrelated companies were marked as duplicate information.");
            var higher=Intelligence.CompanyOffers(state,hidden.Company)[1];
            Contains(PurchaseGuidance.OverlapSummary(state,higher),"1건","취재 깊이를 더합니다","차트·기간 고저","판단이 달라질 수 있습니다");
            state=Intelligence.Buy(state,higher.Index,state.Day);
            Contains(PurchaseGuidance.OverlapSummary(state,targeted),"더 높은 등급","늘어나지는 않습니다");
            state=Market.AcknowledgeSettlement(Market.NextDay(state));
            Require(PurchaseGuidance.RelatedClues(state,Intelligence.CompanyOffers(state,hidden.Company).Last()).Length==0,
                "An older purchase was marked as today's same event.");

            foreach(int grade in Intelligence.CompanyGrades) {
                GameState candidate=null;IntelOffer sealedOffer=default;
                for(int seed=0;seed<1000;seed++) {
                    var fresh=Market.Create(seed);var offers=Intelligence.Offers(fresh).Where(o=>o.Grade==grade).ToArray();
                    if(offers.Length==0)continue;candidate=fresh;sealedOffer=offers[0];break;
                }
                Require(candidate!=null,"No same-grade sealed offer found for overlap check.");
                candidate.Cash=1000000000;candidate=Intelligence.Buy(candidate,sealedOffer.Index,candidate.Day);
                var offer=Intelligence.CompanyOffers(candidate,sealedOffer.Company).Single(o=>o.Grade==grade);
                var before=SaveCodec.Encode(candidate);
                var related=PurchaseGuidance.RelatedClues(candidate,offer);
                Require(related.Length==1&&related[0].Index==sealedOffer.Index,"Same-grade purchased sealed offer missing.");
                Contains(PurchaseGuidance.OverlapSummary(candidate,offer),"같은 등급","취재 판단도 같습니다","다시 뽑지 않습니다","보고서 본문");
                Require(sealedOffer.ClaimedPositive==offer.ClaimedPositive,"Overlap claim did not match the existing world rule.");
                Require(before.SequenceEqual(SaveCodec.Encode(candidate)),"Purchase guidance mutated the game.");
            }
            return 3;
        }
    }
}
