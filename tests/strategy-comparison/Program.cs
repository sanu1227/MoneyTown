using System.Globalization;
using System.Text.Json;
using MoneyTown;

// No GameStore calls: simulated money and all game states live only in memory.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
if (args.Contains("--help")) {
    Console.WriteLine("dotnet run --project tests/strategy-comparison/StrategyComparison.csproj -- [days:1..365] [ticks-per-day:0..60] [--details]");
    return;
}
var numbers = args.Where(a => a != "--details").ToArray();
if (numbers.Length > 2) throw new ArgumentException("Expected [days] [ticks-per-day] [--details].");
int days = Number(0, 30, 1, 365), ticks = Number(1, 2, 0, 60);
int Number(int index, int fallback, int minimum, int maximum) {
    if (index >= numbers.Length) return fallback;
    if (!int.TryParse(numbers[index], out int value) || value < minimum || value > maximum)
        throw new ArgumentException($"Argument {index + 1} must be {minimum}..{maximum}.");
    return value;
}
using (var current = new StreamReader(typeof(Market).Assembly.GetManifestResourceStream("information-catalog")!))
using (var previous = new StreamReader(typeof(Market).Assembly.GetManifestResourceStream("information-catalog-v1")!))
using (var second = new StreamReader(typeof(Market).Assembly.GetManifestResourceStream("information-catalog-v2")!))
    InformationCatalog.Load(current.ReadToEnd(), previous.ReadToEnd(),second.ReadToEnd());

Comparison.CheckReader();
var seeds = new long[] { 1, 7, 42, 493, 2026, 12345, 67890, 314159, 20261002, 20261012, 123456789, 4294967295 };
var capitals = new long[] { 1_000_000, 10_000_000, 100_000_000 };
var results = new List<Result>();
foreach (long capital in capitals)
    foreach (long seed in seeds)
        results.AddRange(Comparison.Run(seed, capital, days, ticks));
var replay = Comparison.Run(seeds[0], capitals[0], days, ticks);
Comparison.Require(JsonSerializer.Serialize(results.Take(5)) == JsonSerializer.Serialize(replay), "Deterministic replay changed.");

Console.WriteLine($"# MoneyTown game-balance diagnostic; days={days}; ticks/day={ticks}; seeds={string.Join(';', seeds)}");
Console.WriteLine("# PASS: shared market paths, non-mutating reads/actions, public-text reader checks, deterministic first-scenario replay.");
Console.WriteLine("# Prices, RNG, fees and information costs use the production core. Final assets are marked to market without liquidation.");
Console.WriteLine("# These fixed heuristics are not optimal play, player testing, or a profit guarantee. See README for denominators and limits.");
Console.WriteLine("capital,strategy,runs,mean_final_assets,mean_return_pct,min_return_pct,max_return_pct,mean_info_cost,mean_fees,mean_purchases,mean_buy_blocked_attempts,cash_blocked_days_pct,depleted_days_pct,depleted_runs_pct,mean_info_initial_pct,mean_purchase_cash_pct,unread_purchases_pct,mean_info_unaffordable_days,mean_no_offer_days");
foreach (var group in results.GroupBy(r => (r.Capital, r.Strategy))) {
    int purchases = group.Sum(r => r.Purchases);
    Console.WriteLine(string.Join(",", group.Key.Capital, group.Key.Strategy, group.Count(),
        F(group.Average(r => r.FinalAssets)), F(group.Average(r => r.Return)), F(group.Min(r => r.Return)), F(group.Max(r => r.Return)),
        F(group.Average(r => r.InfoCost)), F(group.Average(r => r.Fees)), F(group.Average(r => r.Purchases)), F(group.Average(r => r.BuyBlocked)),
        F(group.Average(r => r.CashBlockedDays) * 100 / days), F(group.Average(r => r.DepletedDays) * 100 / days),
        F(group.Count(r => r.DepletedDays > 0) * 100.0 / group.Count()), F(group.Average(r => 100.0 * r.InfoCost / r.Capital)),
        F(purchases == 0 ? 0 : group.Sum(r => r.PurchaseCashPercent) / purchases),
        F(purchases == 0 ? 0 : group.Sum(r => r.Unread) * 100.0 / purchases),
        F(group.Average(r => r.InfoUnaffordable)), F(group.Average(r => r.NoOffer))));
}
if (args.Contains("--details")) {
    Console.WriteLine("# Per-seed results (one JSON object per line)");
    foreach (var result in results) Console.WriteLine(JsonSerializer.Serialize(result));
}
static string F(double number) => number.ToString("0.00", CultureInfo.InvariantCulture);

sealed record Result(long Seed, long Capital, string Strategy, long FinalAssets, long InfoCost, long Fees,
    int Purchases, int BuyBlocked, int CashBlockedDays, int DepletedDays, int Unread, int InfoUnaffordable, int NoOffer,
    double PurchaseCashPercent) {
    public double Return => 100.0 * (FinalAssets - Capital) / Capital;
}

