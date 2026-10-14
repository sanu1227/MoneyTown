using System;
using System.Linq;

namespace MoneyTown
{
    public static class OrderGuidanceChecks
    {
        static void Equal(long expected, long actual)
        { if (expected != actual) throw new Exception($"Order guidance: expected {expected}, actual {actual}."); }
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Order guidance: " + message); }
        static void Throws(Action action)
        { try { action(); } catch (InvalidOperationException) { return; } catch (OverflowException) { return; } throw new Exception("Invalid quote was accepted."); }
        static GameState State(long price, long cash)
        { var state = Market.Create(); state.Cash = cash; state.Stocks[0].Price = price; state.Stocks[0].History = new[] { price }; return state; }

        public static int Run()
        {
            var state = State(1001, 0);
            Equal(0, OrderGuidance.MaximumBuy(state, 0));
            var estimate = OrderGuidance.GetEstimate(state, 0, 1);
            Check(estimate.BuyError == "현금 부족" && estimate.SellError == "보유 수량 부족", "Directions need independent errors.");
            state.Cash = 1002; Equal(0, OrderGuidance.MaximumBuy(state, 0));
            state.Cash = 1003; Equal(1, OrderGuidance.MaximumBuy(state, 0));
            Equal(2, OrderGuidance.GetEstimate(state, 0, 1).Quote.Fee);
            Equal(0, Market.Trade(state, 0, 1, true).Cash);

            state = State(500, 1001); Equal(2, OrderGuidance.MaximumBuy(state, 0));
            Equal(1, Market.GetQuote(state, 0, 2).Fee);
            Equal(0, Market.Trade(state, 0, 2, true).Cash);
            state.Cash--; Equal(1, OrderGuidance.MaximumBuy(state, 0));

            foreach (long price in new[] { 100L, 1001L, 10000L, Market.Limit }) {
                state = State(price, Market.Limit); byte[] before = SaveCodec.Encode(state);
                long maximum = OrderGuidance.MaximumBuy(state, 0);
                if (maximum > 0) {
                    Check(OrderGuidance.GetEstimate(state, 0, maximum).BuyError.Length == 0, "Maximum was rejected by guidance.");
                    Market.Trade(state, 0, maximum, true);
                }
                Throws(() => Market.Trade(state, 0, maximum + 1, true));
                Check(before.SequenceEqual(SaveCodec.Encode(state)), "Guidance mutated the market.");
            }
            Equal(0, OrderGuidance.MaximumBuy(State(Market.Limit, Market.Limit), 0));
            state = State(100, 101); state.Stocks[0].Quantity = 1; state.Stocks[0].Cost = Market.Limit - 100;
            Equal(0, OrderGuidance.MaximumBuy(state, 0));
            state.Stocks[0].Cost--; Equal(1, OrderGuidance.MaximumBuy(state, 0));
            Equal(Market.Limit, Market.Trade(state, 0, 1, true).Stocks[0].Cost);
            state = State(100, 101); state.DayFees = Market.Limit;
            Equal(0, OrderGuidance.MaximumBuy(state, 0));
            state.DayFees--; Equal(1, OrderGuidance.MaximumBuy(state, 0));
            Equal(Market.Limit, Market.Trade(state, 0, 1, true).DayFees);

            state = State(1000, 1001); Equal(1, OrderGuidance.MaximumBuy(state, 0));
            state.Stocks[0].Price = 1001; state.Stocks[0].History = new[] { 1001L };
            Equal(0, OrderGuidance.MaximumBuy(state, 0));
            Check(OrderGuidance.GetEstimate(state, 0, 1).BuyError == "현금 부족", "Quote did not refresh after a price change.");

            state = Market.Trade(State(1000, 1001), 0, 1, true);
            estimate = OrderGuidance.GetEstimate(state, 0, 1);
            Check(estimate.BuyError == "현금 부족" && estimate.SellError.Length == 0, "Cash shortage blocked a valid sale.");
            Equal(999, Market.Trade(state, 0, 1, false).Cash);
            Check(OrderGuidance.GetEstimate(state, 0, 2).SellError == "보유 수량 부족", "Overselling needs its own error.");
            state.Stocks[0].Cost = 0; state.Realized = Market.Limit;
            Check(OrderGuidance.GetEstimate(state, 0, 1).SellError == "거래 한도 초과", "Realized profit limit was omitted.");
            Throws(() => Market.Trade(state, 0, 1, false));

            state = State(Market.Limit, 0); state.Stocks[0].Quantity = 1; state.Stocks[0].Cost = 10000;
            Market.Validate(state);
            estimate = OrderGuidance.GetEstimate(state, 0, 1);
            Check(estimate.BuyError.Length > 0 && estimate.SellError.Length == 0, "Buy total limit blocked a valid sale.");
            Equal(estimate.Quote.Sell, Market.Trade(state, 0, 1, false).Cash);
            Throws(() => Market.Trade(state, 0, 1, true));

            state = Market.NextDay(Market.Create());
            Equal(0, OrderGuidance.MaximumBuy(state, 0));
            estimate = OrderGuidance.GetEstimate(state, 0, 1);
            Check(estimate.BuyError == "결산 확인 필요" && estimate.SellError == estimate.BuyError, "Unconfirmed settlement must block both directions.");
            foreach (long invalid in new[] { 0L, -1L, long.MaxValue }) Throws(() => OrderGuidance.GetEstimate(state, 0, invalid));
            Throws(() => OrderGuidance.MaximumBuy(state, -1));
            return 1;
        }
    }
}
