using System;
using System.Linq;
using System.Numerics;

namespace MoneyTown
{
    public static class PriceMigration
    {
        public static GameState Apply(GameState previous)
        {
            Market.Validate(previous);
            var state=previous.Copy();
            if(state.PriceMigrationDay>0)return state;
            long total=Market.Total(previous),cashReturned=0;
            BigInteger profit=0;
            for(int i=0;i<state.Stocks.Length;i++) {
                var stock=state.Stocks[i];long oldPrice=stock.Price,newPrice=Market.Companies[i].Price;
                long value=checked(oldPrice*stock.Quantity);
                long quantity=value/newPrice,remainder=value%newPrice;
                long disposedCost=value==0?0:(long)((BigInteger)stock.Cost*remainder/value);
                stock.Quantity=quantity;stock.Cost-=disposedCost;
                stock.History=stock.History.Select(price=>Scale(price,newPrice,oldPrice)).ToArray();
                stock.Price=newPrice;
                cashReturned=checked(cashReturned+remainder);
                profit+=(BigInteger)remainder-disposedCost;
            }
            state.Cash=checked(state.Cash+cashReturned);
            state.Realized=SignedAmount((BigInteger)state.Realized+profit);
            // Fractional-share settlement is a conversion, not today's trading profit.
            state.DayStartRealized=SignedAmount((BigInteger)state.DayStartRealized+profit);
            state.PriceMigrationDay=state.Day;state.PriceMigrationCash=cashReturned;
            state.PriceMigrationAssets=total;state.PriceMigrationProfit=SignedAmount(profit);
            foreach(var clue in state.Clues)clue.BeforePriceMigration=true;
            Market.Validate(state);
            if(Market.Total(state)!=total)throw new InvalidOperationException("가격 전환 전후 총자산이 일치하지 않습니다.");
            return state;
        }
        static long Scale(long value,long numerator,long denominator)
        {
            BigInteger scaled=((BigInteger)value*numerator+denominator/2)/denominator;
            return (long)BigInteger.Min(Market.Limit,BigInteger.Max(100,scaled));
        }
        static long SignedAmount(BigInteger value)
        {
            if(value < -Market.Limit || value > Market.Limit)
                throw new InvalidOperationException("가격 전환의 손익 기록이 금액 한도를 초과했습니다. 원본 저장을 보존합니다.");
            return (long)value;
        }
    }
}
