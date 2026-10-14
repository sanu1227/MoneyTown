using System;
using System.Linq;
namespace MoneyTown
{
    // Frozen 0.7 offers: purchased records must retain their original wording and odds.
    internal static class LegacyIntelligence
    {
        public static readonly IntelTier[] Tiers = {
            new IntelTier("거리의 소문",300,55,"짧은 정황 · 장소와 대상 불명"),
            new IntelTier("취재 자료",1000,80,"사건과 업종을 추론할 단서"),
            new IntelTier("심층 취재",2500,95,"기업·일정·조건까지 구체적 근거")
        };
        public static readonly string[] Sources={"경제신문 스크랩","현장 조사 보고서","취재 브리핑","익명의 쪽지"};
        static void Require(bool valid,string message)
        { if(!valid)throw new InvalidOperationException(message); }
        static uint Mix(uint x)
        { unchecked { x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16); } }
        internal static IntelOffer[] Offers(long day,long seed)
        {
            uint key=Mix(unchecked((uint)seed ^ (uint)day*0x9e3779b9u));
            var companies=Enumerable.Range(0,6).OrderBy(i=>Mix(key^(uint)(i+1)*0x85ebca6bu)).ToArray();
            var offers=new IntelOffer[3];
            for(int i=0;i<offers.Length;i++) {
                uint identity=Mix(key^(uint)(i+1)*0xc2b2ae35u);
                bool actual=(Mix(identity^0x761D41A9u)&1)==1;
                bool correct=Mix(identity^0xA18439B5u)%100<Tiers[i].Confidence;
                offers[i]=new IntelOffer(i,companies[i],companies[i]*2+(int)(Mix(identity^0x37938CA1u)%2),
                    (InfoKind)(Mix(identity^0x57CB817Fu)%4),actual,correct?actual:!actual);
            }
            return offers;
        }
    }
}
