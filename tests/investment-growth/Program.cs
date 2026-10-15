using System.Globalization;
using MoneyTown;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
if (args.Length > 1 || (args.Length == 1 && (!int.TryParse(args[0], out _) || int.Parse(args[0]) < 1 || int.Parse(args[0]) > 10000)))
    throw new ArgumentException("Expected [seed-count:1..10000].");
int seeds = args.Length == 0 ? 128 : int.Parse(args[0]);
using (var current = new StreamReader(typeof(Market).Assembly.GetManifestResourceStream("information-catalog")!))
using (var first = new StreamReader(typeof(Market).Assembly.GetManifestResourceStream("information-catalog-v1")!))
using (var second = new StreamReader(typeof(Market).Assembly.GetManifestResourceStream("information-catalog-v2")!))
    InformationCatalog.Load(current.ReadToEnd(), first.ReadToEnd(), second.ReadToEnd());

var samples = new List<Sample>();
for (int seed = 1; seed <= seeds; seed++) {
    var oldState = Market.Create(seed);
    var newState = oldState.Copy();
    oldState.MarketVersion = 3;
    newState.MarketVersion = 4;
    var oldOpen = Market.NextDay(oldState);
    var newOpen = Market.NextDay(newState);
    Require(oldOpen.MarketVersion == 4 && newOpen.MarketVersion == 4, "The next day must enter model four.");
    SameWorld(oldOpen, newOpen);
    for (int company = 0; company < Market.Companies.Length; company++) {
        long close = oldState.Stocks[company].Price;
        long oldPrice = oldOpen.Stocks[company].Price, newPrice = newOpen.Stocks[company].Price;
        int direction = Math.Sign(oldPrice - close);
        Require(direction == Math.Sign(newPrice - close), "The opening direction changed.");
        if (direction > 0) Require(newPrice > oldPrice, "A rising opening did not reward the position more.");
        else Require(newPrice == oldPrice, "A falling or flat opening changed.");
        Require(newPrice <= (long)Math.Floor(close * 1.12 + .5), "The opening exceeded the twelve-percent cap.");
        var oldTrade = RoundTrip(oldState, company, oldOpen);
        var newTrade = RoundTrip(newState, company, newOpen);
        if (direction > 0) Require(newTrade.Profit > oldTrade.Profit, "After-fee profit did not improve.");
        else Require(newTrade == oldTrade, "A falling or flat round trip changed.");
        samples.Add(new Sample(direction, oldTrade.Profit, newTrade.Profit,
            100.0 * (oldPrice - close) / close, 100.0 * (newPrice - close) / close));
    }
}

Console.WriteLine($"# MoneyTown conditional opening-response diagnostic; seeds=1..{seeds}; companies={Market.Companies.Length}; first day only; starting cash=1000000.");
Console.WriteLine("# Every company is traded unconditionally from the same initial state. Results are grouped AFTER the opening by model-three price direction.");
Console.WriteLine("# This is not a playable foresight strategy, information-reading accuracy, or a long-term growth forecast. No information purchases or intraday ticks.");
Console.WriteLine("# Net profit uses production maximum-buy, buy, next-day, acknowledgement and full-sale operations, including both fees and unspent cash.");
Console.WriteLine("direction,samples,old_mean_net_profit,new_mean_net_profit,old_min_net_profit,new_min_net_profit,old_max_net_profit,new_max_net_profit,old_mean_opening_pct,new_mean_opening_pct");
foreach (int direction in new[] { 1, -1, 0 }) {
    var group = samples.Where(s => s.Direction == direction).ToArray();
    if (group.Length == 0) continue;
    Console.WriteLine(string.Join(",", direction > 0 ? "rising" : direction < 0 ? "falling" : "flat", group.Length,
        F(group.Average(s => s.OldProfit)), F(group.Average(s => s.NewProfit)),
        group.Min(s => s.OldProfit), group.Min(s => s.NewProfit), group.Max(s => s.OldProfit), group.Max(s => s.NewProfit),
        F(group.Average(s => s.OldChange)), F(group.Average(s => s.NewChange))));
}
Require(samples.Count == seeds * Market.Companies.Length, "The sample count changed.");
Console.WriteLine($"# PASS: {samples.Count} paired round trips; opening directions, falling prices, RNG, next-day events and offers preserved; rising after-fee profits improved.");

static (long Profit, long Fees) RoundTrip(GameState initial, int company, GameState expectedOpening) {
    long quantity = OrderGuidance.MaximumBuy(initial, company);
    Require(quantity > 0, "The initial cash cannot buy this company.");
    var bought = Market.Trade(initial, company, quantity, true);
    var opened = Market.NextDay(bought);
    Require(opened.Stocks.Select(s => s.Price).SequenceEqual(expectedOpening.Stocks.Select(s => s.Price)), "Trading changed the price path.");
    SameWorld(opened, expectedOpening);
    var sold = Market.Trade(Market.AcknowledgeSettlement(opened), company, quantity, false);
    Require(sold.Stocks.All(s => s.Quantity == 0) && sold.Trades.Length == 2, "The round trip did not close its position.");
    long fees = sold.Trades.Sum(t => t.Fee), profit = sold.Cash - initial.Cash;
    Require(sold.Realized == profit, "Cash profit and realized profit differ.");
    return (profit, fees);
}
static void SameWorld(GameState a, GameState b) {
    Require(a.Day == b.Day && a.Seed == b.Seed && a.TickSeed == b.TickSeed && a.TickCount == b.TickCount &&
        a.NewsCompany == b.NewsCompany && a.PositiveNews == b.PositiveNews && a.EventEdition == b.EventEdition,
        "The model or trading changed time, randomness or event selection.");
    for (int company = 0; company < Market.Companies.Length; company++) {
        var x = Intelligence.EventOffer(a, company); var y = Intelligence.EventOffer(b, company);
        Require(x.Story == y.Story && x.ActualPositive == y.ActualPositive && x.ClaimedPositive == y.ClaimedPositive,
            "A next-day event changed.");
    }
    var left = Intelligence.Offers(a); var right = Intelligence.Offers(b);
    Require(left.Length == right.Length, "The offer count changed.");
    for (int i = 0; i < left.Length; i++)
        Require(left[i].Company == right[i].Company && left[i].Story == right[i].Story && left[i].Grade == right[i].Grade &&
            left[i].Price == right[i].Price && left[i].Kind == right[i].Kind && left[i].ActualPositive == right[i].ActualPositive &&
            left[i].ClaimedPositive == right[i].ClaimedPositive, "A next-day offer changed.");
}
static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
static string F(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
sealed record Sample(int Direction, long OldProfit, long NewProfit, double OldChange, double NewChange);
