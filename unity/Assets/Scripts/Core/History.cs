using System;
using System.Linq;

namespace MoneyTown
{
    [Flags]
    public enum GoalFlags { None=0, FirstTrade=1, FirstProfit=2, AssetsTwoMillion=4, Diversified=8, AssetsTenMillion=16 }

    [Serializable]
    public sealed class TradeRecord
    {
        public long Day,Quantity,Price,Fee,RealizedProfit;
        public int Company;
        public bool Buy;
        public TradeRecord Copy()=>(TradeRecord)MemberwiseClone();
    }

    public static partial class Market
    {
        public const int TradeHistoryLimit=100,DayHistoryLimit=30,PriceHistoryLimit=30;

        static T[] AppendRecent<T>(T[] items,T item,int limit)
            =>items.Concat(new[]{item}).Skip(Math.Max(0,items.Length+1-limit)).ToArray();

        internal static void ImportHistory(GameState s)
        {
            // Older prices have no timestamps: zero means unknown, never an inferred trading day.
            foreach(var stock in s.Stocks) {
                stock.HistoryDays=new long[stock.History.Length];
                stock.HistoryOpenings=new bool[stock.History.Length];
            }
            s.DayReports=s.LastReport==null?Array.Empty<DayReport>():new[]{s.LastReport.Copy()};
        }

        static void ValidateHistory(GameState s)
        {
            const string damaged="거래 또는 시세 이력이 손상되었습니다.";
            Require(s.Trades!=null&&s.Trades.Length<=TradeHistoryLimit&&s.DayReports!=null&&s.DayReports.Length<=DayHistoryLimit,damaged);
            Require(((int)s.Goals&~31)==0,damaged);
            foreach(var stock in s.Stocks) {
                Require(stock.HistoryDays!=null&&stock.HistoryOpenings!=null&&stock.HistoryDays.Length==stock.History.Length
                    &&stock.HistoryOpenings.Length==stock.History.Length,damaged);
                for(int i=0;i<stock.History.Length;i++) {
                    long day=stock.HistoryDays[i];bool opening=stock.HistoryOpenings[i];
                    Require(day>=0&&day<=s.Day&&(!opening||day>0),damaged);
                    if(i>0) {
                        long before=stock.HistoryDays[i-1];
                        Require(day>=before&&(!opening||day>before),damaged);
                        Require(before==0||day==before||day==before+1&&opening,damaged);
                    }
                }
                Require(stock.HistoryDays.Last()==0||stock.HistoryDays.Last()==s.Day,damaged);
            }
            long previousDay=0;
            foreach(var trade in s.Trades) {
                Require(trade!=null&&trade.Day>=1&&trade.Day>=previousDay&&trade.Day<=s.Day
                    &&trade.Company>=0&&trade.Company<Companies.Length&&trade.Quantity>=1&&trade.Quantity<=Limit
                    &&trade.Price>=100&&trade.Price<=Limit&&trade.RealizedProfit>=-Limit&&trade.RealizedProfit<=Limit,damaged);
                long gross=Multiply(trade.Price,trade.Quantity);
                Require(trade.Fee==gross/1000+(gross%1000==0?0:1),damaged);
                if(trade.Buy)Add(gross,trade.Fee);
                Require(trade.Buy?trade.RealizedProfit==0:trade.RealizedProfit<=gross-trade.Fee,damaged);
                previousDay=trade.Day;
            }
            previousDay=0;
            foreach(var report in s.DayReports) {
                ValidateReport(report,s.Day);
                Require(previousDay==0||report.Day==previousDay+1,damaged);
                previousDay=report.Day;
            }
            Require(s.LastReport==null?s.DayReports.Length==0:s.DayReports.Length>0&&SameReport(s.LastReport,s.DayReports.Last()),damaged);
        }

        static bool SameReport(DayReport a,DayReport b)
            =>a.Day==b.Day&&a.StartAssets==b.StartAssets&&a.CloseAssets==b.CloseAssets&&a.OpeningAssets==b.OpeningAssets
                &&a.RealizedProfit==b.RealizedProfit&&a.Fees==b.Fees&&a.InformationCost==b.InformationCost&&a.Interest==b.Interest
                &&a.OpeningInterest==b.OpeningInterest&&a.WorkIncome==b.WorkIncome&&a.Ticks==b.Ticks&&a.ClosePrices.SequenceEqual(b.ClosePrices)&&a.OpeningPrices.SequenceEqual(b.OpeningPrices)
                &&a.News.Length==b.News.Length&&a.News.Zip(b.News,(x,y)=>x.Company==y.Company&&x.PreviousPrice==y.PreviousPrice&&x.OpeningPrice==y.OpeningPrice
                    &&x.Headline==y.Headline&&x.Body==y.Body&&x.EventImpact==y.EventImpact&&x.MarketImpact==y.MarketImpact).All(equal=>equal);
    }
}