static class Comparison {
    static readonly string[] Names = { "cash", "equal_weight_hold", "random_daily", "sealed_grade_4_5", "company_grade_6" };
    static readonly JsonSerializerOptions StateOptions = new() { IncludeFields = true, IgnoreReadOnlyProperties = true };
    readonly record struct Cover(int Index, int Grade, long Price);
    readonly record struct Article(int Grade, string Headline, string Body);

    public static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }
    static string Snapshot(GameState state) => JsonSerializer.Serialize(state, StateOptions);
    static string Path(GameState state) => string.Join("|", state.Day, state.Seed, state.TickSeed, state.TickCount,
        state.NewsCompany, state.PositiveNews, string.Join(";", state.Stocks.Select(s =>
            $"{s.Price}:{string.Join(':', s.History)}:{string.Join(':', s.HistoryDays)}:{string.Join(':', s.HistoryOpenings)}")));
    static T Read<T>(GameState state, Func<T> read) {
        string before = Snapshot(state);
        T result = read();
        Require(before == Snapshot(state), "Read mutated a game state.");
        return result;
    }
    static GameState Change(GameState state, Func<GameState> action, bool pricesMayChange = false) {
        string before = Snapshot(state), path = Path(state);
        GameState next = action();
        Require(before == Snapshot(state), "Action mutated its input state.");
        if (!pricesMayChange) Require(path == Path(next), "A trade, purchase or acknowledgement changed the market path.");
        return next;
    }
    static GameState Start(long seed, long capital) {
        var state = Market.Create(seed);
        state.Cash = state.DayStartAssets = capital;
        return Market.Validate(state);
    }
    static long Maximum(GameState state, int company, long budget) {
        var allocation = state.Copy();
        allocation.Cash = Math.Min(budget, state.Cash);
        return OrderGuidance.MaximumBuy(allocation, company);
    }
    static bool CanBuy(GameState state) => Enumerable.Range(0, Market.Companies.Length)
        .Any(i => OrderGuidance.MaximumBuy(state, i) > 0);

    public static Result[] Run(long seed, long capital, int days, int ticks) {
        var players = Names.Select((name, i) => new Player(name, Start(seed, capital), unchecked((uint)seed ^ 0x61C88647u))).ToArray();
        for (int day = 0; day < days; day++) {
            for (int tick = 0; tick < ticks; tick++) {
                foreach (var player in players) player.State = Change(player.State, () => Market.Intraday(player.State), true);
                SamePath(players);
            }
            for (int strategy = 0; strategy < players.Length; strategy++) {
                var player = players[strategy];
                if (strategy == 1 && day == 0) {
                    for (int company = 0; company < Market.Companies.Length; company++) player.Buy(company, capital / Market.Companies.Length);
                } else if (strategy >= 2) {
                    player.SellAll();
                    if (strategy == 2) {
                        // Separate fixed RNG: random decisions cannot consume market randomness.
                        if ((player.Random() >> 16) % 2 == 0) player.Buy((int)((player.Random() >> 16) % 6), player.State.Cash);
                    } else {
                        Cover[] covers = Read(player.State, () => (strategy == 3 ? Intelligence.Offers(player.State)
                            : Intelligence.CompanyOffers(player.State, day % Market.Companies.Length))
                            .Select(o => new Cover(o.Index, o.Grade, o.Price)).ToArray());
                        var candidates = covers.Where(o => strategy == 3 ? o.Grade is 4 or 5 : o.Grade == 6)
                            .OrderBy(o => o.Price).ThenBy(o => o.Index).ToArray();
                        if (candidates.Length == 0) player.NoOffer++;
                        else if (candidates[0].Price > player.State.Cash) player.InfoUnaffordable++;
                        else {
                            Cover cover = candidates[0];
                            player.PurchaseCashPercent += 100.0 * cover.Price / player.State.Cash;
                            player.State = Change(player.State, () => Intelligence.Buy(player.State, cover.Index, player.State.Day));
                            player.Purchases++;
                            // The strategy receives only paid, displayed text, never Company/Story/actual or claimed flags.
                            Article article = Read(player.State, () => {
                                var owned = Intelligence.Owned(player.State, player.State.Day, cover.Index);
                                Require(owned != null, "Cannot read an unpaid article.");
                                var offer = owned!.Offer;
                                return new Article(cover.Grade, ClueContent.Headline(offer), ClueContent.Body(offer));
                            });
                            var signal = Interpret(article);
                            if (signal.Company < 0 || signal.Direction == 0) player.Unread++;
                            else if (signal.Direction > 0) player.Buy(signal.Company, player.State.Cash);
                        }
                    }
                }
                player.InfoCost += player.State.DayInformationCost;
                player.Fees += player.State.DayFees;
                if (!Read(player.State, () => CanBuy(player.State))) player.CashBlockedDays++;
                bool depleted = IsDepleted(player.State);
                player.State = Change(player.State, () => Market.NextDay(player.State), true);
                player.State = Change(player.State, () => Market.AcknowledgeSettlement(player.State));
                if (depleted || IsDepleted(player.State)) player.DepletedDays++;
            }
            SamePath(players);
        }
        return players.Select(p => new Result(seed, capital, p.Name, Read(p.State, () => Market.Total(p.State)), p.InfoCost, p.Fees,
            p.Purchases, p.BuyBlocked, p.CashBlockedDays, p.DepletedDays, p.Unread, p.InfoUnaffordable, p.NoOffer, p.PurchaseCashPercent)).ToArray();
    }
    static bool IsDepleted(GameState state) => state.DepositDue == 0 && state.Stocks.All(s => s.Quantity == 0) && !Read(state, () => CanBuy(state));
    static void SamePath(Player[] players) {
        string expected = Path(players[0].State);
        foreach (var player in players) Require(Path(player.State) == expected, $"Market path differs for {player.Name} on day {player.State.Day}.");
    }
    static (int Company, int Direction) Interpret(Article article) {
        string text = article.Headline + "\n" + article.Body;
        var companies = Enumerable.Range(0, Market.Companies.Length).Where(i => text.Contains(
            article.Grade >= 6 ? Market.Companies[i].Name : Market.Companies[i].Sector, StringComparison.Ordinal)).ToArray();
        if (companies.Length != 1) return (-1, 0);
        bool supportive = article.Body.Contains("사업 여건 개선이 기대", StringComparison.Ordinal);
        bool adverse = article.Body.Contains("사업 여건에 부담이 예상", StringComparison.Ordinal);
        if (supportive || adverse) return (companies[0], supportive == adverse ? 0 : supportive ? 1 : -1);
        // ponytail: literal opening words only; ambiguous/reversed meanings stay unread. Replace with player evidence, not hidden labels.
        string opening = article.Body.Split("\n\n")[0];
        if (opening.Contains("경쟁", StringComparison.Ordinal)) return (companies[0], 0);
        bool up = new[] { "늘어난", "확대", "개최", "열린", "승인이 난", "통과", "개통", "확보", "정상화", "재개" }.Any(opening.Contains);
        bool down = new[] { "보류", "축소", "취소", "연기", "반려", "폐쇄", "잃", "중단", "이탈" }.Any(opening.Contains);
        return (companies[0], up == down ? 0 : up ? 1 : -1);
    }
    public static void CheckReader() {
        Require(Interpret(new(4, "게임박람회", "내일 게임박람회가 열린다.")) == (1, 1), "Public sector/up cue failed.");
        Require(Interpret(new(4, "게임박람회", "내일 게임박람회가 연기된다.")) == (1, -1), "Public down cue failed.");
        Require(Interpret(new(5, "게임 업종", "사업 여건 개선이 기대되지만 비용 부담은 남는다.")) == (1, 1), "Grade 5 statement failed.");
        Require(Interpret(new(6, "분석", "분석 대상: 그린푸드. 사업 여건에 부담이 예상된다.")) == (0, -1), "Named-company statement failed.");
        Require(Interpret(new(4, "단서", "계약이 재개된다.")) == (-1, 0), "Hidden company inferred without a cue.");
        Require(Interpret(new(4, "게임과 기술", "일정이 재개된다.")) == (-1, 0), "Ambiguous sectors should stay unread.");
        Require(Interpret(new(4, "게임", "경쟁작 출시가 연기된다.")) == (1, 0), "Reversed competition cue should stay unread.");
        Require(Interpret(new(4, "게임", "일부 행사는 취소되고 다른 행사는 재개된다.")) == (1, 0), "Mixed directions should stay unread.");
        Require(Interpret(new(5, "게임", "사업 여건 개선이 기대된다. 사업 여건에 부담이 예상된다.")) == (1, 0), "Conflicting statements should stay unread.");
        var state = Market.Create();
        long one = Market.GetQuote(state, 0, 1).Buy;
        Require(Read(state, () => Maximum(state, 0, one)) == 1 && Read(state, () => Maximum(state, 0, one - 1)) == 0, "Fee-inclusive quantity boundary failed.");
    }
    sealed class Player(string name, GameState state, uint random) {
        public readonly string Name = name;
        public GameState State = state;
        public long InfoCost, Fees;
        public int Purchases, BuyBlocked, CashBlockedDays, DepletedDays, Unread, InfoUnaffordable, NoOffer;
        public double PurchaseCashPercent;
        public uint Random() { random = unchecked(random * 1664525u + 1013904223u); return random; }
        public void Buy(int company, long budget) {
            long quantity = Read(State, () => Maximum(State, company, budget));
            if (quantity == 0) { BuyBlocked++; return; }
            State = Change(State, () => Market.Trade(State, company, quantity, true));
        }
        public void SellAll() {
            for (int company = 0; company < State.Stocks.Length; company++) {
                long quantity = State.Stocks[company].Quantity;
                if (quantity > 0) State = Change(State, () => Market.Trade(State, company, quantity, false));
            }
        }
    }
}
