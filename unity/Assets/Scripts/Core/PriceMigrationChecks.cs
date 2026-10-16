using System;
using System.IO;
using System.Linq;
using System.Numerics;

namespace MoneyTown
{
    public static class PriceMigrationChecks
    {
        static readonly long[] OldPrices={10000,16000,24000,12000,8000,20000,18000,14000,11000,15000,9000,22000};
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void Reject(Action action)
        {try{action();}catch(Exception e)when(e is InvalidOperationException||e is IOException||e is UnauthorizedAccessException){return;}throw new Exception("Unsafe price conversion was accepted.");}
        static GameState OldState()
        {
            var state=Market.Create(42);state.Cash=state.DayStartAssets=100000000;
            for(int i=0;i<OldPrices.Length;i++){state.Stocks[i].Price=OldPrices[i];state.Stocks[i].History=new[]{OldPrices[i]};}
            return state;
        }
        static void Price(Stock stock,long price)
        {stock.History=new[]{stock.Price,price};stock.Price=price;stock.HistoryDays=new long[]{1,1};stock.HistoryOpenings=new[]{true,false};}
        static GameState HeldState()
        {
            var state=Market.Trade(OldState(),2,151,true);state=Market.Trade(state,4,2,true);
            Price(state.Stocks[2],40000);
            return Intelligence.Buy(state,Intelligence.DailyCount+2*3+2,state.Day);
        }
        static string Article(ClueRecord clue)=>ClueContent.Body(clue.Offer)+ClueContent.Snapshot(clue)+ClueContent.PriceScenario(clue);
        static void WithStore(Action<GameStore,string> action)
        {
            string directory=Path.Combine(Path.GetTempPath(),"moneytown-price-check-"+Guid.NewGuid().ToString("N"));
            try{action(new GameStore(directory),Path.Combine(directory,"game-unity-v1.bin"));}
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        public static int Run()
        {CheckConversion();CheckVersionsAndReplay();CheckLimits();CheckStorage();return 4;}
        static void CheckConversion()
        {
            var old=HeldState();byte[] original=SaveCodec.Encode(old);string article=Article(old.Clues.Single());
            var changed=PriceMigration.Apply(old);
            Check(changed.Stocks[2].Price==6000000&&changed.Stocks[2].Quantity==1&&changed.Stocks[2].Cost==3603600
                &&changed.Stocks[4].Price==1000&&changed.Stocks[4].Quantity==16&&changed.Stocks[4].Cost==16016,
                "Price conversion changed value, integer shares or proportional purchase cost.");
            Check(changed.Cash==old.Cash+40000&&changed.Realized==old.Realized+15976
                &&changed.DayStartRealized==old.DayStartRealized+15976&&Market.Total(changed)==Market.Total(old),
                "Fractional settlement changed total assets or today's realized profit.");
            Check(changed.PriceMigrationDay==old.Day&&changed.PriceMigrationCash==40000
                &&changed.PriceMigrationAssets==Market.Total(old)&&changed.PriceMigrationProfit==15976,
                "The migration summary does not explain the exact conversion.");
            Check(changed.Stocks[2].History.SequenceEqual(new long[]{3600000,6000000})
                &&changed.Clues.Single().BeforePriceMigration&&Article(changed.Clues.Single())==article,
                "The live chart was not rebased or an already paid article was rewritten.");
            Check(changed.Day==old.Day&&changed.Seed==old.Seed&&changed.TickSeed==old.TickSeed&&changed.TickCount==old.TickCount
                &&changed.DayFees==old.DayFees&&changed.Goals==old.Goals&&changed.DayStartAssets==old.DayStartAssets
                &&original.SequenceEqual(SaveCodec.Encode(old)),"Conversion advanced the market or mutated its original state.");
            // Clearing only the documented conversion fields must recover all untouched archived records.
            var normalized=changed.Copy();normalized.PriceMigrationDay=normalized.PriceMigrationCash=normalized.PriceMigrationAssets=normalized.PriceMigrationProfit=0;
            normalized.Cash=old.Cash;normalized.Realized=old.Realized;normalized.DayStartRealized=old.DayStartRealized;
            normalized.Stocks=old.Stocks.Select(s=>s.Copy()).ToArray();foreach(var clue in normalized.Clues)clue.BeforePriceMigration=false;
            Check(original.SequenceEqual(SaveCodec.Encode(normalized)),"Conversion rewrote transactions or other historical data.");
            var pending=Market.WorkWarehouse(old);var settled=PriceMigration.Apply(pending);
            Check(settled.SettlementPending&&settled.LastReport.WorkIncome==50000
                &&settled.LastReport.ClosePrices.SequenceEqual(pending.LastReport.ClosePrices)
                &&settled.LastReport.OpeningPrices.SequenceEqual(pending.LastReport.OpeningPrices)
                &&settled.DayReports.Last().News.Select(n=>n.Body).SequenceEqual(pending.DayReports.Last().News.Select(n=>n.Body)),
                "A pending settlement or its historical news was rewritten during conversion.");
        }
        static void CheckVersionsAndReplay()
        {
            for(int version=1;version<=10;version++) {
                byte[] bytes=CoreChecks.LegacyBytes(OldState(),version);var decoded=SaveCodec.Decode(bytes);
                WithStore((store,path)=>{
                    File.WriteAllBytes(path,bytes);var converted=store.Load();
                    Check(store.PricesMigrated&&Market.Total(converted)==Market.Total(decoded)
                        &&converted.Stocks.Select(s=>s.Price).SequenceEqual(Market.Companies.Select(c=>c.Price)),"An older save did not convert safely.");
                    byte[] saved=File.ReadAllBytes(path);Check(BitConverter.ToInt32(saved,4)==11,"Conversion was not committed as version eleven.");
                    Check(SaveCodec.Encode(store.Load()).SequenceEqual(saved)&&!store.PricesMigrated
                        &&SaveCodec.Encode(PriceMigration.Apply(converted)).SequenceEqual(saved),"A repeated load or apply converted values twice.");
                });
            }
            // A v0.20 v10 save already uses the new bases; file version cannot identify its origin.
            var current=Market.Create();current=Market.Trade(current,4,4,true);var raw=CoreChecks.LegacyBytes(current,10);
            var result=PriceMigration.Apply(SaveCodec.Decode(raw));
            Check(result.Cash==current.Cash&&result.Stocks[4].Quantity==4&&result.Stocks[4].Cost==current.Stocks[4].Cost
                &&result.PriceMigrationCash==0&&result.PriceMigrationProfit==0,"An already rebased v10 save was multiplied by old/new base ratios.");
        }
        static void CheckLimits()
        {
            var state=Market.Create();state.Cash=0;var stock=state.Stocks[2];
            stock.Price=100;stock.History=new long[]{100};stock.Quantity=Market.Limit/100;stock.Cost=Market.Limit;
            long value=stock.Price*stock.Quantity,remainder=value%6000000;
            var changed=PriceMigration.Apply(state);
            Check(Market.Total(changed)==value&&changed.Stocks[2].Cost==stock.Cost-(long)((BigInteger)stock.Cost*remainder/value),
                "Large value/cost multiplication overflowed during conversion.");
            var bounds=OldState();Price(bounds.Stocks[2],100);bounds.Stocks[2].History[0]=Market.Limit;
            Price(bounds.Stocks[4],Market.Limit);bounds.Stocks[4].History[0]=100;
            var scaled=PriceMigration.Apply(bounds);
            Check(scaled.Stocks[2].History[0]==Market.Limit&&scaled.Stocks[4].History[0]==100,
                "Rebased chart points escaped the supported price interval.");
            var fractional=Market.Trade(OldState(),2,1,true);var cashed=PriceMigration.Apply(fractional);
            Check(cashed.Stocks[2].Quantity==0&&cashed.Stocks[2].Cost==0&&cashed.Cash==fractional.Cash+24000
                &&cashed.Realized==fractional.Realized-24,"A holding below one new share lost its cash remainder or kept orphaned cost.");
            foreach(bool daily in new[]{false,true}) {
                var capped=HeldState();if(daily)capped.DayStartRealized=Market.Limit;else capped.Realized=Market.Limit;
                byte[] before=SaveCodec.Encode(capped);Reject(()=>PriceMigration.Apply(capped));
                Check(before.SequenceEqual(SaveCodec.Encode(capped)),"A rejected profit overflow changed the source state.");
            }
            var invalidFlag=HeldState();invalidFlag.Clues.Single().BeforePriceMigration=true;
            Reject(()=>SaveCodec.Encode(invalidFlag));
            var later=Market.AcknowledgeSettlement(Market.NextDay(PriceMigration.Apply(HeldState())));
            later=Intelligence.Buy(later,0,later.Day);later.Clues.Last().BeforePriceMigration=true;
            Reject(()=>SaveCodec.Encode(later));
        }
        static void CheckStorage()
        {
            byte[] old=CoreChecks.LegacyBytes(HeldState(),10),damaged={1,2,3};
            WithStore((store,path)=>{
                File.WriteAllBytes(path,old);File.WriteAllBytes(path+".backup",old);store.Load();
                Check(File.ReadAllBytes(path+".backup").SequenceEqual(old),"Loading replaced the original backup.");
                var copies=Directory.GetFiles(Path.GetDirectoryName(path),"*.before-price-update-*");
                Check(copies.Length==2&&copies.All(p=>File.ReadAllBytes(p).SequenceEqual(old)),"Original primary and backup were not preserved separately.");
                store.Load();Check(Directory.GetFiles(Path.GetDirectoryName(path),"*.before-price-update-*").Length==2,
                    "Normal reload repeated migration backups.");
            });
            WithStore((store,path)=>{
                File.WriteAllBytes(path,damaged);File.WriteAllBytes(path+".backup",old);
                var preview=store.PreviewBackup();
                Check(preview.PriceMigrationDay>0&&File.ReadAllBytes(path).SequenceEqual(damaged)
                    &&File.ReadAllBytes(path+".backup").SequenceEqual(old)&&Directory.GetFiles(Path.GetDirectoryName(path)).Length==2,
                    "Migration preview wrote or replaced files.");
                var restored=store.RestoreBackup();
                Check(File.ReadAllBytes(path).SequenceEqual(SaveCodec.Encode(restored))&&File.ReadAllBytes(path+".backup").SequenceEqual(old),
                    "Recovery did not persist the conversion while retaining the old backup.");
            });
            WithStore((store,path)=>{
                File.WriteAllBytes(path,old);File.WriteAllBytes(path+".backup",old);Directory.CreateDirectory(path+".tmp");
                Reject(()=>store.Load());
                Check(!store.PricesMigrated&&File.ReadAllBytes(path).SequenceEqual(old)&&File.ReadAllBytes(path+".backup").SequenceEqual(old),
                    "A failed migration write replaced old files or reported success.");
            });
            WithStore((store,path)=>{
                File.WriteAllBytes(path+".backup",old);var loaded=store.Load();
                Check(store.PricesMigrated&&File.ReadAllBytes(path).SequenceEqual(SaveCodec.Encode(loaded))
                    &&File.ReadAllBytes(path+".backup").SequenceEqual(old),"Backup-only loading failed to commit while preserving its source.");
            });
            WithStore((store,path)=>{
                var capped=HeldState();capped.Realized=Market.Limit;byte[] bytes=CoreChecks.LegacyBytes(capped,10);
                File.WriteAllBytes(path,bytes);Reject(()=>store.Load());
                Check(File.ReadAllBytes(path).SequenceEqual(bytes)&&Directory.GetFiles(Path.GetDirectoryName(path)).Length==1,
                    "An invalid conversion touched its original save before validation.");
            });
        }
    }
}
