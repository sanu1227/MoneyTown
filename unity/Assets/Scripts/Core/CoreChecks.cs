using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace MoneyTown
{
    // Unity 메뉴와 .NET 명령에서 같은 검증을 실행합니다.
    public static class CoreChecks
    {
        static void Equal(long expected, long actual) { if (expected != actual) throw new Exception($"Expected {expected}, actual {actual}"); }
        static void Throws(Action action) {
            try { action(); } catch (Exception) { return; }
            throw new Exception("Invalid operation was accepted.");
        }
        static GameState NextDay(GameState s)=>Market.AcknowledgeSettlement(Market.NextDay(s));
        public static int Run()
        {
            var s = Market.Trade(Market.Create(), 0, 3, true);
            Equal(969970, s.Cash);
            s = Market.Trade(s, 0, 1, false); Equal(-20, s.Realized); Equal(20020, s.Stocks[0].Cost);
            s = Market.Trade(s, 0, 2, false); Equal(999940, s.Cash); Equal(-60, s.Realized); Equal(0, s.Stocks[0].Cost);

            s = Market.Create(); byte[] before = SaveCodec.Encode(s);
            foreach (long q in new long[] { 0, -1, 100, long.MaxValue }) Throws(() => Market.Trade(s, 0, q, true));
            Throws(() => Market.Trade(s, 0, 1, false)); Throws(() => Market.Trade(s, -1, 1, true));
            if (!before.SequenceEqual(SaveCodec.Encode(s))) throw new Exception("Rejected trade mutated input.");

            // Every advertised price is charged once; rejected purchases are atomic.
            for(int i=0;i<Intelligence.OfferCount;i++) {
                int index=i;s=Market.Create();s.Cash=1000000000;
                var offer=Intelligence.Offer(s.Day,s.Seed,i);
                var purchased=Intelligence.Buy(s,i,s.Day);Equal(s.Cash-offer.Price,purchased.Cash);
                Equal(offer.Price,purchased.Clues[0].PaidPrice);
                Throws(()=>Intelligence.Buy(purchased,index,purchased.Day));
                s.Cash=offer.Price-1;before=SaveCodec.Encode(s);Throws(()=>Intelligence.Buy(s,index,s.Day));
                if(!before.SequenceEqual(SaveCodec.Encode(s)))throw new Exception("Rejected purchase mutated input.");
            }
            s=Market.Create();Throws(()=>Intelligence.Buy(s,-1,1));Throws(()=>Intelligence.Buy(s,Intelligence.OfferCount,1));Throws(()=>Intelligence.Buy(s,0,0));

            s = Market.Deposit(Market.Create()); Equal(1000000, Market.Total(s)); Equal(900000, s.Cash);
            Throws(() => Market.Deposit(s));
            s = NextDay(NextDay(s)); Equal(900000, s.Cash);
            s = NextDay(s); Equal(1000500, s.Cash); Equal(0, s.DepositDue);
            s = NextDay(s); Equal(1000500, s.Cash);

            s = NextDay(Market.Deposit(Intelligence.Buy(Market.Trade(Market.Create(), 2, 2, true),1,1)));
            var copy = SaveCodec.Decode(SaveCodec.Encode(s));
            if (!SaveCodec.Encode(NextDay(s)).SequenceEqual(SaveCodec.Encode(NextDay(copy)))) throw new Exception("Restored market diverged.");

            byte[] damaged = SaveCodec.Encode(s); damaged[20] ^= 1;
            Throws(() => SaveCodec.Decode(damaged)); Throws(() => SaveCodec.Decode(before.Take(20).ToArray()));
            Throws(() => SaveCodec.Decode(before.Concat(new byte[] { 0 }).ToArray()));
            var invalid = s.Copy(); invalid.Cash = -1; Throws(() => SaveCodec.Encode(invalid));

            s = NextDay(Market.Create()); Equal(2,s.Day);Equal(Market.Companies.Length,s.LastReport.News.Length);
            var a = Market.Create(42); var b = Market.Trade(a, 0, 1, true);
            for (int i = 0; i < 100; i++) { a = NextDay(a); b = NextDay(b); }
            for (int i = 0; i < a.Stocks.Length; i++) {
                Equal(a.Stocks[i].Price, b.Stocks[i].Price); Equal(30, a.Stocks[i].History.Length);
            }

            s = Market.Create(); Throws(() => Market.GetQuote(s, 0, Market.Limit));
            s.Stocks[0].Quantity = 3; s.Stocks[0].Cost = 10001;
            s = Market.Trade(s, 0, 1, false); Equal(6668, s.Stocks[0].Cost);
            s = Market.Trade(s, 0, 2, false); Equal(0, s.Stocks[0].Cost);

            string directory = Path.Combine(Path.GetTempPath(), "moneytown-check-" + Guid.NewGuid().ToString("N"));
            try {
                var store = new GameStore(directory);
                store.Save(Market.Create()); store.Save(Market.Deposit(Market.Create()));
                Equal(900000, store.Load().Cash); Equal(4, store.Load().DepositDue);
                string path = Path.Combine(directory, "game-unity-v1.bin");
                File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
                Throws(() => store.Load()); Equal(3, new FileInfo(path).Length);
                Equal(1000000, store.Reset().Cash);
                Equal(2, Directory.GetFiles(directory, "*.before-reset-*").Length);
            } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            // Frozen v1 and v2 formats preserve old balances, reports, snapshots and dates.
            var legacy=NextDay(NextDay(Market.Create()));
            legacy.Cash-=2000;legacy.Reports[2]=5;legacy=NextDay(legacy);
            var migrated=SaveCodec.Decode(LegacyBytes(legacy,1));
            Equal(legacy.Cash,migrated.Cash);Equal(legacy.Day,migrated.Day);Equal(0,migrated.Clues.Length);
            Equal(5,InfoMarket.Owned(migrated,InfoKind.Report,2).ExpiresDay);
            Equal(3,InfoMarket.Owned(migrated,InfoKind.Report,2).IssuedDay);
            s=Market.Create();
            for(int kind=0;kind<4;kind++) {
                var record=InfoMarket.Capture(s,(InfoKind)kind,0);
                s.Information[InfoMarket.Slot(record.Kind,record.Company)]=record;
                s.Cash-=InfoMarket.Products[kind].Price;
                if(record.Kind==InfoKind.Report)s.Reports[record.Company]=record.ExpiresDay;
            }
            migrated=SaveCodec.Decode(LegacyBytes(s,2));Equal(s.Cash,migrated.Cash);Equal(4,migrated.Information.Count(r=>r!=null));
            copy=SaveCodec.Decode(SaveCodec.Encode(migrated));
            if(!SaveCodec.Encode(migrated).SequenceEqual(SaveCodec.Encode(copy)))throw new Exception("Migrated save did not round-trip.");
            var untouched=copy.Copy();copy.Information[InfoMarket.Slot(InfoKind.Briefing,0)].Prices[0]=12345;
            Equal(10000,untouched.Information[InfoMarket.Slot(InfoKind.Briefing,0)].Prices[0]);
            invalid=migrated.Copy();invalid.Information[InfoMarket.Slot(InfoKind.Briefing,0)].ExpiresDay++;Throws(()=>SaveCodec.Encode(invalid));

            CheckCatalog();
            CheckOffers();
            CheckWorld();
            CheckArchive();
            CheckLegacyClues();
            CheckTutorial();
            CheckIntraday();CheckSettlement();CheckDayMigration();CheckMarketIntervals();
            CheckArticleEditions();
            CheckMediaVoices();
            return 30 + StorageChecks.Run() + PurchaseGuidanceChecks.Run() + OrderGuidanceChecks.Run() + HistoryChecks.Run() + ProgressChecks.Run() + ExpansionChecks.Run() + CorrelationChecks.Run() + WarehouseChecks.Run();
        }
        static void CheckMediaVoices()
        {
            int reports=0;
            foreach(var row in InformationCatalog.All) {
                foreach(bool positive in new[]{false,true}) {
                    var sealedOffer=new IntelOffer(0,row.Company,row.Event,row.Kind,positive,positive,row.Grade,3,3);
                    if(ClueContent.Body(sealedOffer)!=(positive?row.PositiveBody:row.NegativeBody))
                        throw new Exception("Sealed information lost its media voice.");
                    if(row.Grade!=6&&row.Grade!=8&&row.Grade!=10)continue;
                    var targeted=new IntelOffer(Intelligence.DailyCount,row.Company,row.Event,InfoKind.Report,positive,positive,row.Grade,3,3);
                    string body=ClueContent.Body(targeted);
                    if(body!=(positive?row.ReportPositiveBody:row.ReportNegativeBody)||!body.StartsWith("요약 | ")||body.Contains("얘기야"))
                        throw new Exception("Company analysis must use report prose.");
                    if(row.Event<100) {
                        var oldOffer=new IntelOffer(6,row.Company,row.Event,InfoKind.Report,positive,positive,row.Grade,2,1);
                        if(ClueContent.Body(oldOffer)!=(positive?oldOffer.Content.PositiveBody:oldOffer.Content.NegativeBody))
                            throw new Exception("Archived report changed.");
                    }
                    reports++;
                }
            }
            Equal(1800,reports);
            Throws(()=>new CatalogClue(new string[19]));
        }
        static void CheckArticleEditions()
        {
            var state=Market.Create(42);state.Cash=100000000;
            state=Intelligence.Buy(state,Intelligence.DailyCount,state.Day);
            Equal(InformationCatalog.CurrentEdition,state.Clues[0].ContentEdition);
            var old=Market.Create(42);old.Cash=100000000;old=LegacyPurchase(old,6,1);
            string original=ClueContent.Body(old.Clues[0].Offer);
            var migrated=SaveCodec.Decode(LegacyBytes(old,5));
            Equal(1,migrated.Clues[0].ContentEdition);Equal(old.Cash,migrated.Cash);
            if(ClueContent.Body(migrated.Clues[0].Offer)!=original)throw new Exception("V5 paid article changed during migration.");
            Throws(()=>Intelligence.Buy(migrated,Intelligence.DailyCount,migrated.Day));
            migrated=Intelligence.Buy(migrated,Intelligence.DailyCount+1,migrated.Day);
            var restored=SaveCodec.Decode(SaveCodec.Encode(migrated));
            Equal(1,restored.Clues[0].ContentEdition);Equal(3,restored.Clues[1].ContentEdition);
            if(ClueContent.Body(restored.Clues[0].Offer)!=original||ClueContent.Body(restored.Clues[1].Offer)!=ClueContent.Body(migrated.Clues[1].Offer))
                throw new Exception("Mixed article editions failed to round-trip.");
            var invalid=state.Copy();invalid.Clues[0].ContentEdition=4;Throws(()=>SaveCodec.Encode(invalid));
            invalid.Clues[0].ContentEdition=0;Throws(()=>SaveCodec.Encode(invalid));
            for(int index=0;index<24;index++) {
                var a=Intelligence.Offer(state.Day,state.Seed,index,1,2,2);var b=Intelligence.Offer(state.Day,state.Seed,index,2,2,2);
                Equal(a.Company,b.Company);Equal(a.Story,b.Story);Equal(a.Price,b.Price);Equal(a.Content.Impact,b.Content.Impact);
                if(a.ActualPositive!=b.ActualPositive||a.ClaimedPositive!=b.ClaimedPositive)throw new Exception("Editorial update changed event outcomes.");
            }
        }
        static void CheckCatalog()
        {
            Equal(3000,InformationCatalog.All.Count);
            if(InformationCatalog.All.Select(r=>r.Category).Distinct().Count()<35)throw new Exception("Expanded catalog lost topic variety.");
            Equal(10,InformationCatalog.All.Select(r=>r.Source).Distinct().Count());
            long previousMax=0;
            foreach(int grade in Enumerable.Range(1,10)) {
                var rows=InformationCatalog.All.Where(r=>r.Grade==grade).ToArray();
                Equal(300,rows.Length);Equal(300,rows.Select(r=>r.PositiveBody).Distinct().Count());Equal(300,rows.Select(r=>r.NegativeBody).Distinct().Count());
                Equal(12,rows.Select(r=>r.Company).Distinct().Count());
                if(rows.Min(r=>r.Price)<=previousMax)throw new Exception("Grade prices overlap or fail to rise.");
                previousMax=rows.Max(r=>r.Price);
                foreach(var r in rows) {
                    string body=r.PositiveTitle+r.NegativeTitle+r.PositiveBody+r.NegativeBody;
                    if(grade<6&&Market.Companies.Any(c=>body.Contains(c.Name)))throw new Exception("Low grades reveal a company.");
                    if(grade>=6&&!body.Contains(Market.Companies[r.Company].Name))throw new Exception("High grade lacks company specifics.");
                    if(grade>=8&&(!body.Contains("민감도")||!body.Contains("기준 100")))throw new Exception("High grade lacks quantitative conditions.");
                    if(grade==10&&(!body.Contains("반대 시나리오")||!body.Contains("%p")))throw new Exception("Top grade lacks risk and event contribution.");
                    if(System.Text.RegularExpressions.Regex.IsMatch(body,@"(?<![가-힣])매[수도](?:\s|하세요|하라|해야)"))throw new Exception("Information gives trade instructions.");
                }
            }
            Equal(4000,InformationCatalog.All.Min(r=>r.Price));Equal(24000000,previousMax);
            Throws(()=>InformationCatalog.Load("id\ninvalid"));Equal(3000,InformationCatalog.All.Count);
        }
        static void CheckOffers()
        {
            var samples=new int[10];var correct=new int[10];var sources=new System.Collections.Generic.HashSet<string>();
            int companies=0,kinds=0;var events=new System.Collections.Generic.HashSet<int>();
            for(int seed=1;seed<=2000;seed++) {
                var original=Market.Create(seed);byte[] before=SaveCodec.Encode(original);
                var offers=Intelligence.Offers(original);Equal(9,offers.Select(o=>o.Company).Distinct().Count());
                Equal(3,offers.Count(o=>o.Grade<=3));Equal(3,offers.Count(o=>o.Grade>=4&&o.Grade<=6));Equal(3,offers.Count(o=>o.Grade>=7));
                foreach(var offer in offers) {
                    companies|=1<<offer.Company;kinds|=1<<(int)offer.Kind;events.Add(offer.Story);sources.Add(offer.Source);
                    samples[offer.Grade-1]++;if(offer.ActualPositive==offer.ClaimedPositive)correct[offer.Grade-1]++;
                    foreach(var targeted in Intelligence.CompanyOffers(original,offer.Company)) {
                        Equal(offer.Story,targeted.Story);
                        if(offer.ActualPositive!=targeted.ActualPositive)throw new Exception("Channels disagree on actual event.");
                        if(offer.Grade==targeted.Grade&&offer.ClaimedPositive!=targeted.ClaimedPositive)throw new Exception("Channel re-rolls same-grade claim.");
                        Equal(targeted.Content.Price*2,targeted.Price);
                        if(!targeted.Targeted||!ClueContent.Body(targeted).Contains(Market.Companies[targeted.Company].Name))throw new Exception("Targeted analysis lacks its company.");
                    }
                }
                if(!before.SequenceEqual(SaveCodec.Encode(original)))throw new Exception("Reading offers mutated market.");
            }
            Equal((1<<Market.Companies.Length)-1,companies);Equal(15,kinds);Equal(300,events.Count);Equal(10,sources.Count);
            for(int i=0;i<10;i++)if(samples[i]==0||Math.Abs(correct[i]*100.0/samples[i]-Intelligence.Tiers[i].Confidence)>5)
                throw new Exception("Event reliability outside tolerance: "+(i+1));
        }
        static void CheckWorld()
        {
            int truthVsPriceMismatch=0;
            for(int seed=1;seed<=200;seed++) {
                var original=Market.Create(seed);var offers=Intelligence.Offers(original);
                var purchased=Intelligence.Buy(original,0,original.Day);
                var traded=Market.Trade(purchased,0,1,true);var restored=SaveCodec.Decode(SaveCodec.Encode(traded));
                foreach(var variant in new[]{purchased,traded,restored})
                    if(!offers.SequenceEqual(Intelligence.Offers(variant)))throw new Exception("Same-day information rerolled.");
                string body=ClueContent.Body(purchased.Clues[0].Offer);
                var next=NextDay(original);var nextBought=NextDay(purchased);
                Equal(next.Seed,nextBought.Seed);
                var random=original.Copy();
                for(int i=0;i<Market.Companies.Length;i++) {
                    Equal(next.Stocks[i].Price,nextBought.Stocks[i].Price);
                    double change=Market.NextChange(random,i,Intelligence.EventEffect(original,i));
                    Equal(Math.Max(100,(long)Math.Floor(original.Stocks[i].Price*(1+change)+.5)),next.Stocks[i].Price);
                }
                foreach(var offer in offers) {
                    double effect=Intelligence.EventEffect(original,offer.Company);
                    if(effect!=(offer.ActualPositive?1:-1)*offer.Content.Impact/10000.0)throw new Exception("Event and market effect disagree.");
                    if(offer.ActualPositive!=(next.Stocks[offer.Company].Price>original.Stocks[offer.Company].Price))truthVsPriceMismatch++;
                }
                if(body!=ClueContent.Body(nextBought.Clues[0].Offer))throw new Exception("Purchased article changed.");
                bool verified=ClueContent.FollowUp(purchased.Clues[0]).StartsWith("취재 내용과 일치\n");
                if(verified!=(offers[0].ActualPositive==offers[0].ClaimedPositive))throw new Exception("Follow-up disagrees with actual event.");
                Throws(()=>Intelligence.Buy(next,0,original.Day));
            }
            if(truthVsPriceMismatch==0)throw new Exception("Events must not guarantee price direction.");
            var rich=Market.Create();rich.Cash=100000000;
            var saved=Intelligence.Buy(rich,Intelligence.DailyCount+2,rich.Day);var record=saved.Clues[0];
            if(ClueContent.Snapshot(record)==""||ClueContent.PriceScenario(record)=="")throw new Exception("Top company analysis lacks snapshot/scenarios.");
            string snapshot=ClueContent.Snapshot(record)+ClueContent.PriceScenario(record);
            var later=SaveCodec.Decode(SaveCodec.Encode(NextDay(saved)));
            if(snapshot!=ClueContent.Snapshot(later.Clues[0])+ClueContent.PriceScenario(later.Clues[0]))throw new Exception("Historical analysis changed.");
            var copy=saved.Copy();copy.Clues[0].Prices[0]=999;Equal(10000,saved.Clues[0].Prices[0]);
        }
        static void CheckArchive()
        {
            var s=Market.Create();s.Cash=1000000000000;long totalPaid=0;
            for(int day=1;day<=5;day++) {
                for(int i=0;i<Intelligence.OfferCount;i++){totalPaid+=Intelligence.Offer(s.Day,s.Seed,i).Price;s=Intelligence.Buy(s,i,s.Day);}
                if(day<5)s=NextDay(s);
            }
            Equal(Intelligence.ArchiveLimit,s.Clues.Length);Equal(2,s.Clues.Min(r=>r.IssuedDay));Equal(1000000000000-totalPaid,s.Cash);
            for(int i=0;i<Intelligence.OfferCount;i++){int index=i;Throws(()=>Intelligence.Buy(s,index,s.Day));}
            var copy=SaveCodec.Decode(SaveCodec.Encode(s));
            if(!SaveCodec.Encode(s).SequenceEqual(SaveCodec.Encode(copy)))throw new Exception("Archive did not round-trip.");
            var invalid=s.Copy();invalid.Clues.Last().Index=Intelligence.OfferCount;Throws(()=>SaveCodec.Encode(invalid));
            invalid=s.Copy();invalid.Clues.Last().IssuedDay=6;Throws(()=>SaveCodec.Encode(invalid));
            invalid=s.Copy();invalid.Clues.Last().Seed^=1;Throws(()=>SaveCodec.Encode(invalid));
            invalid=s.Copy();invalid.Clues.Last().Version=4;Throws(()=>SaveCodec.Encode(invalid));
            invalid=s.Copy();invalid.Clues.Last().PaidPrice++;Throws(()=>SaveCodec.Encode(invalid));
            invalid=s.Copy();invalid.Clues.Last().Prices=Array.Empty<long>();Throws(()=>SaveCodec.Encode(invalid));
            invalid=s.Copy();invalid.Clues=new[]{invalid.Clues[0],invalid.Clues[0]};Throws(()=>SaveCodec.Encode(invalid));
        }
        static void CheckLegacyClues()
        {
            var old=Market.Create();old.Clues=Enumerable.Range(0,3).Select(i=>new ClueRecord{IssuedDay=old.Day,Seed=old.Seed,Index=i}).ToArray();old.Cash-=3800;
            var migrated=SaveCodec.Decode(LegacyBytes(old,3));Equal(old.Cash,migrated.Cash);Equal(3,migrated.Clues.Length);
            for(int i=0;i<3;i++) {
                var offer=LegacyIntelligence.Offers(old.Day,old.Seed)[i];
                Equal(1,migrated.Clues[i].Version);Equal(LegacyIntelligence.Tiers[i].Price,migrated.Clues[i].Offer.Price);
                if(ClueContent.Body(migrated.Clues[i].Offer)!=LegacyClueContent.Body(offer))throw new Exception("Legacy paid text changed.");
            }
            var combined=Intelligence.Buy(migrated,0,migrated.Day);Equal(4,combined.Clues.Length);
            var restored=SaveCodec.Decode(SaveCodec.Encode(combined));Equal(4,restored.Clues.Length);
            if(Intelligence.Owned(restored,1,0,1)==null||Intelligence.Owned(restored,1,0,3)==null)throw new Exception("Old/new purchase keys collided.");
            var invalid=old.Copy();invalid.Clues[0].Index=3;Throws(()=>SaveCodec.Encode(invalid));
        }
        static void CheckTutorial()
        {
            var real=Market.Trade(Market.Create(42),0,5,true);real=Market.Deposit(real);
            byte[] original=SaveCodec.Encode(real);
            var lesson=TutorialSession.Restore(0);
            Equal(1000000,lesson.State.Cash);Equal(4,lesson.State.Day);Equal(4,lesson.State.Stocks[1].History.Length);
            Equal(1,lesson.Offer.Company);Equal(4,lesson.Offer.Grade);Equal(17,lesson.Offer.Story);
            for(int step=0;step<TutorialSession.Count;step++) {
                Equal(step,lesson.Step);
                var restored=TutorialSession.Restore(step);
                if(!SaveCodec.Encode(lesson.State).SequenceEqual(SaveCodec.Encode(restored.State)))throw new Exception("Resumed practice diverged.");
                var before=SaveCodec.Encode(lesson.State);Throws(()=>lesson.Advance(step+1,1));
                if(step==4) {
                    Throws(()=>lesson.Advance(step,0));Throws(()=>lesson.Advance(step));Equal(step,lesson.Step);
                    if(!before.SequenceEqual(SaveCodec.Encode(lesson.State)))throw new Exception("Wrong answer charged money or advanced.");
                    if(ClueContent.Body(lesson.Clue.Offer).Contains(Market.Companies[1].Name))throw new Exception("Practice question reveals its answer.");
                }
                long cashBefore=lesson.State.Cash,totalBefore=Market.Total(lesson.State);var buy=Market.GetQuote(lesson.State,1,1);
                lesson.Advance(step,1);
                if(step==2){Equal(1,lesson.State.Stocks[1].Quantity);Equal(cashBefore-buy.Buy,lesson.State.Cash);}
                if(step==3){Equal(1,lesson.State.Clues.Length);Equal(cashBefore-lesson.Offer.Price,lesson.State.Cash);}
                if(step==5)Equal(5,lesson.State.Day);
                if(step==6){Equal(0,lesson.State.Stocks[1].Quantity);Equal(cashBefore+buy.Sell,lesson.State.Cash);}
                if(step==7){Equal(cashBefore-100000,lesson.State.Cash);Equal(totalBefore,Market.Total(lesson.State));Equal(8,lesson.State.DepositDue);}
                if(step==8){Equal(0,lesson.State.DepositDue);Equal(cashBefore+100500,lesson.State.Cash);Equal(8,lesson.State.Day);}
            }
            Equal(TutorialSession.Count,lesson.Step);Throws(()=>lesson.Advance(lesson.Step));
            Equal(1000000+lesson.State.Realized-lesson.Offer.Price+500,Market.Total(lesson.State));
            var replay=TutorialSession.Restore(0);Equal(1000000,replay.State.Cash);Equal(0,replay.State.Clues.Length);
            if(!original.SequenceEqual(SaveCodec.Encode(real)))throw new Exception("Practice changed real progress.");
            Throws(()=>TutorialSession.Restore(-1));Throws(()=>TutorialSession.Restore(11));
        }
        static void CheckIntraday()
        {
            var clock=new MarketClock();Equal(30,clock.Remaining);
            if(clock.Advance(29,true))throw new Exception("Early tick.");Equal(1,clock.Remaining);
            if(clock.Advance(3600,false))throw new Exception("Background tick.");Equal(1,clock.Remaining);
            if(!clock.Advance(1,true))throw new Exception("Missing 30-second tick.");Equal(30,clock.Remaining);
            clock.Advance(10,true);clock.Reset();Equal(30,clock.Remaining);
            Throws(()=>clock.Advance(double.NaN,true));Throws(()=>clock.Advance(-1,true));
            for(int seed=1;seed<=200;seed++) {
                var s=Intelligence.Buy(Market.Deposit(Market.Create(seed)),0,1);
                var offers=Intelligence.Offers(s);string article=ClueContent.Body(s.Clues[0].Offer);
                var tick=Market.Intraday(s);
                Equal(s.Day,tick.Day);Equal(s.Seed,tick.Seed);Equal(s.Cash,tick.Cash);Equal(s.DepositDue,tick.DepositDue);Equal(1,tick.TickCount);
                if(!offers.SequenceEqual(Intelligence.Offers(tick))||article!=ClueContent.Body(tick.Clues[0].Offer))throw new Exception("Intraday re-rolled information.");
                if(tick.TickSeed==s.TickSeed)throw new Exception("Tick random state did not advance.");
                var next=Market.NextDay(tick);
                for(int i=0;i<Market.Companies.Length;i++) {
                    double small=Math.Abs(tick.Stocks[i].Price/(double)s.Stocks[i].Price-1);
                    double big=Math.Abs(next.Stocks[i].Price/(double)tick.Stocks[i].Price-1);
                    if(small>.0061||big<.0149||big<=small)throw new Exception("Opening move is not larger than an intraday move.");
                }
                var loaded=SaveCodec.Decode(SaveCodec.Encode(tick));
                if(!SaveCodec.Encode(Market.Intraday(tick)).SequenceEqual(SaveCodec.Encode(Market.Intraday(loaded))))throw new Exception("Tick re-roll after reload.");
            }
            var longRun=Market.Create();for(int i=0;i<100;i++)longRun=Market.Intraday(longRun);
            Equal(1,longRun.Day);Equal(100,longRun.TickCount);Equal(30,longRun.Stocks[0].History.Length);
        }
        static void CheckMarketIntervals()
        {
            int[] intervals={30,60,180,300,900,1800,3600};
            string[] labels={"30초","1분","3분","5분","15분","30분","1시간"};
            string[] times={"00:30","01:00","03:00","05:00","15:00","30:00","01:00:00"};
            if(!intervals.SequenceEqual(MarketClock.Intervals))throw new Exception("Wrong interval choices.");
            for(int i=0;i<intervals.Length;i++) {
                var clock=new MarketClock();clock.SetInterval(intervals[i]);
                Equal(intervals[i],clock.Interval);Equal(intervals[i],clock.Remaining);
                if(clock.RemainingText!=times[i]||MarketClock.IntervalText(intervals[i])!=labels[i])throw new Exception("Unreadable countdown.");
                if(clock.Advance(intervals[i]-1,true))throw new Exception("Early configured tick.");
                Equal(1,clock.Remaining);
                clock.SetInterval(intervals[i]);Equal(1,clock.Remaining);
                if(clock.Advance(7200,false))throw new Exception("Paused configured tick.");
                Equal(1,clock.Remaining);
                if(!clock.Advance(1,true))throw new Exception("Missing configured tick.");
                Equal(intervals[i],clock.Remaining);
                clock.Advance(12,true);clock.Reset();Equal(intervals[i],clock.Remaining);
                int changed=intervals[(i+1)%intervals.Length];clock.Advance(5,true);clock.SetInterval(changed);Equal(changed,clock.Remaining);
                foreach(int invalid in new[]{-1,0,29,31,120,3601,int.MaxValue}) {
                    Throws(()=>clock.SetInterval(invalid));Equal(changed,clock.Interval);Equal(changed,clock.Remaining);
                }
            }
            var hour=new MarketClock();hour.SetInterval(3600);hour.Advance(1,true);
            if(hour.RemainingText!="00:59:59")throw new Exception("Hour boundary formatting.");
            hour.Advance(3598.5,true);Equal(1,hour.Remaining);
            if(hour.Advance(.49,true)||!hour.Advance(.01,true))throw new Exception("Fractional countdown boundary.");
        }
        static void CheckSettlement()
        {
            var s=Market.Create();s=Market.Trade(s,0,3,true);s=Market.Intraday(s);s=Market.Trade(s,0,1,false);
            s=Intelligence.Buy(s,0,s.Day);s=Market.Deposit(s);
            var closed=Market.NextDay(s);var r=closed.LastReport;
            Equal(1,r.Day);Equal(1000000,r.StartAssets);Equal(Market.Total(s),r.CloseAssets);Equal(Market.Total(closed),r.OpeningAssets);
            Equal(s.Realized,r.RealizedProfit);Equal(s.DayFees,r.Fees);Equal(s.DayInformationCost,r.InformationCost);Equal(1,r.Ticks);
            Equal(r.OpeningAssets-r.CloseAssets,r.OpeningProfit);Equal(r.CloseAssets,closed.DayStartAssets);
            Throws(()=>Market.NextDay(closed));Throws(()=>Market.Intraday(closed));Throws(()=>Market.Trade(closed,0,1,true));Throws(()=>Market.Deposit(closed));Throws(()=>Intelligence.Buy(closed,0,closed.Day));
            var restored=SaveCodec.Decode(SaveCodec.Encode(closed));
            if(!restored.SettlementPending)throw new Exception("Pending settlement lost on restart.");
            var copy=restored.Copy();copy.LastReport.ClosePrices[0]=999;Equal(s.Stocks[0].Price,restored.LastReport.ClosePrices[0]);
            s=Market.AcknowledgeSettlement(restored);Throws(()=>Market.AcknowledgeSettlement(s));
            var start=s.Copy();s=Market.NextDay(s);
            Equal(start.LastReport.CloseAssets,s.LastReport.StartAssets);
            Equal(Market.Total(start)-start.LastReport.CloseAssets,s.LastReport.Profit);
            s=Market.AcknowledgeSettlement(s);s=Market.NextDay(s);Equal(500,s.LastReport.OpeningInterest);Equal(500,s.DayInterest);Equal(0,s.DepositDue);
            Equal(s.LastReport.OpeningAssets-s.LastReport.CloseAssets-500,s.LastReport.OpeningProfit);
            s=Market.AcknowledgeSettlement(s);s=Market.NextDay(s);Equal(500,s.LastReport.Interest);Equal(0,s.LastReport.OpeningInterest);
            var invalid=s.Copy();invalid.LastReport.Day++;Throws(()=>SaveCodec.Encode(invalid));
            invalid=s.Copy();invalid.TickSeed=-1;Throws(()=>SaveCodec.Encode(invalid));
            invalid=s.Copy();invalid.LastReport.OpeningPrices=new long[1];Throws(()=>SaveCodec.Encode(invalid));
        }
        static void CheckDayMigration()
        {
            var old=Market.Trade(Market.Create(),0,5,true);old=LegacyPurchase(old,0,1);
            var migrated=SaveCodec.Decode(LegacyBytes(old,4));
            Equal(old.Cash,migrated.Cash);Equal(Market.Total(old),migrated.DayStartAssets);Equal(old.Seed,migrated.Seed);
            Equal(0,migrated.TickCount);Equal(0,migrated.DayFees);Equal(1,migrated.Clues[0].MarketVersion);
            if(migrated.SettlementPending||migrated.LastReport!=null)throw new Exception("Legacy migration invented a settlement.");
            var tick=Market.Intraday(migrated);Equal(old.Clues[0].Seed,tick.Clues[0].Seed);
            var restored=SaveCodec.Decode(SaveCodec.Encode(tick));Equal(tick.TickSeed,restored.TickSeed);Equal(tick.DayStartAssets,restored.DayStartAssets);
        }
        internal static GameState LegacyPurchase(GameState previous,int index,int edition=2)
        {
            var s=previous.Copy();s.EventEdition=2;
            var offer=Intelligence.Offer(s.Day,s.Seed,index,edition,2,2);
            s.Cash-=offer.Price;s.DayInformationCost+=offer.Price;
            s.Clues=s.Clues.Concat(new[]{new ClueRecord{IssuedDay=s.Day,Seed=s.Seed,Index=index,Version=2,
                ContentEdition=edition,EventEdition=2,MarketVersion=2,PaidPrice=offer.Price,Prices=(long[])s.Stocks[offer.Company].History.Clone()}}).ToArray();
            return Market.Validate(s);
        }
        static void WriteLegacyReport(BinaryWriter writer,DayReport r,int version=7)
        {
            writer.Write(r.Day);writer.Write(r.StartAssets);writer.Write(r.CloseAssets);writer.Write(r.OpeningAssets);
            writer.Write(r.RealizedProfit);writer.Write(r.Fees);writer.Write(r.InformationCost);writer.Write(r.Interest);writer.Write(r.OpeningInterest);writer.Write(r.Ticks);
            int count=version>=8?r.ClosePrices.Length:6;
            if(version>=8)writer.Write(count);
            foreach(long price in r.ClosePrices.Take(count))writer.Write(price);foreach(long price in r.OpeningPrices.Take(count))writer.Write(price);
            if(version>=8) {
                writer.Write(r.News.Length);
                foreach(var n in r.News){writer.Write(n.Company);writer.Write(n.PreviousPrice);writer.Write(n.OpeningPrice);
                    writer.Write(n.Headline);writer.Write(n.Body);writer.Write(n.EventImpact);writer.Write(n.MarketImpact);}
            }
        }
        internal static byte[] LegacyBytes(GameState s,int version)
        {
            // Frozen older file layouts, independent of the current save writer.
            if(version<1||version>8||version<8&&s.Clues.Any(c=>c.Version>2||c.ContentEdition>2))throw new ArgumentException("Legacy fixture contains current information.");
            int companies=version>=8?s.Stocks.Length:6;
            using(var buffer=new MemoryStream()) {
                using(var writer=new BinaryWriter(buffer,System.Text.Encoding.UTF8,true)) {
                    writer.Write(0x4d54554e);writer.Write(version);
                    writer.Write(s.Day);writer.Write(s.Seed);writer.Write(s.Cash);writer.Write(s.Realized);writer.Write(s.DepositDue);
                    writer.Write(s.NewsCompany%companies);writer.Write(s.PositiveNews);
                    if(version>=8)writer.Write(s.EventEdition);
                    foreach(var stock in s.Stocks.Take(companies)) {
                        writer.Write(stock.Price);writer.Write(stock.Quantity);writer.Write(stock.Cost);writer.Write(stock.History.Length);
                        foreach(long price in stock.History)writer.Write(price);
                    }
                    foreach(long expiry in s.Reports.Take(companies))writer.Write(expiry);
                    if(version>=2)foreach(var record in s.Information) {
                        writer.Write(record!=null);if(record==null)continue;
                        writer.Write((int)record.Kind);writer.Write(record.Company);writer.Write(record.IssuedDay);writer.Write(record.ExpiresDay);
                        writer.Write(record.PositiveNews);writer.Write(record.RumorUp);writer.Write(record.RisingCount);
                        writer.Write(record.Prices.Length);foreach(long price in record.Prices)writer.Write(price);
                    }
                    if(version>=3) {
                        writer.Write(s.Clues.Length);
                        foreach(var clue in s.Clues){
                            writer.Write(clue.IssuedDay);writer.Write(clue.Seed);writer.Write(clue.Index);
                            if(version>=4){writer.Write(clue.Version);writer.Write(clue.PaidPrice);writer.Write(clue.Prices.Length);foreach(long price in clue.Prices)writer.Write(price);}
                            if(version>=5)writer.Write(clue.MarketVersion);
                            if(version>=6)writer.Write(clue.ContentEdition);
                            if(version>=8)writer.Write(clue.EventEdition);
                        }
                    }
                    if(version>=5) {
                        writer.Write(s.TickSeed);writer.Write(s.TickCount);writer.Write(s.DayStartAssets);writer.Write(s.DayStartRealized);
                        writer.Write(s.DayFees);writer.Write(s.DayInformationCost);writer.Write(s.DayInterest);writer.Write(s.SettlementPending);
                        writer.Write(s.LastReport!=null);
                        if(s.LastReport!=null)WriteLegacyReport(writer,s.LastReport,version);
                    }
                    if(version>=7) {
                        foreach(var stock in s.Stocks.Take(companies)) {
                            foreach(long day in stock.HistoryDays)writer.Write(day);
                            foreach(bool opening in stock.HistoryOpenings)writer.Write(opening);
                        }
                        writer.Write(s.Trades.Length);
                        foreach(var trade in s.Trades) {
                            writer.Write(trade.Day);writer.Write(trade.Company);writer.Write(trade.Buy);writer.Write(trade.Quantity);
                            writer.Write(trade.Price);writer.Write(trade.Fee);writer.Write(trade.RealizedProfit);
                        }
                        writer.Write(s.DayReports.Length);foreach(var report in s.DayReports)WriteLegacyReport(writer,report,version);
                        writer.Write((int)s.Goals);
                    }
                }
                byte[] body=buffer.ToArray();
                using(var hash=SHA256.Create())return body.Concat(hash.ComputeHash(body)).ToArray();
            }
        }
    }
}
