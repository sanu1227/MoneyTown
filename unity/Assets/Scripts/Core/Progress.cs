using System;
using System.Linq;

namespace MoneyTown
{
    public static class Progress
    {
        public static void Record(GameState state)
        {
            if (state.Trades.Length > 0 || state.Stocks.Any(stock => stock.Quantity > 0) || state.Realized != 0)
                state.Goals |= GoalFlags.FirstTrade;
            if (state.Realized > 0 || state.Trades.Any(trade => !trade.Buy && trade.RealizedProfit > 0))
                state.Goals |= GoalFlags.FirstProfit;
            long assets = Market.Total(state);
            if (assets >= 2000000) state.Goals |= GoalFlags.AssetsTwoMillion;
            if (assets >= 10000000) state.Goals |= GoalFlags.AssetsTenMillion;
            if (state.Stocks.Count(stock => stock.Quantity > 0) >= 3) state.Goals |= GoalFlags.Diversified;
        }

        public static bool FundsExhausted(GameState state)
            => state.DepositDue == 0 && state.Stocks.All(stock => stock.Quantity == 0)
                && state.Cash < 101;

        public static bool WaitingForFunds(GameState state)
            => state.Stocks.All(stock => stock.Quantity == 0)
                && Enumerable.Range(0, Market.Companies.Length).All(index => OrderGuidance.MaximumBuy(state, index) == 0);
    }

    public static class ProgressChecks
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static int Run()
        {
            var state = Market.Create();
            Check(state.Goals == 0, "New game must not complete goals.");
            state = Market.Trade(state, 0, 1, true);
            Check((state.Goals & GoalFlags.FirstTrade) != 0, "First trade goal missing.");
            state.Stocks[0].Price += 1000;
            state.Stocks[0].History[state.Stocks[0].History.Length - 1] = state.Stocks[0].Price;
            state = Market.Trade(state, 0, 1, false);
            Check((state.Goals & GoalFlags.FirstProfit) != 0, "Profitable sale goal missing.");
            var restored = SaveCodec.Decode(SaveCodec.Encode(state));
            Check(restored.Goals == state.Goals, "Goals must persist.");
            state.Cash = 10000000; RecordAndCheck(state, GoalFlags.AssetsTwoMillion | GoalFlags.AssetsTenMillion);
            state.Cash = 1000000; Progress.Record(state);
            Check((state.Goals & GoalFlags.AssetsTenMillion) != 0, "Completed asset goal was lost.");
            for (int i = 0; i < 3; i++) state = Market.Trade(state, i, 1, true);
            RecordAndCheck(state, GoalFlags.Diversified);
            state = Market.Create(); state.Cash = 100;
            Check(Progress.FundsExhausted(state), "No possible order at the minimum price should be detected.");
            state.Cash = 101;
            Check(!Progress.FundsExhausted(state) && Progress.WaitingForFunds(state), "Potential later affordability is not permanent exhaustion.");
            state.Cash = 0; state.DepositDue = state.Day + 1;
            Check(!Progress.FundsExhausted(state), "Pending deposit is not exhaustion.");
            state.DepositDue = 0; state.Stocks[0].Quantity = 1;
            Check(!Progress.FundsExhausted(state), "Sellable holding is not exhaustion.");
            return 4;
        }
        static void RecordAndCheck(GameState state, GoalFlags expected)
        { Progress.Record(state); Check((state.Goals & expected) == expected, "Progress goal missing."); }
    }
}
