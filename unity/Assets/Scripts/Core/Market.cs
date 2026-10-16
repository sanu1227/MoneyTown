using System;
using System.Linq;
using System.Numerics;

namespace MoneyTown
{
    public sealed class Company
    {
        public readonly string Name, Sector;
        public readonly long Price;
        public readonly double Growth, Volatility;
        public Company(string name, string sector, long price, double growth, double volatility)
        { Name = name; Sector = sector; Price = price; Growth = growth; Volatility = volatility; }
    }
    [Serializable]
    public sealed class Stock
    {
        public long Price, Quantity, Cost;
        public long[] History;
        public long[] HistoryDays;
        public bool[] HistoryOpenings;
        public Stock Copy() => new Stock { Price = Price, Quantity = Quantity, Cost = Cost, History = (long[])History.Clone(),
            HistoryDays = (long[])HistoryDays.Clone(), HistoryOpenings = (bool[])HistoryOpenings.Clone() };
    }
    [Serializable]
    public sealed class GameState
    {
        public long Day, Seed, Cash, Realized, DepositDue;
        public int EventEdition = InformationCatalog.CurrentEdition;
        public int MarketVersion = Market.CurrentMarketVersion;
        public int StoryUnlocked = 1, StorySeen;
        public long TickSeed,TickCount,DayStartAssets,DayStartRealized,DayFees,DayInformationCost,DayInterest;
        public long PriceMigrationDay,PriceMigrationCash,PriceMigrationAssets,PriceMigrationProfit;
        public DayReport LastReport;
        public TradeRecord[] Trades = Array.Empty<TradeRecord>();
        public DayReport[] DayReports = Array.Empty<DayReport>();
        public GoalFlags Goals;
        public bool SettlementPending;
        public int NewsCompany;
        public bool PositiveNews;
        public Stock[] Stocks;
        public long[] Reports;
        public InfoRecord[] Information = new InfoRecord[InfoMarket.SlotCount];
        public ClueRecord[] Clues = Array.Empty<ClueRecord>();
        public GameState Copy()
        {
            var copy = (GameState)MemberwiseClone();
            copy.Stocks = Stocks.Select(s => s.Copy()).ToArray();
            copy.Reports = (long[])Reports.Clone();
            copy.Information = Information.Select(item => item == null ? null : item.Copy()).ToArray();
            copy.LastReport = LastReport?.Copy();
            copy.Trades = Trades.Select(item => item.Copy()).ToArray();
            copy.DayReports = DayReports.Select(item => item.Copy()).ToArray();
            copy.Clues = Clues.Select(item => item.Copy()).ToArray();
            return copy;
        }
    }
    public struct Quote
    {
        public long Gross, Fee;
        public long Buy => Gross + Fee;
        public long Sell => Gross - Fee;
    }
    public static partial class Market
    {
        public const long Limit = 9007199254740991;
        public const int CurrentMarketVersion = 4;
        public static readonly Company[] Companies = {
            new Company("그린푸드", "식품", 10000, .001, .018),
            new Company("픽셀게임즈", "게임", 50000, .002, .045),
            new Company("노바테크", "기술", 6000000, .002, .04),
            new Company("솔라에너지", "에너지", 150000, .001, .03),
            new Company("브릿지물류", "물류", 1000, .001, .02),
            new Company("온유메디컬", "의료", 3000000, .001, .035),
            new Company("한빛모터스", "자동차", 1800000, .0015, .032),
            new Company("두레건설", "건설", 300000, .001, .028),
            new Company("해오름항공", "항공", 1100000, .001, .038),
            new Company("별빛엔터", "콘텐츠", 1400000, .002, .044),
            new Company("모아리테일", "유통", 5000, .001, .024),
            new Company("누리텔레콤", "통신", 4000000, .0008, .017)
        };
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        static long Amount(long value)
        { Require(value >= 0 && value <= Limit, "금액 한도를 초과했습니다."); return value; }
        static long Add(long a, long b) => Amount(checked(a + b));
        static long Multiply(long a, long b) => Amount(checked(a * b));
        static double Random(GameState state)
        { state.Seed = (state.Seed * 1664525 + 1013904223) & 0xffffffffL; return state.Seed / 4294967296.0; }
        static void News(GameState state)
        { state.NewsCompany = (int)(Random(state) * Companies.Length); state.PositiveNews = Random(state) > .5; }
        public static GameState Create(long seed = 20261002)
        {
            var s = new GameState {
                Day = 1, Seed = seed & 0xffffffffL, Cash = 1000000,
                Stocks = Companies.Select(c => new Stock { Price = c.Price, History = new[] { c.Price },
                    HistoryDays = new long[] { 1 }, HistoryOpenings = new[] { true } }).ToArray(),
                Reports = new long[Companies.Length]
            };
            News(s); InitializeDay(s); Progress.Record(s); return s;
        }
        public static long Total(GameState s)
        {
            long total = Add(s.Cash, s.DepositDue > 0 ? 100000 : 0);
            foreach (var stock in s.Stocks) total = Add(total, Multiply(stock.Price, stock.Quantity));
            return total;
        }
        public static GameState Validate(GameState s)
        {
            const string damaged = "저장 데이터가 손상되었습니다.";
            Require(s != null && s.Day >= 1 && s.Day <= Limit - 3 && s.Seed >= 0 && s.Seed <= uint.MaxValue, damaged);
            Amount(s.Cash);
            Require(s.EventEdition>=2&&s.EventEdition<=InformationCatalog.CurrentEdition,damaged);
            Require(s.MarketVersion>=3&&s.MarketVersion<=CurrentMarketVersion,damaged);
            Require(s.Realized >= -Limit && s.Realized <= Limit, damaged);
            Require(s.PriceMigrationDay>=0&&s.PriceMigrationDay<=s.Day,damaged);
            Amount(s.PriceMigrationCash);Amount(s.PriceMigrationAssets);
            Require(s.PriceMigrationCash<=s.PriceMigrationAssets&&s.PriceMigrationProfit>=-Limit&&s.PriceMigrationProfit<=Limit,damaged);
            Require(s.PriceMigrationDay>0||s.PriceMigrationCash==0&&s.PriceMigrationAssets==0&&s.PriceMigrationProfit==0,damaged);
            Require(s.Stocks != null && s.Stocks.Length == Companies.Length && s.Reports != null && s.Reports.Length == Companies.Length, damaged);
            foreach (var stock in s.Stocks) {
                Require(stock != null && stock.Price >= 100 && stock.Price <= Limit && stock.Quantity >= 0 && stock.Quantity <= Limit, damaged);
                Amount(stock.Cost);
                Require(stock.Quantity > 0 || stock.Cost == 0, damaged);
                Require(stock.History != null && stock.History.Length >= 1 && stock.History.Length <= 30 && stock.History.Last() == stock.Price, damaged);
                Require(stock.History.All(p => p >= 100 && p <= Limit), damaged);
            }
            Require(s.Reports.All(day => day >= 0 && day <= s.Day + 2), damaged);
            Require(s.DepositDue == 0 || (s.DepositDue > s.Day && s.DepositDue <= s.Day + 3), damaged);
            Require(s.NewsCompany >= 0 && s.NewsCompany < Companies.Length, damaged);
            InfoMarket.Validate(s);
            Intelligence.Validate(s);
            Require(s.Clues.All(c=>!c.BeforePriceMigration||s.PriceMigrationDay>0&&c.IssuedDay<=s.PriceMigrationDay),damaged);
            ValidateDay(s);ValidateHistory(s);Story.Validate(s);Total(s); return s;
        }
        public static Quote GetQuote(GameState s, int index, long quantity)
        {
            Require(index >= 0 && index < Companies.Length, "종목을 확인하세요.");
            Require(quantity >= 1 && quantity <= Limit, "수량은 1 이상의 정수로 입력하세요.");
            long gross = Multiply(s.Stocks[index].Price, quantity);
            long fee = gross / 1000 + (gross % 1000 == 0 ? 0 : 1);
            // A buy total may exceed the limit while the corresponding sale is valid.
            // Each direction validates its own cash/cost changes in Trade.
            return new Quote { Gross = gross, Fee = fee };
        }
        public static GameState Trade(GameState previous, int index, long quantity, bool buy)
        {
            Ready(previous);var order = GetQuote(previous, index, quantity);
            var s = previous.Copy(); var stock = s.Stocks[index];
            if (buy) {
                Require(s.Cash >= order.Buy, "보유 현금이 부족합니다.");
                s.Cash -= order.Buy;
                stock.Cost = Add(stock.Cost, order.Buy); stock.Quantity = Add(stock.Quantity, quantity);
            } else {
                Require(stock.Quantity >= quantity, "보유 주식이 부족합니다.");
                long cost = (long)((BigInteger)stock.Cost * quantity / stock.Quantity);
                s.Cash = Add(s.Cash, order.Sell); s.Realized = checked(s.Realized + order.Sell - cost);
                stock.Cost -= cost; stock.Quantity -= quantity;
            }
            s.DayFees=Add(s.DayFees,order.Fee);
            s.Trades=AppendRecent(s.Trades,new TradeRecord{Day=s.Day,Company=index,Buy=buy,Quantity=quantity,
                Price=stock.Price,Fee=order.Fee,RealizedProfit=s.Realized-previous.Realized},TradeHistoryLimit);
            Progress.Record(s);return Validate(s);
        }
        internal static double OpeningVolatility(int company, int marketVersion = CurrentMarketVersion)
            => Companies[company].Volatility * (marketVersion >= 3 ? .5 : 1);
        internal static double NextChange(GameState s, int index, double eventEffect)
        {
            var company = Companies[index];
            // The event drives the opening; order flow can still offset a weaker event.
            // Keep one RNG draw per company so purchases and upgrades never re-roll events.
            double change=Math.Max(-.12,Math.Min(.12,company.Growth+eventEffect+(Random(s)*2-1)*OpeningVolatility(index)));
            return OpeningChange(change,s.MarketVersion);
        }
        public static GameState Deposit(GameState previous)
        {
            Ready(previous);Require(previous.DepositDue == 0, "진행 중인 예금이 있습니다.");
            Require(previous.Cash >= 100000, "예금할 현금이 부족합니다.");
            var s = previous.Copy(); s.Cash -= 100000; s.DepositDue = s.Day + 3; Progress.Record(s);return Validate(s);
        }
        public static GameState NextDay(GameState previous)
        {
            Ready(previous);var s=previous.Copy();
            var report=new DayReport{Day=previous.Day,StartAssets=previous.DayStartAssets,CloseAssets=Total(previous),
                RealizedProfit=previous.Realized-previous.DayStartRealized,Fees=previous.DayFees,InformationCost=previous.DayInformationCost,
                Interest=previous.DayInterest,Ticks=previous.TickCount,ClosePrices=previous.Stocks.Select(p=>p.Price).ToArray()};
            report.News=new MarketNews[s.Stocks.Length];
            for(int i=0;i<s.Stocks.Length;i++) {
                var offer=Intelligence.EventOffer(previous,i);
                double effect=(offer.ActualPositive?1:-1)*offer.Content.Impact/10000.0;
                double change=NextChange(s,i,effect);
                SetPrice(s.Stocks[i],change,s.Day+1,true);
                report.News[i]=MarketNews.Create(offer,previous.Stocks[i].Price,s.Stocks[i].Price,effect,change-effect);
            }
            report.News=report.News.OrderByDescending(n=>Math.Abs((double)n.OpeningPrice/n.PreviousPrice-1)).ThenBy(n=>n.Company).ToArray();
            s.Day++;
            s.EventEdition=InformationCatalog.CurrentEdition;
            s.MarketVersion=CurrentMarketVersion;
            if(s.DepositDue>0&&s.Day>=s.DepositDue){s.Cash=Add(s.Cash,100500);s.DepositDue=0;report.OpeningInterest=500;}
            News(s);
            report.OpeningPrices=s.Stocks.Select(p=>p.Price).ToArray();report.OpeningAssets=Total(s);
            s.LastReport=report;s.SettlementPending=true;s.TickCount=0;
            s.DayReports=AppendRecent(s.DayReports,report.Copy(),DayHistoryLimit);
            // This new day's P/L starts at the prior close, so overnight P/L and interest are counted once.
            s.DayStartAssets=report.CloseAssets;s.DayStartRealized=s.Realized;
            s.DayFees=s.DayInformationCost=0;s.DayInterest=report.OpeningInterest;
            Progress.Record(s);return Validate(s);
        }
    }
}
