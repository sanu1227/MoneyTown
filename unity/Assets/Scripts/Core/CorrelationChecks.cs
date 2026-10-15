using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MoneyTown
{
    // These checks exercise production day transitions; only the frozen old price formula is reproduced.
    public static class CorrelationChecks
    {
        public sealed class Measurement
        {
            public readonly int[] Samples=new int[12],OldMatches=new int[12],NewMatches=new int[12];
            public readonly int[] GradeSamples=new int[10],TrueClaims=new int[10],PriceMatches=new int[10];
            public int PositiveFalls,NegativeRises;
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static GameState Next(GameState state)=>Market.AcknowledgeSettlement(Market.NextDay(state));
        static string Money(long value)=>value.ToString("N0",CultureInfo.GetCultureInfo("ko-KR"))+"원";
        static ClueRecord Record(GameState state,IntelOffer offer,int marketVersion=Market.CurrentMarketVersion)=>new ClueRecord {
            Version=offer.Version,MarketVersion=marketVersion,ContentEdition=offer.ContentEdition,EventEdition=state.EventEdition,
            IssuedDay=state.Day,Seed=state.Seed,Index=offer.Index,PaidPrice=offer.Price,Prices=(long[])state.Stocks[offer.Company].History.Clone()
        };
        public static Measurement Measure(int seedCount=128,int days=8)
        {
            if(seedCount<1||seedCount>10000||days<1||days>30)throw new ArgumentOutOfRangeException();
            var measured=new Measurement();
            for(int seed=1;seed<=seedCount;seed++) {
                var before=Market.Create(seed);
                for(int day=0;day<days;day++) {
                    var after=Market.NextDay(before);long oldSeed=before.Seed;
                    for(int company=0;company<Market.Companies.Length;company++) {
                        var offer=Intelligence.EventOffer(before,company);var definition=Market.Companies[company];
                        oldSeed=(oldSeed*1664525+1013904223)&uint.MaxValue;
                        double oldChange=Math.Max(-.12,Math.Min(.12,definition.Growth+Intelligence.EventEffect(before,company)
                            +(oldSeed/4294967296.0*2-1)*definition.Volatility));
                        oldChange=(oldChange<0?-1:1)*Math.Max(.015,Math.Abs(oldChange));
                        long close=before.Stocks[company].Price;
                        long oldOpen=Math.Max(100,(long)Math.Floor(close*(1+oldChange)+.5));
                        long newOpen=after.Stocks[company].Price;
                        measured.Samples[company]++;
                        if(oldOpen!=close&&(oldOpen>close)==offer.ActualPositive)measured.OldMatches[company]++;
                        if(newOpen!=close&&(newOpen>close)==offer.ActualPositive)measured.NewMatches[company]++;
                        if(offer.ActualPositive&&newOpen<close)measured.PositiveFalls++;
                        if(!offer.ActualPositive&&newOpen>close)measured.NegativeRises++;
                    }
                    // Two News() draws follow the twelve price draws. Their preservation keeps future events unchanged.
                    oldSeed=(oldSeed*1664525+1013904223)&uint.MaxValue;
                    oldSeed=(oldSeed*1664525+1013904223)&uint.MaxValue;
                    Check(oldSeed==after.Seed,"Changing opening noise changed the daily RNG sequence.");
                    foreach(var offer in Intelligence.Offers(before)) {
                        int grade=offer.Grade-1;measured.GradeSamples[grade]++;
                        if(offer.ClaimedPositive==offer.ActualPositive)measured.TrueClaims[grade]++;
                        long close=before.Stocks[offer.Company].Price,open=after.Stocks[offer.Company].Price;
                        if(open!=close&&(open>close)==offer.ClaimedPositive)measured.PriceMatches[grade]++;
                    }
                    before=Market.AcknowledgeSettlement(after);
                }
            }
            return measured;
        }
        public static int Run()
        {
            CheckCorrelation();CheckPurchaseAndReplay();CheckScenarios();CheckReactionHistory();CheckReactionOutcomes();return 5;
        }
        static void CheckCorrelation()
        {
            var m=Measure();int count=m.Samples.Sum(),oldMatches=m.OldMatches.Sum(),newMatches=m.NewMatches.Sum();
            Check(count==12288&&newMatches>=count*.95&&newMatches-oldMatches>=count*.08,
                "Confirmed events no longer explain at least 95% of sampled opening directions or improve by eight points.");
            Check(m.PositiveFalls>0&&m.NegativeRises>0,"Information must not guarantee the stock direction.");
            for(int company=0;company<12;company++)Check(m.NewMatches[company]>=m.Samples[company]*.90
                &&m.NewMatches[company]>=m.OldMatches[company],"A company's information correlation regressed.");
            for(int grade=0;grade<10;grade++)Check(m.GradeSamples[grade]>0
                &&Math.Abs(100.0*m.TrueClaims[grade]/m.GradeSamples[grade]-Intelligence.Tiers[grade].Confidence)<6,
                "An information grade's factual reliability changed.");
            Check((double)m.PriceMatches[9]/m.GradeSamples[9]>.90
                &&(double)m.PriceMatches[9]/m.GradeSamples[9]>(double)m.PriceMatches[0]/m.GradeSamples[0]+.3,
                "The best grade lost its meaningful prediction advantage.");
        }
        static void CheckPurchaseAndReplay()
        {
            var before=Market.Create(122);before.Cash=before.DayStartAssets=100000000;
            var bought=Intelligence.Buy(before,Intelligence.DailyCount+5,before.Day);
            Check(bought.Clues.Single().MarketVersion==Market.CurrentMarketVersion,"New purchases did not pin the new price model.");
            var traded=Market.Trade(bought,1,2,true);traded=Market.Trade(traded,1,1,false);
            var restored=SaveCodec.Decode(SaveCodec.Encode(traded));
            Check(restored.Clues.Single().MarketVersion==Market.CurrentMarketVersion,"Save/reload lost the purchase's price model.");
            var expected=Market.NextDay(before);
            foreach(var state in new[]{bought,traded,restored}) {
                var next=Market.NextDay(state);
                Check(next.Seed==expected.Seed&&next.Stocks.Select(s=>s.Price).SequenceEqual(expected.Stocks.Select(s=>s.Price)),
                    "Buying information, trading or reloading rerolled opening prices.");
            }
            var waited=before;for(int i=0;i<20;i++)waited=Market.Intraday(waited);
            for(int company=0;company<12;company++)Check(Intelligence.EventEffect(before,company)==Intelligence.EventEffect(waited,company),
                "Waiting for intraday ticks rerolled a known event.");
        }
        static void CheckScenarios()
        {
            int truthCases=0,falseCases=0;
            for(int seed=1;seed<=32;seed++) {
                var before=Market.Create(seed);var after=Market.NextDay(before);
                for(int company=0;company<12;company++) {
                    var offer=Intelligence.CompanyOffers(before,company).Last();var record=Record(before,offer);
                    var bounds=Regex.Match(ClueContent.PriceScenario(record),@"([\d,]+)원~([\d,]+)원");
                    Check(bounds.Success,"Top-grade scenario did not expose its conditional price interval.");
                    long low=long.Parse(bounds.Groups[1].Value.Replace(",","")),high=long.Parse(bounds.Groups[2].Value.Replace(",",""));
                    if(offer.ClaimedPositive==offer.ActualPositive) {
                        truthCases++;Check(after.Stocks[company].Price>=low&&after.Stocks[company].Price<=high,
                            "A confirmed grade-ten scenario missed the actual opening without intervening ticks.");
                    } else falseCases++;
                    var legacy=record.Copy();legacy.MarketVersion=2;
                    string scenario=ClueContent.PriceScenario(legacy);var definition=Market.Companies[company];
                    double center=definition.Growth+(offer.ClaimedPositive?1:-1)*offer.Content.Impact/10000.0;
                    Func<double,long> oldPrice=change=>Math.Max(100,(long)Math.Floor(record.Prices.Last()*
                        (1+(change<0?-1:1)*Math.Max(.015,Math.Abs(change)))+.5));
                    string oldRange=Money(oldPrice(Math.Max(-.12,center-definition.Volatility)))+"~"+
                        Money(oldPrice(Math.Min(.12,center+definition.Volatility)));
                    Check(scenario.StartsWith("고정 기준 개장 시나리오 | "+oldRange),"An already paid version-two scenario changed its price range.");
                    Check(Market.OpeningVolatility(company,1)==definition.Volatility&&Market.OpeningVolatility(company,2)==definition.Volatility,
                        "Legacy market versions inherited the new volatility.");
                }
            }
            Check(truthCases>350&&falseCases>0,"Scenario coverage lost true or false reporting cases.");
        }
        static void CheckReactionHistory()
        {
            var before=Market.Create(42);before.Cash=before.DayStartAssets=100000000;
            before=Intelligence.Buy(before,0,before.Day);var record=before.Clues.Single();
            Check(ClueContent.MarketReaction(before,record)=="","Current-day feedback revealed tomorrow's target or price.");
            var future=record.Copy();future.IssuedDay++;
            Check(ClueContent.MarketReaction(before,future)=="","Future feedback was exposed.");
            var after=Next(before);string reaction=ClueContent.MarketReaction(after,record);
            var news=after.LastReport.News.Single(n=>n.Company==record.Offer.Company);
            Check(reaction.Contains(Money(news.PreviousPrice))&&reaction.Contains(Money(news.OpeningPrice))
                &&reaction.Contains(Market.Companies[record.Offer.Company].Name),"Feedback quoted the wrong company or historical prices.");
            var later=Market.Intraday(Next(after));
            Check(ClueContent.MarketReaction(later,record)==reaction,"Later quotes or a newer settlement rewrote historical feedback.");
            var saved=SaveCodec.Decode(SaveCodec.Encode(later));
            Check(ClueContent.MarketReaction(saved,saved.Clues.Single())==reaction,"Reload changed historical market feedback.");
            foreach(int version in new[]{1,2}) {var old=record.Copy();old.MarketVersion=version;
                Check(ClueContent.MarketReaction(later,old)=="","A legacy purchase acquired invented price feedback.");}
            while(later.Day<=record.IssuedDay+Market.DayHistoryLimit)later=Next(later);
            Check(ClueContent.MarketReaction(later,record).Contains("개장 가격 기록이 없습니다"),"Expired history fell back to current prices.");
        }
        static void CheckReactionOutcomes()
        {
            int trueReports=0,falseReports=0,oppositeMoves=0,flatMoves=0;
            for(int seed=1;seed<=40;seed++) {
                var before=Market.Create(seed);var after=Market.NextDay(before);
                foreach(var offer in Intelligence.Offers(before)) {
                    var record=Record(before,offer);string reaction=ClueContent.MarketReaction(after,record);
                    bool correct=offer.ActualPositive==offer.ClaimedPositive;
                    if(correct)trueReports++;else falseReports++;
                    Check(reaction.StartsWith(correct?"취재 내용 일치":"취재 내용 불일치"),"Factual reliability was confused with price direction.");
                    long close=before.Stocks[offer.Company].Price,open=after.Stocks[offer.Company].Price;
                    if((open>close)!=offer.ActualPositive) {oppositeMoves++;
                        Check(reaction.Contains("반대 방향의 개장 수급"),"Feedback hid an actual event/price disagreement.");}
                }
                var floor=Market.Create(seed);
                foreach(var stock in floor.Stocks){stock.Price=100;stock.History=new long[]{100};}
                var flat=Market.NextDay(floor);
                foreach(var offer in Intelligence.Offers(floor))if(flat.Stocks[offer.Company].Price==100) {flatMoves++;
                    Check(ClueContent.MarketReaction(flat,Record(floor,offer)).Contains("시가는 종가와 같습니다"),"Floor-bound prices were reported as a fall.");}
            }
            Check(trueReports>0&&falseReports>0&&oppositeMoves>0&&flatMoves>0,"Reaction checks lost important outcome coverage.");
        }
    }
}
