using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace MoneyTown
{
    public static class HistoryChecks
    {
        static void Check(bool condition,string message) {if(!condition)throw new Exception(message);}
        static void Throws(Action action)
        {
            try{action();}catch(Exception){return;}
            throw new Exception("Invalid history was accepted.");
        }
        static GameState Next(GameState s)=>Market.AcknowledgeSettlement(Market.NextDay(s));
        public static int Run()
        {
            CheckTrades();CheckReportsAndPrices();CheckMigration();CheckInvalidHistory();CheckInvalidBytes();return 5;
        }
        static void CheckTrades()
        {
            var initial=Market.Create();var s=initial;byte[] original=SaveCodec.Encode(initial);
            s=Market.Trade(s,0,3,true);s=Market.Trade(s,0,1,false);s=Market.Trade(s,0,2,false);
            Check(s.Trades.Length==3&&s.Trades.All(t=>t.Day==1&&t.Company==0&&t.Price==10000),"Trade identity changed.");
            Check(s.Trades[0].Buy&&s.Trades[0].Quantity==3&&s.Trades[0].Fee==30&&s.Trades[0].RealizedProfit==0,"Buy history is incorrect.");
            Check(!s.Trades[1].Buy&&s.Trades[1].Quantity==1&&s.Trades[1].Fee==10&&s.Trades[1].RealizedProfit==-20,"Partial sale history is incorrect.");
            Check(s.Trades[2].RealizedProfit==-40&&s.Trades.Sum(t=>t.RealizedProfit)==s.Realized,"Realized profit counted fees twice.");
            Check(original.SequenceEqual(SaveCodec.Encode(initial)),"Trade mutated its input.");
            var round=Market.Create();round.Stocks[0].Quantity=3;round.Stocks[0].Cost=10001;
            round=Market.Trade(round,0,1,false);round=Market.Trade(round,0,2,false);
            Check(round.Trades[0].RealizedProfit==6657&&round.Trades[1].RealizedProfit==13312,"Partial cost rounding changed.");
            byte[] before=SaveCodec.Encode(s);Throws(()=>Market.Trade(s,0,1,false));
            Check(before.SequenceEqual(SaveCodec.Encode(s)),"Rejected trade wrote history.");
            var copy=s.Copy();copy.Trades[0].Fee=0;
            Check(s.Trades[0].Fee==30,"Trade copy shares mutable records.");
            for(int i=0;i<52;i++){s=Market.Trade(s,0,1,true);s=Market.Trade(s,0,1,false);}
            Check(s.Trades.Length==100&&!s.Trades.Last().Buy,"Trade retention limit failed.");
            Check((s.Goals&GoalFlags.FirstTrade)!=0,"First trade achievement was lost.");
            Check(SaveCodec.Encode(s).SequenceEqual(SaveCodec.Encode(SaveCodec.Decode(SaveCodec.Encode(s)))),"Trade history did not round-trip.");
        }
        static void CheckReportsAndPrices()
        {
            var s=Market.Create();s=Market.Trade(s,0,1,true);s=Market.Intraday(s);long close=s.Stocks[0].Price;
            s=Next(s);
            Check(s.DayReports.Length==1&&s.DayReports[0].Day==1&&s.DayReports[0].ClosePrices[0]==close,"Daily close was fabricated or lost.");
            Check(s.Stocks[0].HistoryDays.SequenceEqual(new long[]{1,1,2})&&s.Stocks[0].HistoryOpenings.SequenceEqual(new[]{true,false,true}),"Intraday/opening boundaries failed.");
            var untouched=s.Copy();byte[] before=SaveCodec.Encode(s);
            untouched.DayReports[0].ClosePrices[0]++;untouched.Stocks[0].HistoryDays[0]=0;untouched.Stocks[0].HistoryOpenings[0]=false;
            Check(before.SequenceEqual(SaveCodec.Encode(s)),"Report/price copy shares mutable arrays.");
            for(int i=0;i<100;i++)s=Market.Intraday(s);
            Check(s.Stocks.All(p=>p.History.Length==30&&p.HistoryDays.All(d=>d==2)&&p.HistoryOpenings.All(o=>!o)),"Intraday history limit failed.");
            for(int i=0;i<35;i++)s=Next(s);
            Check(s.DayReports.Length==30&&s.DayReports[0].Day==7&&s.DayReports.Last().Day==36,"Daily report limit failed.");
            Check(s.Stocks.All(p=>p.HistoryDays.First()==8&&p.HistoryDays.Last()==37&&p.HistoryOpenings.All(o=>o)),"Opening history limit failed.");
            byte[] encoded=SaveCodec.Encode(s);
            Check(encoded.Length<SaveCodec.MaximumBytes&&encoded.SequenceEqual(SaveCodec.Encode(SaveCodec.Decode(encoded))),"Retained history exceeded save capacity or changed.");
        }
        static void CheckMigration()
        {
            var original=Next(Market.Intraday(Market.Trade(Market.Create(),0,3,true)));
            for(int version=1;version<=7;version++) {
                var migrated=SaveCodec.Decode(CoreChecks.LegacyBytes(original,version));
                Check(migrated.Cash==original.Cash&&migrated.Seed==original.Seed&&migrated.Day==original.Day,"Legacy progress changed.");
                Check(migrated.Trades.Length==(version==7?original.Trades.Length:0),"Legacy migration changed transaction history.");
                if(version<7)Check(migrated.Stocks.Take(6).All(p=>p.HistoryDays.All(d=>d==0)&&p.HistoryOpenings.All(o=>!o)),"Legacy migration invented historical price dates.");
                else Check(migrated.Stocks.Take(6).SelectMany(p=>p.HistoryDays).SequenceEqual(original.Stocks.Take(6).SelectMany(p=>p.HistoryDays)),"V7 migration changed known price dates.");
                Check(migrated.DayReports.Length==(version>=5?1:0),"Legacy migration invented a daily report.");
                Check((migrated.Goals&GoalFlags.FirstTrade)!=0,"Known legacy holding did not record first trade.");
                var tick=Market.Intraday(migrated);
                Check(tick.Stocks[0].HistoryDays.Last()==tick.Day&&tick.Stocks[0].HistoryDays[0]==(version==7?original.Stocks[0].HistoryDays[0]:0),"New tick did not preserve legacy dates.");
                Check(SaveCodec.Encode(tick).SequenceEqual(SaveCodec.Encode(SaveCodec.Decode(SaveCodec.Encode(tick)))),"Migrated history did not round-trip.");
                var next=Next(tick);
                Check(next.DayReports.Last().ClosePrices[0]==tick.Stocks[0].Price&&next.Stocks[0].HistoryOpenings.Last(),"Migrated close/opening was lost.");
            }
        }
        static void CheckInvalidHistory()
        {
            var valid=Next(Market.Intraday(Market.Trade(Market.Create(),0,1,true)));
            Action<Action<GameState>> invalid=change=>{var s=valid.Copy();change(s);Throws(()=>SaveCodec.Encode(s));};
            invalid(s=>s.Trades=new TradeRecord[101]);invalid(s=>s.DayReports=new DayReport[31]);
            invalid(s=>s.Trades[0].Day=s.Day+1);invalid(s=>s.Trades[0].Company=Market.Companies.Length);
            invalid(s=>s.Trades[0].Quantity=0);invalid(s=>s.Trades[0].Quantity=Market.Limit);
            invalid(s=>s.Trades[0].Fee++);invalid(s=>s.Trades[0].RealizedProfit=1);
            invalid(s=>s.Stocks[0].HistoryDays=new long[0]);invalid(s=>s.Stocks[0].HistoryDays[0]=-1);
            invalid(s=>s.Stocks[0].HistoryDays[0]=s.Day+1);invalid(s=>s.Stocks[0].HistoryDays[2]=1);
            invalid(s=>s.Stocks[0].HistoryOpenings[1]=true);invalid(s=>s.Stocks[0].HistoryOpenings[2]=false);
            invalid(s=>s.DayReports[0].Day=0);invalid(s=>s.DayReports[0].ClosePrices[0]=99);
            invalid(s=>s.DayReports[0].CloseAssets++);invalid(s=>s.Goals=(GoalFlags)32);
            invalid(s=>s.DayReports=new[]{s.DayReports[0],s.DayReports[0]});
            var s2=Next(Next(valid));s2.Trades=new[]{new TradeRecord{Day=3,Company=0,Buy=true,Quantity=1,Price=10000,Fee=10},
                new TradeRecord{Day=1,Company=0,Buy=true,Quantity=1,Price=10000,Fee=10}};
            Throws(()=>SaveCodec.Encode(s2));
        }
        static void CheckInvalidBytes()
        {
            var s=Next(Market.Trade(Market.Create(),0,1,true));byte[] bytes=SaveCodec.Encode(s);
            int footer=CurrentHistoryOffset(bytes);
            int trades=footer+s.Stocks.Sum(p=>p.History.Length*9);
            Throws(()=>SaveCodec.Decode(ChangeBytes(bytes,trades,BitConverter.GetBytes(101))));
            Throws(()=>SaveCodec.Decode(ChangeBytes(bytes,trades,BitConverter.GetBytes(-1))));
            Throws(()=>SaveCodec.Decode(ChangeBytes(bytes,trades+4,BitConverter.GetBytes(s.Day+1))));
            Throws(()=>SaveCodec.Decode(ChangeBytes(bytes,bytes.Length-36,BitConverter.GetBytes(32))));
            Throws(()=>SaveCodec.Decode(ChangeBytes(bytes,footer+s.Stocks[0].History.Length*8,new byte[]{2})));
            Throws(()=>SaveCodec.Decode(ChangeBytes(bytes,trades+4+8+4,new byte[]{2})));
            Throws(()=>SaveCodec.Decode(ChangeBytes(bytes,8,new byte[0],bytes.Length-33)));
        }
        static int CurrentHistoryOffset(byte[] bytes)
        {
            // Locate v9's history section so corrupt-byte checks target real fields, not incidental offsets.
            using(var reader=new BinaryReader(new MemoryStream(bytes))) {
                reader.BaseStream.Position=8+5*8+4+1+4;
                for(int i=0;i<Market.Companies.Length;i++) {
                    reader.BaseStream.Position+=3*8;int count=reader.ReadInt32();reader.BaseStream.Position+=count*8;
                }
                reader.BaseStream.Position+=Market.Companies.Length*8;
                for(int i=0;i<InfoMarket.SlotCount;i++)if(reader.ReadBoolean())throw new Exception("Byte fixture must not have legacy information.");
                if(reader.ReadInt32()!=0)throw new Exception("Byte fixture must not have clues.");
                reader.BaseStream.Position+=7*8+1;
                if(reader.ReadBoolean()) {
                    reader.BaseStream.Position+=10*8;int prices=reader.ReadInt32();reader.BaseStream.Position+=prices*16;
                    int news=reader.ReadInt32();
                    for(int i=0;i<news;i++){reader.BaseStream.Position+=4+2*8;reader.ReadString();reader.ReadString();reader.BaseStream.Position+=2*8;}
                    reader.BaseStream.Position+=8; // Work income.
                }
                return (int)reader.BaseStream.Position;
            }
        }
        static byte[] ChangeBytes(byte[] source,int offset,byte[] replacement,int length=-1)
        {
            byte[] body=source.Take(length<0?source.Length-32:length).ToArray();
            Array.Copy(replacement,0,body,offset,replacement.Length);
            using(var hash=SHA256.Create())return body.Concat(hash.ComputeHash(body)).ToArray();
        }
    }
}
