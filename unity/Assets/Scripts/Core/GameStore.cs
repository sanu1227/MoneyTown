using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace MoneyTown
{
    public static class SaveCodec
    {
        public const int CurrentVersion=11;
        public const int MaximumBytes=2*1024*1024;
        public static byte[] Encode(GameState s)
        {
            Market.Validate(s);
            using (var buffer = new MemoryStream()) {
                using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, true)) {
                    writer.Write(0x4d54554e); writer.Write(CurrentVersion);
                    writer.Write(s.Day); writer.Write(s.Seed); writer.Write(s.Cash); writer.Write(s.Realized); writer.Write(s.DepositDue);
                    writer.Write(s.NewsCompany); writer.Write(s.PositiveNews);
                    writer.Write(s.EventEdition);
                    writer.Write(s.MarketVersion);
                    foreach (var stock in s.Stocks) {
                        writer.Write(stock.Price); writer.Write(stock.Quantity); writer.Write(stock.Cost);
                        writer.Write(stock.History.Length);
                        foreach (long price in stock.History) writer.Write(price);
                    }
                    foreach (long day in s.Reports) writer.Write(day);
                    foreach (var record in s.Information) {
                        writer.Write(record != null);
                        if (record == null) continue;
                        writer.Write((int)record.Kind); writer.Write(record.Company);
                        writer.Write(record.IssuedDay); writer.Write(record.ExpiresDay);
                        writer.Write(record.PositiveNews); writer.Write(record.RumorUp); writer.Write(record.RisingCount);
                        writer.Write(record.Prices.Length);
                        foreach (long price in record.Prices) writer.Write(price);
                    }
                    writer.Write(s.Clues.Length);
                    foreach(var clue in s.Clues) {
                        writer.Write(clue.IssuedDay);writer.Write(clue.Seed);writer.Write(clue.Index);
                        writer.Write(clue.Version);writer.Write(clue.PaidPrice);writer.Write(clue.Prices.Length);
                        foreach(long price in clue.Prices)writer.Write(price);
                        writer.Write(clue.MarketVersion);
                        writer.Write(clue.ContentEdition);
                        writer.Write(clue.EventEdition);
                    }
                    writer.Write(s.TickSeed);writer.Write(s.TickCount);writer.Write(s.DayStartAssets);writer.Write(s.DayStartRealized);
                    writer.Write(s.DayFees);writer.Write(s.DayInformationCost);writer.Write(s.DayInterest);writer.Write(s.SettlementPending);
                    writer.Write(s.LastReport!=null);
                    if(s.LastReport!=null)WriteReport(writer,s.LastReport);
                    foreach(var stock in s.Stocks) {
                        foreach(long day in stock.HistoryDays)writer.Write(day);
                        foreach(bool opening in stock.HistoryOpenings)writer.Write(opening);
                    }
                    writer.Write(s.Trades.Length);
                    foreach(var trade in s.Trades) {
                        writer.Write(trade.Day);writer.Write(trade.Company);writer.Write(trade.Buy);writer.Write(trade.Quantity);
                        writer.Write(trade.Price);writer.Write(trade.Fee);writer.Write(trade.RealizedProfit);
                    }
                    writer.Write(s.DayReports.Length);
                    foreach(var report in s.DayReports)WriteReport(writer,report);
                    writer.Write((int)s.Goals);
                    writer.Write(s.PriceMigrationDay);writer.Write(s.PriceMigrationCash);
                    writer.Write(s.PriceMigrationAssets);writer.Write(s.PriceMigrationProfit);
                    foreach(var clue in s.Clues)writer.Write(clue.BeforePriceMigration);
                }
                var body = buffer.ToArray();
                if(body.Length>MaximumBytes-32)throw new InvalidDataException("저장 파일 크기 한도를 초과했습니다.");
                using (var hash = SHA256.Create()) return body.Concat(hash.ComputeHash(body)).ToArray();
            }
        }
        public static GameState Decode(byte[] data)
        {
            if (data == null || data.Length < 40 || data.Length > MaximumBytes) throw new InvalidDataException("저장 파일 크기를 확인하세요.");
            var body = data.Take(data.Length - 32).ToArray();
            using (var hash = SHA256.Create())
                if (!hash.ComputeHash(body).SequenceEqual(data.Skip(body.Length))) throw new InvalidDataException("저장 파일 검증에 실패했습니다.");
            using (var buffer = new MemoryStream(body))
            using (var reader = new BinaryReader(buffer)) {
                if (reader.ReadInt32() != 0x4d54554e) throw new InvalidDataException("지원하지 않는 저장 파일입니다.");
                int version = reader.ReadInt32();
                if (version < 1 || version > CurrentVersion) throw new InvalidDataException("지원하지 않는 저장 버전입니다.");
                var s = new GameState { Day = reader.ReadInt64(), Seed = reader.ReadInt64(), Cash = reader.ReadInt64(), Realized = reader.ReadInt64(),
                    DepositDue = reader.ReadInt64(), NewsCompany = reader.ReadInt32(), PositiveNews = reader.ReadBoolean(),
                    Stocks = Market.Companies.Select(c=>new Stock{Price=c.Price,History=new[]{c.Price},HistoryDays=new long[]{0},HistoryOpenings=new[]{false}}).ToArray(),
                    Reports = new long[Market.Companies.Length] };
                s.EventEdition=version>=8?reader.ReadInt32():2;
                // Finish the already offered day with its original price model before upgrading.
                s.MarketVersion=version>=10?reader.ReadInt32():3;
                int companies=version>=8?Market.Companies.Length:6;
                for (int i = 0; i < companies; i++) {
                    var stock = new Stock { Price = reader.ReadInt64(), Quantity = reader.ReadInt64(), Cost = reader.ReadInt64() };
                    int count = reader.ReadInt32();
                    if (count < 1 || count > 30) throw new InvalidDataException("가격 기록이 손상되었습니다.");
                    stock.History = new long[count];
                    for (int j = 0; j < count; j++) stock.History[j] = reader.ReadInt64();
                    s.Stocks[i] = stock;
                }
                for (int i = 0; i < companies; i++) s.Reports[i] = reader.ReadInt64();
                if (version >= 2) {
                    for (int i = 0; i < s.Information.Length; i++) {
                        if (!reader.ReadBoolean()) continue;
                        var record = new InfoRecord {
                            Kind=(InfoKind)reader.ReadInt32(), Company=reader.ReadInt32(),
                            IssuedDay=reader.ReadInt64(), ExpiresDay=reader.ReadInt64(),
                            PositiveNews=reader.ReadBoolean(), RumorUp=reader.ReadBoolean(), RisingCount=reader.ReadInt32()
                        };
                        int count = reader.ReadInt32();
                        if (count < 1 || count > 30) throw new InvalidDataException("정보 가격 기록이 손상되었습니다.");
                        record.Prices = new long[count];
                        for (int j = 0; j < count; j++) record.Prices[j] = reader.ReadInt64();
                        s.Information[i] = record;
                    }
                }
                if(version>=3) {
                    int count=reader.ReadInt32();
                    if(count<0 || count>(version>=8?Intelligence.ArchiveLimit:60))throw new InvalidDataException("단서 보관 개수가 손상되었습니다.");
                    s.Clues=new ClueRecord[count];
                    for(int i=0;i<count;i++) {
                        var clue=new ClueRecord{IssuedDay=reader.ReadInt64(),Seed=reader.ReadInt64(),Index=reader.ReadInt32()};
                        if(version>=4) {
                            clue.Version=reader.ReadInt32();clue.PaidPrice=reader.ReadInt64();int prices=reader.ReadInt32();
                            if(prices<0||prices>30)throw new InvalidDataException("단서 시세 기록이 손상되었습니다.");
                            clue.Prices=new long[prices];for(int j=0;j<prices;j++)clue.Prices[j]=reader.ReadInt64();
                        }
                        if(version>=5)clue.MarketVersion=reader.ReadInt32();
                        if(version>=6)clue.ContentEdition=reader.ReadInt32();
                        clue.EventEdition=version>=8?reader.ReadInt32():2;
                        s.Clues[i]=clue;
                    }
                }
                if(version>=5) {
                    s.TickSeed=reader.ReadInt64();s.TickCount=reader.ReadInt64();s.DayStartAssets=reader.ReadInt64();s.DayStartRealized=reader.ReadInt64();
                    s.DayFees=reader.ReadInt64();s.DayInformationCost=reader.ReadInt64();s.DayInterest=reader.ReadInt64();s.SettlementPending=reader.ReadBoolean();
                    if(reader.ReadBoolean())s.LastReport=ReadReport(reader,version);
                } else Market.InitializeDay(s);
                if(version>=7) {
                    foreach(var stock in s.Stocks.Take(companies)) {
                        stock.HistoryDays=new long[stock.History.Length];stock.HistoryOpenings=new bool[stock.History.Length];
                        for(int i=0;i<stock.History.Length;i++)stock.HistoryDays[i]=reader.ReadInt64();
                        for(int i=0;i<stock.History.Length;i++)stock.HistoryOpenings[i]=ReadFlag(reader);
                    }
                    int trades=ReadCount(reader,Market.TradeHistoryLimit);
                    s.Trades=new TradeRecord[trades];
                    for(int i=0;i<trades;i++)s.Trades[i]=new TradeRecord{Day=reader.ReadInt64(),Company=reader.ReadInt32(),Buy=ReadFlag(reader),
                        Quantity=reader.ReadInt64(),Price=reader.ReadInt64(),Fee=reader.ReadInt64(),RealizedProfit=reader.ReadInt64()};
                    int reports=ReadCount(reader,Market.DayHistoryLimit);s.DayReports=new DayReport[reports];
                    for(int i=0;i<reports;i++)s.DayReports[i]=ReadReport(reader,version);
                    s.Goals=(GoalFlags)reader.ReadInt32();
                } else {
                    Market.ImportHistory(s);Market.Validate(s);Progress.Record(s);
                }
                // Newly listed companies start now; never invent their old prices or holdings.
                for(int i=companies;i<s.Stocks.Length;i++) {
                    s.Stocks[i].HistoryDays=new[]{s.Day};s.Stocks[i].HistoryOpenings=new[]{true};
                }
                if(version>=11) {
                    s.PriceMigrationDay=reader.ReadInt64();s.PriceMigrationCash=reader.ReadInt64();
                    s.PriceMigrationAssets=reader.ReadInt64();s.PriceMigrationProfit=reader.ReadInt64();
                    foreach(var clue in s.Clues)clue.BeforePriceMigration=ReadFlag(reader);
                    if(s.PriceMigrationDay==0&&s.Clues.Any(c=>c.BeforePriceMigration))throw new InvalidDataException("가격 전환 기록이 손상되었습니다.");
                }
                if (buffer.Position != buffer.Length) throw new InvalidDataException("알 수 없는 저장 데이터가 있습니다.");
                if (version == 1) { Market.Validate(s); InfoMarket.ImportLegacyReports(s); }
                return Market.Validate(s);
            }
        }
        static int ReadCount(BinaryReader reader,int maximum)
        {
            int count=reader.ReadInt32();
            if(count<0||count>maximum)throw new InvalidDataException("이력 개수가 손상되었습니다.");
            return count;
        }
        static bool ReadFlag(BinaryReader reader)
        {
            byte value=reader.ReadByte();
            if(value>1)throw new InvalidDataException("이력 표시가 손상되었습니다.");
            return value==1;
        }
        static void WriteReport(BinaryWriter writer,DayReport r)
        {
            writer.Write(r.Day);writer.Write(r.StartAssets);writer.Write(r.CloseAssets);writer.Write(r.OpeningAssets);
            writer.Write(r.RealizedProfit);writer.Write(r.Fees);writer.Write(r.InformationCost);writer.Write(r.Interest);writer.Write(r.OpeningInterest);writer.Write(r.Ticks);
            writer.Write(r.ClosePrices.Length);
            foreach(long price in r.ClosePrices)writer.Write(price);foreach(long price in r.OpeningPrices)writer.Write(price);
            writer.Write(r.News.Length);
            foreach(var n in r.News) {
                writer.Write(n.Company);writer.Write(n.PreviousPrice);writer.Write(n.OpeningPrice);
                writer.Write(n.Headline);writer.Write(n.Body);writer.Write(n.EventImpact);writer.Write(n.MarketImpact);
            }
            writer.Write(r.WorkIncome);
        }
        static DayReport ReadReport(BinaryReader reader,int version)
        {
            var r=new DayReport{Day=reader.ReadInt64(),StartAssets=reader.ReadInt64(),CloseAssets=reader.ReadInt64(),OpeningAssets=reader.ReadInt64(),
                RealizedProfit=reader.ReadInt64(),Fees=reader.ReadInt64(),InformationCost=reader.ReadInt64(),Interest=reader.ReadInt64(),OpeningInterest=reader.ReadInt64(),Ticks=reader.ReadInt64()};
            int count=version>=8?reader.ReadInt32():6;
            if(count!=6&&count!=Market.Companies.Length)throw new InvalidDataException("결산 종목 수가 손상되었습니다.");
            r.ClosePrices=new long[count];r.OpeningPrices=new long[count];
            for(int i=0;i<r.ClosePrices.Length;i++)r.ClosePrices[i]=reader.ReadInt64();
            for(int i=0;i<r.OpeningPrices.Length;i++)r.OpeningPrices[i]=reader.ReadInt64();
            if(version>=8) {
                int news=ReadCount(reader,count);r.News=new MarketNews[news];
                for(int i=0;i<news;i++)r.News[i]=new MarketNews{Company=reader.ReadInt32(),PreviousPrice=reader.ReadInt64(),OpeningPrice=reader.ReadInt64(),
                    Headline=reader.ReadString(),Body=reader.ReadString(),EventImpact=reader.ReadDouble(),MarketImpact=reader.ReadDouble()};
            }
            if(version>=9)r.WorkIncome=reader.ReadInt64();
            return r;
        }
    }
    public sealed class GameStore
    {
        readonly string path;
        public bool PricesMigrated { get; private set; }
        public GameStore(string directory) { Directory.CreateDirectory(directory); path = Path.Combine(directory, "game-unity-v1.bin"); }
        public GameState Load()
        {
            PricesMigrated=false;
            string source = File.Exists(path) ? path : path + ".backup";
            if (!File.Exists(source)) return Market.Create();
            var state=Read(source,out bool migrated);
            if(migrated) {
                Preserve("price-update");
                Write(state,false);
                PricesMigrated=true;
            }
            return state;
        }
        static GameState Read(string source,out bool migrated)
        {
            if (new FileInfo(source).Length > SaveCodec.MaximumBytes) throw new InvalidDataException("저장 파일이 너무 큽니다.");
            byte[] bytes=File.ReadAllBytes(source);
            var state=SaveCodec.Decode(bytes);
            migrated=BitConverter.ToInt32(bytes,4)<11;
            return migrated?PriceMigration.Apply(state):state;
        }
        public GameState PreviewBackup() => File.Exists(path + ".backup") ? Read(path + ".backup",out _) : null;
        public void Save(GameState state) => Write(state, true);
        void Write(GameState state, bool updateBackup)
        {
            byte[] bytes = SaveCodec.Encode(state);
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) {
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, updateBackup ? path + ".backup" : null);
            else File.Move(temporary, path);
        }
        void Preserve(string operation)
        {
            string suffix = ".before-" + operation + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N");
            foreach (string source in new[] { path, path + ".backup" })
                if (File.Exists(source)) File.Copy(source, source + suffix);
        }
        public GameState RestoreBackup()
        {
            var state = PreviewBackup();
            if (state == null) throw new InvalidDataException("복구할 백업이 없습니다.");
            Preserve("recovery");
            // Do not replace the known-good backup with the damaged primary file.
            Write(state, false); return state;
        }
        public GameState Reset()
        {
            Preserve("reset");
            var state = Market.Create(); Save(state); return state;
        }
    }
}
