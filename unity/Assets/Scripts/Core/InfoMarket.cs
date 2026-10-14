using System;
using System.Linq;

namespace MoneyTown
{
    public enum InfoKind { Newspaper, Report, Briefing, Rumor }

    public sealed class InfoProduct
    {
        public readonly string Name, Source, Reliability, Description;
        public readonly long Price;
        public readonly int Days;
        public InfoProduct(string name, string source, string reliability, string description, long price, int days)
        { Name=name; Source=source; Reliability=reliability; Description=description; Price=price; Days=days; }
    }

    [Serializable]
    public sealed class InfoRecord
    {
        public InfoKind Kind;
        public int Company, RisingCount;
        public long IssuedDay, ExpiresDay;
        public bool PositiveNews, RumorUp;
        public long[] Prices;
        public InfoRecord Copy()
        { var copy=(InfoRecord)MemberwiseClone(); copy.Prices=(long[])Prices.Clone(); return copy; }
    }

    // Read-only archive and migration of information bought before 0.7.
    public static class InfoMarket
    {
        public const int SlotCount = 24; // Four kinds x six companies; newspaper uses one global slot.
        public static readonly InfoProduct[] Products = {
            new InfoProduct("경제신문", "머니타운 경제신문", "취재 기사", "기업 기사와 시장 전체의 등락을 읽습니다.", 500, 1),
            new InfoProduct("기업분석", "MT 리서치 센터", "기업 기초 분석", "성장 성향과 변동성을 확인합니다.", 2000, 3),
            new InfoProduct("시세 브리핑", "마켓 데이터룸", "기록 기반", "발행 시점의 가격 흐름과 고점·저점을 확인합니다.", 1200, 2),
            new InfoProduct("찌라시", "익명 제보자", "낮음 · 미확인", "다음 날 방향에 관한 소문입니다. 틀릴 수 있습니다.", 300, 1)
        };

        static void Require(bool valid, string message)
        { if(!valid) throw new InvalidOperationException(message); }
        public static int Slot(InfoKind kind, int company)
        {
            Require((int)kind>=0 && (int)kind<Products.Length, "정보 종류를 확인하세요.");
            Require(company>=0 && company<6, "종목을 확인하세요.");
            return (int)kind*6+(kind==InfoKind.Newspaper?0:company);
        }
        public static InfoRecord Owned(GameState s, InfoKind kind, int company) => s.Information[Slot(kind,company)];
        public static bool IsCurrent(GameState s, InfoRecord record) => record!=null && record.ExpiresDay>=s.Day;

        internal static InfoRecord Capture(GameState s, InfoKind kind, int company)
        {
            if(kind==InfoKind.Newspaper) company=s.NewsCompany;
            return new InfoRecord {
                Kind=kind, Company=company, IssuedDay=s.Day, ExpiresDay=s.Day+Products[(int)kind].Days-1,
                PositiveNews=s.PositiveNews, RumorUp=false,
                RisingCount=s.Stocks.Count(stock=>stock.History.Length>1 && stock.Price>stock.History[stock.History.Length-2]),
                Prices=(long[])s.Stocks[company].History.Clone()
            };
        }
        internal static void ImportLegacyReports(GameState s)
        {
            // Old reports contain only stable company traits; preserve their original expiry.
            for(int i=0;i<s.Reports.Length;i++) if(s.Reports[i]>=s.Day) {
                var record=Capture(s,InfoKind.Report,i);
                record.IssuedDay=Math.Max(1,s.Reports[i]-2); record.ExpiresDay=s.Reports[i];
                record.Prices=new[] { s.Stocks[i].Price };
                s.Information[Slot(InfoKind.Report,i)]=record;
            }
        }
        internal static void Validate(GameState s)
        {
            const string damaged="구매 정보 데이터가 손상되었습니다.";
            Require(s.Information!=null && s.Information.Length==SlotCount,damaged);
            for(int i=0;i<s.Information.Length;i++) {
                var r=s.Information[i]; if(r==null) continue;
                Require((int)r.Kind>=0 && (int)r.Kind<Products.Length,damaged);
                Require(r.Company>=0 && r.Company<Market.Companies.Length && Slot(r.Kind,r.Company)==i,damaged);
                Require(r.IssuedDay>=1 && r.IssuedDay<=s.Day && r.ExpiresDay==r.IssuedDay+Products[(int)r.Kind].Days-1,damaged);
                Require(r.RisingCount>=0 && r.RisingCount<=Market.Companies.Length,damaged);
                Require(r.Prices!=null && r.Prices.Length>=1 && r.Prices.Length<=30 && r.Prices.Length<=r.IssuedDay,damaged);
                Require(r.Prices.All(price=>price>=100 && price<=Market.Limit),damaged);
                if(r.Kind==InfoKind.Report) Require(s.Reports[r.Company]==r.ExpiresDay,damaged);
            }
        }
    }
}
