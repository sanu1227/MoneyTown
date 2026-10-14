using System;
using System.Linq;

namespace MoneyTown
{
    public static class WarehouseChecks
    {
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Reject(Action action){try{action();}catch(InvalidOperationException){return;}throw new Exception("Invalid warehouse action accepted.");}
        public static int Run()
        {
            foreach(long cash in new long[]{0,49999,50000,1000000}) {
                var before=Market.Create();before.Cash=cash;before.DayStartAssets=cash;
                byte[] snapshot=SaveCodec.Encode(before);
                var after=Market.WorkWarehouse(before);
                Check(after.Cash==cash+Market.WarehouseWage&&after.Day==before.Day+1&&after.SettlementPending,"Work must pay once and advance one day, at every cash balance.");
                Check(after.LastReport.WorkIncome==50000&&after.LastReport.Profit==50000&&after.LastReport.OpeningProfit==0&&after.Realized==0,"Wages belong to the completed day, not trading or opening profit.");
                Check(snapshot.SequenceEqual(SaveCodec.Encode(before)),"Work mutated its input.");
                Reject(()=>Market.WorkWarehouse(after));Reject(()=>Market.NextDay(after));
                after=Market.AcknowledgeSettlement(after);after=Market.WorkWarehouse(after);
                Check(after.Cash==cash+100000&&after.Day==3,"Repeat work must consume another full day.");
            }
            var held=Market.Deposit(Market.Trade(Market.Create(),0,3,true));
            held=Market.AcknowledgeSettlement(Market.NextDay(held));held=Market.AcknowledgeSettlement(Market.NextDay(held));
            var ordinary=Market.NextDay(held);var worked=Market.WorkWarehouse(held);
            Check(worked.Cash==ordinary.Cash+50000&&worked.DepositDue==0&&worked.LastReport.OpeningInterest==500,"Work must mature deposits and pay wages once.");
            Check(worked.Seed==ordinary.Seed&&worked.Stocks.Select(s=>s.Price).SequenceEqual(ordinary.Stocks.Select(s=>s.Price))&&worked.Stocks[0].Quantity==3,"Work must use normal overnight prices and preserve holdings.");
            Check(worked.LastReport.OpeningProfit==ordinary.LastReport.OpeningProfit&&worked.LastReport.Profit==ordinary.LastReport.Profit+50000,"Wages must not distort stock returns.");
            var restored=SaveCodec.Decode(SaveCodec.Encode(worked));
            Check(restored.SettlementPending&&restored.Cash==worked.Cash&&restored.LastReport.WorkIncome==50000&&restored.DayReports.Last().WorkIncome==50000,"Pending wage settlement must survive restart.");
            restored=Market.AcknowledgeSettlement(restored);restored=Market.NextDay(restored);
            Check(restored.LastReport.WorkIncome==0&&restored.LastReport.Profit==worked.LastReport.OpeningProfit+500,"The following day must not count wages again.");
            var old=Market.NextDay(Intelligence.Buy(Market.Create(),0,1));
            var migrated=SaveCodec.Decode(CoreChecks.LegacyBytes(old,8));
            Check(migrated.Cash==old.Cash&&migrated.Clues.Length==old.Clues.Length&&migrated.SettlementPending&&migrated.LastReport.WorkIncome==0&&migrated.DayReports.All(r=>r.WorkIncome==0),"Version 8 migration must retain money and purchases without inventing wages.");
            Check(SaveCodec.Encode(migrated).SequenceEqual(SaveCodec.Encode(old)),"Version 8 migration changed existing state.");
            var corrupt=worked.Copy();corrupt.LastReport.WorkIncome=1;Reject(()=>Market.Validate(corrupt));
            corrupt=worked.Copy();corrupt.DayReports.Last().WorkIncome=0;Reject(()=>Market.Validate(corrupt));
            var capped=Market.Create();capped.Cash=Market.Limit;byte[] capBefore=SaveCodec.Encode(capped);
            Reject(()=>Market.WorkWarehouse(capped));Check(capBefore.SequenceEqual(SaveCodec.Encode(capped)),"Rejected pay must not mutate capped assets.");
            var originalWage=Market.WorkWarehouse(Market.Create());
            originalWage.Cash-=20000;originalWage.DayStartAssets-=20000;
            originalWage.LastReport.WorkIncome=30000;originalWage.LastReport.CloseAssets-=20000;originalWage.LastReport.OpeningAssets-=20000;
            originalWage.DayReports[originalWage.DayReports.Length-1]=originalWage.LastReport.Copy();
            var previousRelease=SaveCodec.Decode(SaveCodec.Encode(originalWage));
            Check(previousRelease.Cash==1030000&&previousRelease.SettlementPending&&previousRelease.LastReport.WorkIncome==30000,
                "The old 30,000-won pending wage must load without retroactive payment.");
            previousRelease=Market.WorkWarehouse(Market.AcknowledgeSettlement(previousRelease));
            var history=SaveCodec.Decode(SaveCodec.Encode(previousRelease));
            Check(history.Cash==1080000&&history.DayReports[0].WorkIncome==30000&&history.LastReport.WorkIncome==50000,
                "New shifts pay 50,000 won while historical 30,000-won reports remain intact.");
            return 6;
        }
    }
}
