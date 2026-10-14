using System;
using System.Linq;

namespace MoneyTown
{
    public static class ExpansionChecks
    {
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Throws(Action action){try{action();}catch(Exception){return;}throw new Exception("Invalid expanded state was accepted.");}
        static GameState Next(GameState state)=>Market.AcknowledgeSettlement(Market.NextDay(state));
        public static int Run()
        {
            CheckCompaniesAndMigration();CheckPurchasedWorld();CheckNews();CheckNewsStorage();return 4;
        }
        static void CheckCompaniesAndMigration()
        {
            var fresh=Market.Create();Check(fresh.Stocks.Length==12&&Market.Companies.Length==12,"Expected twelve playable companies.");
            for(int company=6;company<12;company++) {
                var bought=Market.Trade(fresh,company,2,true);var restored=SaveCodec.Decode(SaveCodec.Encode(bought));
                Check(restored.Stocks[company].Quantity==2&&restored.Trades.Last().Company==company,"New holding or trade identity lost.");
                var sold=Market.Trade(restored,company,2,false);Check(sold.Stocks[company].Quantity==0,"New company sale failed.");
            }
            Throws(()=>Market.Trade(fresh,12,1,true));
            var funded=Market.Create(63);funded.Cash=funded.DayStartAssets=100000000;
            var original=Next(Market.Deposit(Market.Trade(funded,0,2,true)));
            for(int version=1;version<=7;version++) {
                var old=original.Copy();if(version>=4)old=CoreChecks.LegacyPurchase(old,6,version<6?1:2);
                string article=old.Clues.Length==0?null:ClueContent.Body(old.Clues[0].Offer);
                var migrated=SaveCodec.Decode(CoreChecks.LegacyBytes(old,version));
                Check(migrated.EventEdition==2&&migrated.Cash==old.Cash&&migrated.DepositDue==old.DepositDue&&migrated.Realized==old.Realized,"Migration changed existing money or daily world.");
                for(int i=0;i<6;i++)Check(migrated.Stocks[i].Price==old.Stocks[i].Price&&migrated.Stocks[i].Quantity==old.Stocks[i].Quantity&&migrated.Stocks[i].Cost==old.Stocks[i].Cost&&migrated.Stocks[i].History.SequenceEqual(old.Stocks[i].History),"Existing stock changed during migration.");
                for(int i=6;i<12;i++)Check(migrated.Stocks[i].Quantity==0&&migrated.Stocks[i].Cost==0&&migrated.Stocks[i].History.SequenceEqual(new[]{Market.Companies[i].Price})&&migrated.Stocks[i].HistoryDays.SequenceEqual(new[]{old.Day})&&migrated.Stocks[i].HistoryOpenings.Single(),"Migration invented a new company's previous prices or holdings.");
                Check(migrated.DayReports.All(r=>r.ClosePrices.Length==6&&r.News.Length==0),"Migration fabricated old company prices or news.");
                if(article!=null)Check(ClueContent.Body(migrated.Clues[0].Offer)==article,"Paid legacy article was rewritten.");
                var encoded=SaveCodec.Encode(migrated);Check(BitConverter.ToInt32(encoded,4)==9&&encoded.SequenceEqual(SaveCodec.Encode(SaveCodec.Decode(encoded))),"Expanded migration did not round-trip in v9.");
            }
        }
        static void CheckPurchasedWorld()
        {
            var old=Market.Create(42);old.Cash=old.DayStartAssets=100000000;
            old=CoreChecks.LegacyPurchase(old,6,2);
            var migrated=SaveCodec.Decode(CoreChecks.LegacyBytes(old,7));
            var archived=migrated.Clues[0];var expected=archived.Offer;
            var current=Intelligence.CompanyOffers(migrated,expected.Company).First();
            Check(current.Story==expected.Story&&current.ActualPositive==expected.ActualPositive&&current.ClaimedPositive==expected.ClaimedPositive,"Update rerolled an already purchased event.");
            Check(Intelligence.Owned(migrated,migrated.Day,current.Index)==archived,"Old paid company report was not linked on the new shelf.");
            Throws(()=>Intelligence.Buy(migrated,current.Index,migrated.Day));
            migrated=Intelligence.Buy(migrated,current.Index+1,migrated.Day);
            Check(migrated.Clues.Last().Version==3&&migrated.Clues.Last().ContentEdition==3&&migrated.Clues.Last().EventEdition==2,"Mixed-edition new purchase lost its daily world.");
            var copy=SaveCodec.Decode(SaveCodec.Encode(migrated));
            Check(copy.Clues.Last().Offer.Story==expected.Story,"Reload changed a migrated-day new purchase.");
            var advanced=Next(copy);var article=advanced.LastReport.News.Single(n=>n.Company==expected.Company);
            string fact=expected.ActualPositive?expected.Content.ActualPositive:expected.Content.ActualNegative;
            Check(article.Body.Contains(fact.TrimEnd('.',' '))&&advanced.EventEdition==3,"Settlement ignored the old paid event or failed to start the new world.");
            Check(advanced.DayReports.Last().ClosePrices.Length==12&&advanced.DayReports.Last().News.Length==12,"New day omitted newly listed companies.");
        }
        static void CheckNews()
        {
            int positiveFall=0,negativeRise=0;
            for(int seed=1;seed<=100;seed++) {
                var before=Market.Intraday(Market.Create(seed));var after=Market.NextDay(before);var news=after.LastReport.News;
                Check(news.Length==12&&news.Select(n=>n.Company).Distinct().Count()==12,"Opening news omitted a company.");
                Check(news.Select(n=>Math.Abs((double)n.OpeningPrice/n.PreviousPrice-1)).SequenceEqual(news.Select(n=>Math.Abs((double)n.OpeningPrice/n.PreviousPrice-1)).OrderByDescending(x=>x)),"Major news was not sorted by actual move size.");
                foreach(var item in news) {
                    var offer=Intelligence.EventOffer(before,item.Company);string fact=offer.ActualPositive?offer.Content.ActualPositive:offer.Content.ActualNegative;
                    Check(item.PreviousPrice==before.Stocks[item.Company].Price&&item.OpeningPrice==after.Stocks[item.Company].Price,"News quoted a different market price.");
                    Check(item.Body.Contains(fact.TrimEnd('.',' '))&&item.EventImpact==Intelligence.EventEffect(before,item.Company),"News reported a claim instead of the actual event.");
                    long expected=Math.Max(100,(long)Math.Floor(item.PreviousPrice*(1+item.EventImpact+item.MarketImpact)+.5));
                    Check(expected==item.OpeningPrice,"News contributions do not explain the recorded price.");
                    bool up=item.OpeningPrice>item.PreviousPrice;
                    Check(item.Headline.Contains(up?"상승":"하락"),"News headline conflicts with actual direction.");
                    if(offer.ActualPositive&&!up){positiveFall++;Check(item.Body.Contains("긍정적인 소식")&&item.Body.Contains("매도세"),"Positive event fall omitted the offsetting market pressure.");}
                    if(!offer.ActualPositive&&up){negativeRise++;Check(item.Body.Contains("부담이 되는 소식에도")&&item.Body.Contains("매수세"),"Negative event rise omitted the offsetting market pressure.");}
                }
            }
            Check(positiveFall>0&&negativeRise>0,"Both event/price disagreement cases need coverage.");
        }
        static void CheckNewsStorage()
        {
            var original=Market.NextDay(Market.Create(52));var bytes=SaveCodec.Encode(original);var copy=original.Copy();
            copy.LastReport.News[0].Body="changed";copy.DayReports[0].News[0].Headline="changed";
            Check(bytes.SequenceEqual(SaveCodec.Encode(original)),"News copy shares mutable records.");
            Check(bytes.SequenceEqual(SaveCodec.Encode(SaveCodec.Decode(bytes))),"Opening news did not survive save/reload.");
            Action<Action<MarketNews>> invalid=change=>{
                var state=original.Copy();change(state.LastReport.News[0]);state.DayReports[0]=state.LastReport.Copy();Throws(()=>SaveCodec.Encode(state));
            };
            invalid(n=>n.Company=12);invalid(n=>n.PreviousPrice++);invalid(n=>n.OpeningPrice++);
            invalid(n=>n.EventImpact=double.NaN);invalid(n=>n.MarketImpact=double.PositiveInfinity);invalid(n=>n.Body="");
            var broken=original.Copy();broken.LastReport.News[1].Company=broken.LastReport.News[0].Company;broken.DayReports[0]=broken.LastReport.Copy();Throws(()=>SaveCodec.Encode(broken));
            broken=original.Copy();broken.EventEdition=4;Throws(()=>SaveCodec.Encode(broken));
            var oversized=Market.Create();for(int day=0;day<30;day++)oversized=Next(oversized);
            foreach(var report in oversized.DayReports)foreach(var news in report.News)news.Body=new string('가',2400);
            oversized.LastReport=oversized.DayReports.Last().Copy();
            Throws(()=>SaveCodec.Encode(oversized));
        }
    }
}
