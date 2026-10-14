using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MoneyTown
{
    public sealed class CatalogClue
    {
        public readonly string Id, Category, Source, NegativeTitle, PositiveTitle, NegativeBody, PositiveBody, Evidence, Risk, ActualNegative, ActualPositive;
        public readonly string ReportNegativeBody, ReportPositiveBody;
        public readonly int Grade, Event, Company, Confidence, Impact;
        public readonly InfoKind Kind;
        public readonly long Price;
        internal CatalogClue(string[] c)
        {
            if(c.Length!=18&&c.Length!=20)throw new InvalidDataException("정보 목록 열 수가 올바르지 않습니다.");
            Id=c[0];Grade=int.Parse(c[1]);Event=int.Parse(c[2]);Company=int.Parse(c[3]);Category=c[4];Source=c[5];
            Kind=(InfoKind)int.Parse(c[6]);Price=long.Parse(c[7],CultureInfo.InvariantCulture);Confidence=int.Parse(c[8]);Impact=int.Parse(c[9]);
            NegativeTitle=c[10];PositiveTitle=c[11];NegativeBody=c[12].Replace("\\n","\n");PositiveBody=c[13].Replace("\\n","\n");
            Evidence=c[14];Risk=c[15];ActualNegative=c[16];ActualPositive=c[17];
            ReportNegativeBody=c.Length==20?c[18].Replace("\\n","\n"):NegativeBody;
            ReportPositiveBody=c.Length==20?c[19].Replace("\\n","\n"):PositiveBody;
            if((Grade==6||Grade==8||Grade==10)&&(string.IsNullOrWhiteSpace(ReportNegativeBody)||string.IsNullOrWhiteSpace(ReportPositiveBody)))
                throw new InvalidDataException("기업 지정 분석 본문이 누락되었습니다.");
        }
    }
    public static class InformationCatalog
    {
        public const int CurrentEdition=3;
        public const int CurrentEventCount=300;
        static CatalogClue[][] catalogs;
        static int[][][] companyEvents;
        public static System.Collections.Generic.IReadOnlyList<CatalogClue> All => Array.AsReadOnly(catalogs?[CurrentEdition-1] ?? throw new InvalidOperationException("정보 목록을 먼저 불러오세요."));
        public static CatalogClue Get(int grade,int eventId,int edition=CurrentEdition)
        {
            if(grade<1||grade>10||edition<1||edition>CurrentEdition)throw new ArgumentOutOfRangeException();
            int count=edition==CurrentEdition?CurrentEventCount:100;
            if(eventId<0||eventId>=count)throw new ArgumentOutOfRangeException(nameof(eventId));
            if(catalogs==null)throw new InvalidOperationException("정보 목록을 먼저 불러오세요.");
            return catalogs[edition-1][(grade-1)*count+eventId];
        }
        internal static int EventFor(int company,uint selection,int edition=CurrentEdition)
        {
            if(edition<1||edition>CurrentEdition||company<0||company>=(edition==CurrentEdition?12:6))throw new ArgumentOutOfRangeException();
            if(companyEvents==null)throw new InvalidOperationException("정보 목록을 먼저 불러오세요.");
            var choices=companyEvents[edition-1][company];return choices[selection%(uint)choices.Length];
        }
        static CatalogClue[] Parse(string text,int count,int companies)
        {
            if(string.IsNullOrWhiteSpace(text))throw new InvalidDataException("정보 목록이 비어 있습니다.");
            var lines=text.Trim().Split('\n');
            var parsed=lines.Skip(1).Select(line=>new CatalogClue(line.TrimEnd('\r').Split('\t'))).OrderBy(r=>r.Grade).ThenBy(r=>r.Event).ToArray();
            if(parsed.Length!=count*10 || parsed.Select(r=>r.Id).Distinct().Count()!=count*10)throw new InvalidDataException($"정보는 중복 없이 {count*10:N0}개여야 합니다.");
            for(int i=0;i<parsed.Length;i++) {
                var r=parsed[i];int grade=i/count+1;
                if(r.Grade!=grade||r.Event!=i%count||r.Company<0||r.Company>=companies||(int)r.Kind<0||(int)r.Kind>3||
                    r.Id!=$"G{grade:00}-{r.Event+1:000}"||r.Confidence!=Intelligence.Tiers[grade-1].Confidence||
                    r.Price<Intelligence.Tiers[grade-1].Price*80/100||r.Price>Intelligence.Tiers[grade-1].Price*120/100||
                    r.Impact<100||r.Impact>350||string.IsNullOrWhiteSpace(r.PositiveBody)||string.IsNullOrWhiteSpace(r.NegativeBody)||
                    string.IsNullOrWhiteSpace(r.Source)||string.IsNullOrWhiteSpace(r.Category))throw new InvalidDataException("정보 목록의 등급·가격·내용을 확인하세요.");
                if(i>=count && (r.Company!=parsed[r.Event].Company||r.Impact!=parsed[r.Event].Impact))throw new InvalidDataException("등급별 사건 대상이 다릅니다.");
            }
            return parsed;
        }
        public static void Load(string text,string previousText=null,string secondEditionText=null)
        {
            if(previousText==null||secondEditionText==null)throw new InvalidDataException("이전 구매본의 정보 목록이 필요합니다.");
            var parsed=Parse(text,CurrentEventCount,12);
            var previous=Parse(previousText,100,6);
            var second=Parse(secondEditionText,100,6);
            foreach(var archive in new[]{previous,second})for(int i=0;i<archive.Length;i++) {
                var old=archive[i];var current=parsed[(old.Grade-1)*CurrentEventCount+old.Event];
                if(current.Company!=old.Company||current.Impact!=old.Impact||current.Price!=old.Price||
                    current.Confidence!=old.Confidence||current.Category!=old.Category||current.Source!=old.Source||
                    current.ActualPositive!=old.ActualPositive||current.ActualNegative!=old.ActualNegative)
                    throw new InvalidDataException("정보 확장으로 기존 사건이나 가격을 바꿀 수 없습니다.");
            }
            var editions=new[]{previous,second,parsed};
            var events=editions.Select((catalog,index)=>Enumerable.Range(0,index==CurrentEdition-1?12:6)
                .Select(c=>catalog.Where(r=>r.Grade==1&&r.Company==c).Select(r=>r.Event).ToArray()).ToArray()).ToArray();
            if(events.Any(edition=>edition.Any(e=>e.Length==0)))throw new InvalidDataException("기업별 사건이 누락되었습니다.");
            catalogs=editions;companyEvents=events;
        }
    }
}
