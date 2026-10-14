using System;
using System.Linq;

namespace MoneyTown
{
    [Serializable]
    public sealed class DayReport
    {
        public long Day,StartAssets,CloseAssets,OpeningAssets,RealizedProfit,Fees,InformationCost,Interest,OpeningInterest,Ticks;
        public long WorkIncome;
        public long[] ClosePrices,OpeningPrices;
        public MarketNews[] News=Array.Empty<MarketNews>();
        public long Profit => CloseAssets-StartAssets;
        public long OpeningProfit => OpeningAssets-CloseAssets-OpeningInterest;
        public DayReport Copy()
        {var r=(DayReport)MemberwiseClone();r.ClosePrices=(long[])ClosePrices.Clone();r.OpeningPrices=(long[])OpeningPrices.Clone();r.News=News.Select(n=>n.Copy()).ToArray();return r;}
    }
    // Active-play seconds only; no wall-clock catch-up or burst updates after a pause.
    public sealed class MarketClock
    {
        public const int DefaultInterval=30;
        public static readonly System.Collections.ObjectModel.ReadOnlyCollection<int> Intervals=Array.AsReadOnly(new[]{30,60,180,300,900,1800,3600});
        public int Interval { get; private set; }=DefaultInterval;
        double elapsed;
        public int Remaining => Math.Max(1,(int)Math.Ceiling(Interval-elapsed));
        public string RemainingText => TimeSpan.FromSeconds(Remaining).ToString(Interval>=3600?@"hh\:mm\:ss":@"mm\:ss");
        public static bool Supports(int seconds)=>Intervals.Contains(seconds);
        public static string IntervalText(int seconds)=>seconds>=3600?"1시간":seconds>=60?(seconds/60)+"분":seconds+"초";
        public void SetInterval(int seconds)
        {
            if(!Supports(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
            if(seconds==Interval)return;
            Interval=seconds;Reset();
        }
        public void Reset()=>elapsed=0;
        public bool Advance(double seconds,bool running)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(!running)return false;
            elapsed+=seconds;
            if(elapsed<Interval)return false;
            elapsed=0;return true;
        }
    }
    public static partial class Market
    {
        public const long WarehouseWage=50000;
        public static GameState WorkWarehouse(GameState previous)
        {
            Ready(previous);
            var paid=previous.Copy();paid.Cash=Add(paid.Cash,WarehouseWage);
            var s=NextDay(paid);
            s.LastReport.WorkIncome=WarehouseWage;
            s.DayReports[s.DayReports.Length-1].WorkIncome=WarehouseWage;
            return Validate(s);
        }
        public const double IntradayMaximum=.006,OpeningMinimum=.015;
        internal static double OpeningChange(double change)=>(change<0?-1:1)*Math.Max(OpeningMinimum,Math.Abs(change));
        internal static void InitializeDay(GameState s)
        {
            s.TickSeed=(s.Seed^0xA511E9B3L)&uint.MaxValue;s.TickCount=0;
            s.DayStartAssets=Total(s);s.DayStartRealized=s.Realized;
            s.DayFees=s.DayInformationCost=s.DayInterest=0;s.LastReport=null;s.SettlementPending=false;
        }
        static void Ready(GameState s)=>Require(!s.SettlementPending,"전날 결산을 먼저 확인하세요.");
        public static GameState Intraday(GameState previous)
        {
            Ready(previous);var s=previous.Copy();
            foreach(var stock in s.Stocks) {
                s.TickSeed=(s.TickSeed*1664525+1013904223)&uint.MaxValue;
                double random=s.TickSeed/4294967296.0;
                // Nonzero 0.10–0.60% swings; the daily event and daily RNG stay untouched.
                double change=(random<.5?-1:1)*(.001+Math.Abs(random*2-1)*.005);
                SetPrice(stock,change,s.Day,false);
            }
            s.TickCount=Add(s.TickCount,1);Progress.Record(s);return Validate(s);
        }
        static void SetPrice(Stock stock,double change,long day,bool opening)
        {
            double rounded=Math.Floor(stock.Price*(1+change)+.5);Require(rounded<=Limit,"가격 한도를 초과했습니다.");
            stock.Price=Math.Max(100,(long)rounded);
            stock.History=AppendRecent(stock.History,stock.Price,PriceHistoryLimit);
            stock.HistoryDays=AppendRecent(stock.HistoryDays,day,PriceHistoryLimit);
            stock.HistoryOpenings=AppendRecent(stock.HistoryOpenings,opening,PriceHistoryLimit);
        }
        public static GameState AcknowledgeSettlement(GameState previous)
        {
            Require(previous.SettlementPending&&previous.LastReport!=null,"확인할 새 결산이 없습니다.");
            var s=previous.Copy();s.SettlementPending=false;Progress.Record(s);return Validate(s);
        }
        static void ValidateDay(GameState s)
        {
            const string damaged="장중 시세 또는 결산 기록이 손상되었습니다.";
            Require(s.TickSeed>=0&&s.TickSeed<=uint.MaxValue&&s.TickCount>=0&&s.TickCount<=Limit,damaged);
            Amount(s.DayStartAssets);Amount(s.DayFees);Amount(s.DayInformationCost);Amount(s.DayInterest);
            Require(s.DayStartRealized>=-Limit&&s.DayStartRealized<=Limit,damaged);
            Require(!s.SettlementPending||s.LastReport!=null,damaged);
            var r=s.LastReport;if(r==null)return;
            Require(r.Day==s.Day-1,damaged);ValidateReport(r,s.Day);
        }
        static void ValidateReport(DayReport r,long day)
        {
            const string damaged="결산 기록이 손상되었습니다.";
            Require(r!=null&&r.Day>=1&&r.Day<day&&r.RealizedProfit>=-2*Limit&&r.RealizedProfit<=2*Limit&&r.Ticks>=0&&r.Ticks<=Limit,damaged);
            Amount(r.StartAssets);Amount(r.CloseAssets);Amount(r.OpeningAssets);Amount(r.Fees);Amount(r.InformationCost);Amount(r.Interest);
            // Keep the original 30,000-won wage in historical and pending settlements.
            Require((r.WorkIncome==0||r.WorkIncome==30000||r.WorkIncome==WarehouseWage)&&r.CloseAssets>=r.WorkIncome,damaged);
            Require(r.OpeningInterest==0||r.OpeningInterest==500,damaged);
            Require(r.ClosePrices!=null&&r.OpeningPrices!=null&&(r.ClosePrices.Length==6||r.ClosePrices.Length==Companies.Length)&&r.OpeningPrices.Length==r.ClosePrices.Length,damaged);
            Require(r.ClosePrices.Concat(r.OpeningPrices).All(p=>p>=100&&p<=Limit),damaged);
            Require(r.News!=null&&(r.News.Length==0||r.News.Length==r.ClosePrices.Length),damaged);
            Require(r.News.All(n=>n!=null)&&r.News.Select(n=>n.Company).Distinct().Count()==r.News.Length,damaged);
            foreach(var n in r.News) {
                Require(n.Company>=0&&n.Company<r.ClosePrices.Length&&n.PreviousPrice==r.ClosePrices[n.Company]&&n.OpeningPrice==r.OpeningPrices[n.Company],damaged);
                Require(!string.IsNullOrWhiteSpace(n.Headline)&&n.Headline.Length<=200&&!string.IsNullOrWhiteSpace(n.Body)&&n.Body.Length<=2400,damaged);
                Require(!double.IsNaN(n.EventImpact)&&!double.IsNaN(n.MarketImpact)&&Math.Abs(n.EventImpact)<=.035&&Math.Abs(n.MarketImpact)<=.2,damaged);
                long expected=Math.Max(100,(long)Math.Floor(n.PreviousPrice*(1+n.EventImpact+n.MarketImpact)+.5));
                Require(Math.Abs(expected-n.OpeningPrice)<=1,damaged);
            }
        }
    }
}
