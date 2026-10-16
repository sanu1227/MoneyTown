using System;
using System.Linq;

namespace MoneyTown
{
    public sealed class IntelTier
    {
        public readonly string Name, Detail;
        public readonly long Price;
        public readonly int Confidence;
        public IntelTier(string name,long price,int confidence,string detail)
        {Name=name;Price=price;Confidence=confidence;Detail=detail;}
    }
    public readonly struct IntelOffer
    {
        public readonly int Index,Company,Story,Grade,Version,ContentEdition;
        public readonly InfoKind Kind;
        internal readonly bool ActualPositive,ClaimedPositive;
        internal IntelOffer(int index,int company,int story,InfoKind kind,bool actual,bool claimed,int grade=0,int version=1,int contentEdition=1)
        {Index=index;Company=company;Story=story;Kind=kind;ActualPositive=actual;ClaimedPositive=claimed;Grade=grade;Version=version;ContentEdition=contentEdition;}
        public bool Targeted => Version>=2 && Index>=Intelligence.DailyCountFor(Version);
        public CatalogClue Content => InformationCatalog.Get(Grade,Story,ContentEdition);
        public IntelTier Tier => Version==1?LegacyIntelligence.Tiers[Index]:Intelligence.Tiers[Grade-1];
        public long Price => Version==1?Tier.Price:Content.Price*(Targeted?2:1);
        public string Source => Version==1?LegacyIntelligence.Sources[(int)Kind]:Targeted?"기업 지정 분석서":Content.Source;
    }
    [Serializable]
    public sealed class ClueRecord
    {
        public long IssuedDay,Seed,PaidPrice;
        public int Index,Version=1,MarketVersion=1,ContentEdition=1;
        public int EventEdition=InformationCatalog.CurrentEdition;
        public bool BeforePriceMigration;
        public long[] Prices=Array.Empty<long>();
        public ClueRecord Copy() {var copy=(ClueRecord)MemberwiseClone();copy.Prices=(long[])Prices.Clone();return copy;}
        public IntelOffer Offer => Version==1?LegacyIntelligence.Offers(IssuedDay,Seed)[Index]:Intelligence.Offer(IssuedDay,Seed,Index,ContentEdition,Version,EventEdition);
    }
    public static class Intelligence
    {
        public const int CurrentVersion=3,ArchiveLimit=180,DailyCount=9,OfferCount=45;
        public static int DailyCountFor(int version)=>version==2?6:DailyCount;
        public static readonly int[] CompanyGrades={6,8,10};
        public static readonly IntelTier[] Tiers={
            new IntelTier("1등급 · 희미한 소문",5000,50,"현장 흔적과 빠진 맥락 · 대상 미상"),
            new IntelTier("2등급 · 목격 단서",12000,56,"목격담과 불확실한 일정"),
            new IntelTier("3등급 · 지역 취재",30000,62,"업종을 연결할 주변 단서"),
            new IntelTier("4등급 · 업계 취재",75000,68,"사건과 업종 · 기업명 미포함"),
            new IntelTier("5등급 · 교차 조사",180000,75,"사건의 파급 경로와 확인 근거"),
            new IntelTier("6등급 · 기업 확인",450000,82,"기업명과 운영 지표의 구체적 추정"),
            new IntelTier("7등급 · 정밀 분석",1200000,88,"원문·일정·위험 조건 대조"),
            new IntelTier("8등급 · 심층 검증",3000000,93,"지표 범위와 조건별 민감도"),
            new IntelTier("9등급 · 전략 분석",7500000,97,"반대 시나리오와 재검토 기준"),
            new IntelTier("10등급 · 최상위 실사",20000000,99,"사건 기여도·가격 범위·교차 검증")
        };
        static void Require(bool valid,string message) {if(!valid)throw new InvalidOperationException(message);}
        static uint Mix(uint x) {unchecked{x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}}
        static uint Key(long day,long seed)=>Mix(unchecked((uint)seed^(uint)day*0x9e3779b9u));
        static uint Identity(long day,long seed,int company)=>Mix(Key(day,seed)^unchecked((uint)(company+1)*0xc2b2ae35u));
        public static IntelOffer[] Offers(GameState s)=>Enumerable.Range(0,DailyCount).Select(i=>Offer(s.Day,s.Seed,i,eventEdition:s.EventEdition)).ToArray();
        public static IntelOffer[] CompanyOffers(GameState s,int company)
        {
            Require(company>=0&&company<Market.Companies.Length,"기업을 확인하세요.");
            return Enumerable.Range(0,3).Select(i=>Offer(s.Day,s.Seed,DailyCount+company*3+i,eventEdition:s.EventEdition)).ToArray();
        }
        internal static IntelOffer Offer(long day,long seed,int index,int contentEdition=InformationCatalog.CurrentEdition,int version=CurrentVersion,int eventEdition=InformationCatalog.CurrentEdition)
        {
            int daily=DailyCountFor(version),companies=version==2?6:Market.Companies.Length,band=version==2?2:3;
            Require((version==2||version==CurrentVersion)&&index>=0&&index<daily+companies*3,"정보 상품을 확인하세요.");
            int company,grade;uint key=Key(day,seed);
            if(index<daily) {
                company=Enumerable.Range(0,companies).OrderBy(i=>Mix(key^unchecked((uint)(i+1)*0x85ebca6bu))).ElementAt(index);
                grade=(index<band?1:index<band*2?4:7)+(int)(Mix(key^unchecked((uint)(index+1)*0x371bc61du))%(index<band*2?3u:4u));
            } else {company=(index-daily)/3;grade=CompanyGrades[(index-daily)%3];}
            uint identity=Identity(day,seed,company);
            int selectionEdition=version==2?contentEdition:company<6?eventEdition:InformationCatalog.CurrentEdition;
            int eventId=InformationCatalog.EventFor(company,Mix(identity^0x37938CA1u),selectionEdition);
            bool actual=(Mix(identity^0x761D41A9u)&1)==1;
            // Identical company/grade/day has one claim, regardless of sales channel.
            bool correct=Mix(identity^unchecked((uint)grade*0xA18439B5u))%100<Tiers[grade-1].Confidence;
            var content=InformationCatalog.Get(grade,eventId,contentEdition);
            return new IntelOffer(index,company,eventId,index<daily?content.Kind:InfoKind.Report,actual,correct?actual:!actual,grade,version,contentEdition);
        }
        public static ClueRecord Owned(GameState s,long day,int index,int version=CurrentVersion)
        {
            var exact=s.Clues.FirstOrDefault(r=>r.IssuedDay==day&&r.Index==index&&r.Version==version);
            if(exact!=null||version!=CurrentVersion||day!=s.Day)return exact;
            var offer=Offer(day,s.Seed,index,eventEdition:s.EventEdition);
            return s.Clues.FirstOrDefault(r=>r.IssuedDay==day&&r.Version==2&&r.Offer.Company==offer.Company
                &&r.Offer.Grade==offer.Grade&&r.Offer.Targeted==offer.Targeted&&r.Offer.Story==offer.Story);
        }
        public static GameState Buy(GameState previous,int index,long offerDay)
        {
            Require(!previous.SettlementPending,"전날 결산을 먼저 확인하세요.");
            Require(offerDay==previous.Day,"판매 날짜가 지났습니다. 오늘 목록을 다시 확인하세요.");
            var offer=Offer(previous.Day,previous.Seed,index,eventEdition:previous.EventEdition);
            Require(Owned(previous,offerDay,index)==null,"이미 구매한 정보입니다.");
            Require(previous.Cash>=offer.Price,"정보 구매 비용이 부족합니다.");
            var s=previous.Copy();s.Cash-=offer.Price;s.DayInformationCost=checked(s.DayInformationCost+offer.Price);
            var record=new ClueRecord{Version=CurrentVersion,MarketVersion=s.MarketVersion,ContentEdition=InformationCatalog.CurrentEdition,EventEdition=s.EventEdition,IssuedDay=s.Day,Seed=s.Seed,Index=index,PaidPrice=offer.Price,Prices=(long[])s.Stocks[offer.Company].History.Clone()};
            s.Clues=s.Clues.Concat(new[]{record}).Skip(Math.Max(0,s.Clues.Length+1-ArchiveLimit)).ToArray();
            return Market.Validate(s);
        }
        // Every company has one daily event. Information purchase never changes it.
        internal static double EventEffect(GameState s,int company)
        {var offer=EventOffer(s,company);return (offer.ActualPositive?1:-1)*offer.Content.Impact/10000.0;}
        internal static IntelOffer EventOffer(GameState s,int company)=>Offer(s.Day,s.Seed,DailyCount+company*3,eventEdition:s.EventEdition);
        internal static void Validate(GameState s)
        {
            const string damaged="단서 보관 데이터가 손상되었습니다.";
            Require(s.Clues!=null&&s.Clues.Length<=ArchiveLimit,damaged);
            foreach(var r in s.Clues) {
                Require(r!=null&&r.IssuedDay>=1&&r.IssuedDay<=s.Day&&r.Seed>=0&&r.Seed<=uint.MaxValue,damaged);
                Require(r.Version>=1&&r.Version<=CurrentVersion&&r.Index>=0&&r.Index<(r.Version==1?3:r.Version==2?24:OfferCount),damaged);
                Require(r.IssuedDay!=s.Day||r.Seed==s.Seed,damaged);
                Require(r.Prices!=null&&r.MarketVersion>=1&&r.MarketVersion<=Market.CurrentMarketVersion,damaged);
                Require(r.MarketVersion<3||r.Version==CurrentVersion,damaged);
                Require(r.ContentEdition>=1&&r.ContentEdition<=InformationCatalog.CurrentEdition,damaged);
                Require(r.Version<3?r.ContentEdition<=2:r.ContentEdition==3&&r.EventEdition>=2&&r.EventEdition<=3,damaged);
                if(r.Version==1)Require(r.PaidPrice==0&&r.Prices.Length==0,damaged);
                else Require(r.PaidPrice==r.Offer.Price&&r.Prices.Length>=1&&r.Prices.Length<=30&&r.Prices.All(p=>p>=100&&p<=9000000000000000),damaged);
            }
            Require(s.Clues.Select(r=>(r.IssuedDay,r.Version,r.Index)).Distinct().Count()==s.Clues.Length,damaged);
        }
    }
}
