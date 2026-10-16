using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;

namespace MoneyTown
{
    public static class InvestmentGrowthChecks
    {
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Reject(Action action)
        {try{action();}catch(InvalidOperationException){return;}throw new Exception("Invalid market model was accepted.");}
        static string Article(ClueRecord record)=>ClueContent.Headline(record.Offer)+ClueContent.Body(record.Offer)
            +ClueContent.Snapshot(record)+ClueContent.PriceScenario(record);
        static string Money(long value)=>value.ToString("N0",CultureInfo.GetCultureInfo("ko-KR"))+"원";
        static GameState LegacyState()
        {
            var state=Market.Create(122);state.MarketVersion=3;state.Cash=state.DayStartAssets=1000000000;
            state=Market.Trade(state,1,3,true);
            state=Market.AcknowledgeSettlement(Market.WorkWarehouse(state));state.MarketVersion=3;
            var offer=Enumerable.Range(0,Market.Companies.Length).Select(i=>Intelligence.CompanyOffers(state,i).Last())
                .First(o=>o.ClaimedPositive);
            return Intelligence.Buy(state,offer.Index,state.Day);
        }
        // Freeze the old formula independently of production OpeningChange and NextChange.
        static double OldChange(double change)=>(change<0?-1:1)*Math.Max(.015,Math.Abs(Math.Max(-.12,Math.Min(.12,change))));
        static long Price(long close,double change)=>Math.Max(100,(long)Math.Floor(close*(1+change)+.5));
        static long[] ExpectedOpening(GameState state,int model)
        {
            long seed=state.Seed;var prices=new long[state.Stocks.Length];
            for(int i=0;i<prices.Length;i++) {
                seed=(seed*1664525+1013904223)&uint.MaxValue;
                var company=Market.Companies[i];
                double change=OldChange(company.Growth+Intelligence.EventEffect(state,i)
                    +(seed/4294967296.0*2-1)*company.Volatility*.5);
                if(model==4&&change>0)change=Math.Min(.12,change*3);
                prices[i]=Price(state.Stocks[i].Price,change);
            }
            return prices;
        }
        public static int Run()
        {
            CheckLegacyMigration();CheckTransition();CheckCurrentSave();return 3;
        }
        static void CheckLegacyMigration()
        {
            var old=LegacyState();var record=old.Clues.Single();var offer=record.Offer;
            var company=Market.Companies[offer.Company];double center=company.Growth+offer.Content.Impact/10000.0;
            string range=Money(Price(record.Prices.Last(),OldChange(center-company.Volatility*.5)))+"~"
                +Money(Price(record.Prices.Last(),OldChange(center+company.Volatility*.5)));
            Check(record.MarketVersion==3&&ClueContent.PriceScenario(record).StartsWith("고정 기준 개장 시나리오 | "+range),
                "An already paid model-three report acquired the larger opening range.");
            var migrated=SaveCodec.Decode(CoreChecks.LegacyBytes(old,9));
            Check(migrated.MarketVersion==3&&migrated.Clues.Single().MarketVersion==3
                &&Article(migrated.Clues.Single())==Article(record),"Version-nine migration rewrote a paid report.");
            Check(SaveCodec.Encode(old).SequenceEqual(SaveCodec.Encode(migrated))
                &&migrated.LastReport.WorkIncome==50000&&migrated.DayReports.Last().WorkIncome==50000
                &&migrated.Stocks[1].Quantity==3,"Version-nine migration changed assets, wages or history.");
        }
        static void CheckTransition()
        {
            var old=SaveCodec.Decode(CoreChecks.LegacyBytes(LegacyState(),9));byte[] original=SaveCodec.Encode(old);
            string oldArticle=Article(old.Clues.Single());
            var bought=Intelligence.Buy(old,0,old.Day);
            Check(bought.MarketVersion==3&&bought.Clues.Last().MarketVersion==3,
                "A same-day purchase used the new model before the old opening settled.");
            var waited=Market.Intraday(bought);var restored=SaveCodec.Decode(SaveCodec.Encode(waited));
            Check(restored.MarketVersion==3&&restored.Clues.All(c=>c.MarketVersion==3),
                "Intraday movement or reload prematurely upgraded an old day's model.");
            var first=Market.NextDay(restored);
            Check(first.Stocks.Select(s=>s.Price).SequenceEqual(ExpectedOpening(restored,3))&&first.MarketVersion==4,
                "The first migrated opening must honor old prices and upgrade the following day.");
            var ready=Market.AcknowledgeSettlement(first);
            ready=Intelligence.Buy(ready,old.Clues[0].Index,ready.Day);
            Check(ready.Clues.Last().MarketVersion==4,"The new day's purchase did not pin model four.");
            var second=Market.NextDay(SaveCodec.Decode(SaveCodec.Encode(ready)));
            Check(second.MarketVersion==4&&second.Stocks.Select(s=>s.Price).SequenceEqual(ExpectedOpening(ready,4)),
                "The next opening did not use the larger positive returns after migration.");
            Check(Article(second.Clues.First())==oldArticle&&original.SequenceEqual(SaveCodec.Encode(old)),
                "Progression rewrote a purchased article or mutated the original save.");
        }
        static void CheckCurrentSave()
        {
            var state=Market.Create(42);state.Cash=state.DayStartAssets=1000000000;
            state=Intelligence.Buy(state,Intelligence.DailyCount+2,state.Day);
            byte[] bytes=SaveCodec.Encode(state);var restored=SaveCodec.Decode(bytes);
            Check(BitConverter.ToInt32(bytes,4)==11&&restored.MarketVersion==4&&restored.Clues.Single().MarketVersion==4
                &&bytes.SequenceEqual(SaveCodec.Encode(restored)),"Version-eleven save lost the active or purchased price model.");
            foreach(int invalid in new[]{-1,0,1,2,5,int.MaxValue}) {
                var broken=state.Copy();broken.MarketVersion=invalid;Reject(()=>SaveCodec.Encode(broken));
                // Authenticate the corrupt body so decoding must reject the model, not only its checksum.
                var body=bytes.Take(bytes.Length-32).ToArray();
                int offset=8+5*sizeof(long)+sizeof(int)+sizeof(byte)+sizeof(int);
                Array.Copy(BitConverter.GetBytes(invalid),0,body,offset,sizeof(int));
                using(var hash=SHA256.Create()) {
                    byte[] corrupted=body.Concat(hash.ComputeHash(body)).ToArray();Reject(()=>SaveCodec.Decode(corrupted));
                }
            }
        }
    }
}
