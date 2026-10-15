using System;
using System.Linq;

namespace MoneyTown
{
    public static class PriceProgressionChecks
    {
        static readonly long[] InitialPrices={10000,50000,6000000,150000,1000,3000000,1800000,300000,1100000,1400000,5000,4000000};
        static readonly long[] PreviousPrices={10000,16000,24000,12000,8000,20000,18000,14000,11000,15000,9000,22000};
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Reject(Action action)
        {try{action();}catch(InvalidOperationException){return;}throw new Exception("An unaffordable stock order was accepted.");}
        public static int Run()
        {
            CheckPriceBands();CheckFeeBoundaries();CheckExistingPrices();return 3;
        }
        static void CheckPriceBands()
        {
            var state=Market.Create();var prices=state.Stocks.Select(s=>s.Price).ToArray();
            Check(state.Cash==1000000&&prices.SequenceEqual(InitialPrices),"New-game starting money or company price bands changed.");
            Check(prices.Count(p=>p<=10000)==3&&prices.Count(p=>p>10000&&p<1000000)==3
                &&prices.Count(p=>p>=1000000&&p<3000000)==3&&prices.Count(p=>p>=3000000)==3,
                "The twelve companies must occupy four price bands of three companies.");
            var available=Enumerable.Range(0,prices.Length).Where(i=>OrderGuidance.MaximumBuy(state,i)>0).ToArray();
            Check(available.SequenceEqual(new[]{0,1,3,4,7,10}),"Starting cash must cover six companies and leave six as growth targets.");
            foreach(int i in available)Check(Market.Trade(state,i,1,true).Stocks[i].Quantity==1,
                "A company shown as affordable cannot actually be purchased.");
            foreach(int i in Enumerable.Range(0,prices.Length).Except(available))Reject(()=>Market.Trade(state,i,1,true));
        }
        static void CheckFeeBoundaries()
        {
            for(int company=0;company<InitialPrices.Length;company++) {
                var state=Market.Create();long price=InitialPrices[company],fee=(price+999)/1000;
                var quote=Market.GetQuote(state,company,1);
                Check(quote.Gross==price&&quote.Fee==fee&&quote.Buy==price+fee,"One-share cost does not include the exact rounded trading fee.");
                state.Cash=state.DayStartAssets=price+fee-1;byte[] before=SaveCodec.Encode(state);
                Check(OrderGuidance.MaximumBuy(state,company)==0&&OrderGuidance.GetEstimate(state,company,1).BuyError!="",
                    "The one-won-short fee boundary was advertised as affordable.");
                Reject(()=>Market.Trade(state,company,1,true));
                Check(before.SequenceEqual(SaveCodec.Encode(state)),"A rejected high-price order changed money or holdings.");
                state.Cash=state.DayStartAssets=price+fee;
                Check(OrderGuidance.MaximumBuy(state,company)==1&&Market.Trade(state,company,1,true).Cash==0,
                    "Exact cash including the fee cannot buy one share.");
            }
            var wealthy=Market.Create();wealthy.Cash=wealthy.DayStartAssets=6006000;
            var bought=Market.Trade(wealthy,2,1,true);var sold=Market.Trade(bought,2,1,false);
            Check(bought.Cash==0&&bought.Stocks[2].Cost==6006000&&sold.Stocks[2].Quantity==0
                &&sold.Stocks[2].Cost==0&&sold.Cash==5994000&&sold.Realized==-12000,
                "NovaTech's high nominal price must preserve normal buy/sell fees and cost basis.");
        }
        static void CheckExistingPrices()
        {
            var old=Market.Create(42);old.MarketVersion=4;old.Cash=old.DayStartAssets=100000000;old.TickCount=2;
            for(int i=0;i<PreviousPrices.Length;i++) {
                long price=PreviousPrices[i];old.Stocks[i].Price=price;
                old.Stocks[i].History=new[]{price-200,price+100,price};
                old.Stocks[i].HistoryDays=new long[]{1,1,1};old.Stocks[i].HistoryOpenings=new[]{true,false,false};
            }
            for(int i=0;i<PreviousPrices.Length;i++)old=Market.Trade(old,i,i+1,true);
            old=Intelligence.Buy(old,Intelligence.DailyCount+2*3+2,old.Day);
            var report=old.Clues.Single();string article=ClueContent.Body(report.Offer)+ClueContent.Snapshot(report)+ClueContent.PriceScenario(report);
            // Fixed v10 writer and old price constants do not read today's company price defaults.
            byte[] frozen=CoreChecks.LegacyBytes(old,10);var restored=SaveCodec.Decode(frozen);
            Check(BitConverter.ToInt32(frozen,4)==10&&restored.Cash==old.Cash&&Market.Total(restored)==Market.Total(old)
                &&SaveCodec.Encode(restored).SequenceEqual(frozen),"Loading a prior v10 save repriced assets or changed its serialized records.");
            for(int i=0;i<PreviousPrices.Length;i++) {
                var stock=restored.Stocks[i];long gross=PreviousPrices[i]*(i+1),fee=(gross+999)/1000;
                Check(stock.Price==PreviousPrices[i]&&stock.Quantity==i+1&&stock.Cost==gross+fee
                    &&stock.History.SequenceEqual(new[]{PreviousPrices[i]-200,PreviousPrices[i]+100,PreviousPrices[i]})
                    &&stock.HistoryDays.SequenceEqual(new long[]{1,1,1})&&stock.HistoryOpenings.SequenceEqual(new[]{true,false,false})
                    &&restored.Trades[i].Price==PreviousPrices[i],"A saved company's prices, quantity, cost or history changed to new-game defaults.");
            }
            var savedReport=restored.Clues.Single();
            Check(savedReport.Prices.Last()==24000&&article==ClueContent.Body(savedReport.Offer)+ClueContent.Snapshot(savedReport)+ClueContent.PriceScenario(savedReport),
                "The saved NovaTech analysis lost its original 24,000-won snapshot or scenario.");
            var sale=Market.Trade(restored,2,1,false);
            Check(sale.Cash-restored.Cash==23976&&sale.Stocks[2].Quantity==2,
                "An existing holding was sold at the new six-million-won starting price.");
        }
    }
}
