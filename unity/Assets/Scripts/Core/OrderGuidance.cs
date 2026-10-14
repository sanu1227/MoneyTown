using System;
using System.Numerics;

namespace MoneyTown
{
    public struct OrderEstimate
    {
        public Quote Quote;
        public string BuyError, SellError;
    }

    public static class OrderGuidance
    {
        public static OrderEstimate GetEstimate(GameState state, int index, long quantity)
        {
            var quote = Market.GetQuote(state, index, quantity);
            return new OrderEstimate { Quote = quote, BuyError = BuyError(state, index, quantity, quote),
                SellError = SellError(state, index, quantity, quote) };
        }

        public static long MaximumBuy(GameState state, int index)
        {
            if (index < 0 || index >= Market.Companies.Length) throw new InvalidOperationException("종목을 확인하세요.");
            var stock = state.Stocks[index];
            long low = 0, high = Math.Min(state.Cash / stock.Price, Market.Limit - stock.Quantity);
            if (state.SettlementPending) return 0;
            // Quote is monotonic; use the trading fee rounding and limits without a second price formula.
            while (low < high) {
                long middle = low + (high - low + 1) / 2;
                bool canBuy;
                try { canBuy = BuyError(state, index, middle, Market.GetQuote(state, index, middle)).Length == 0; }
                catch (InvalidOperationException) { canBuy = false; }
                catch (OverflowException) { canBuy = false; }
                if (canBuy) low = middle; else high = middle - 1;
            }
            return low;
        }

        static string BuyError(GameState state, int index, long quantity, Quote quote)
        {
            if (state.SettlementPending) return "결산 확인 필요";
            if (state.Cash < quote.Buy) return "현금 부족";
            var stock = state.Stocks[index];
            if (quantity > Market.Limit - stock.Quantity || quote.Buy > Market.Limit - stock.Cost ||
                quote.Fee > Market.Limit - state.DayFees) return "거래 한도 초과";
            return "";
        }

        static string SellError(GameState state, int index, long quantity, Quote quote)
        {
            if (state.SettlementPending) return "결산 확인 필요";
            var stock = state.Stocks[index];
            if (stock.Quantity < quantity) return "보유 수량 부족";
            long cost = (long)((BigInteger)stock.Cost * quantity / stock.Quantity);
            long realized = state.Realized + quote.Sell - cost;
            if (quote.Sell > Market.Limit - state.Cash || quote.Fee > Market.Limit - state.DayFees ||
                realized < -Market.Limit || realized > Market.Limit) return "거래 한도 초과";
            return "";
        }
    }
}
