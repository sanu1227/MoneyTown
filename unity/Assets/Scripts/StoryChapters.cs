namespace MoneyTown
{
    public sealed class StoryChapter
    {
        public readonly string Title, Requirement, ArtName;
        public readonly string[] Pages;
        public StoryChapter(string title,string requirement,string artName,params string[] pages)
        { Title=title;Requirement=requirement;ArtName=artName;Pages=pages; }
    }

    public static class StoryChapters
    {
        public static readonly StoryChapter[] All={
            new StoryChapter("아직 작은 시작","프롤로그","00-beginning",
                "이건 내가 세계 최고의 부자가 되는 이야기이다",
                "수중에 있는 돈은 백만 원. 낡은 책상에 공책을 펼쳤다.\n아직은 작지만, 이 돈으로 한번 시작해 보자.",
                "게임 박람회가 열린다는 기사 옆에 물음표를 적었다.\n어떤 회사에 좋은 소식일까? 먼저 거래부터 연습해 보자."),
            new StoryChapter("두 배가 된 가능성","총자산 200만원","01-first-step",
                "총자산이 이백만 원을 넘었다. 숫자를 두 번이나 확인했다.\n처음엔 막막했는데, 여기까지는 왔네.",
                "지난 메모에는 맞은 예상도, 틀린 예상도 남아 있었다.\n다음에도 참고하게, 왜 그렇게 생각했는지 적어 두자.",
                "잠깐 바람을 쐬러 올라온 옥상. 아래로 가게들이 보였다.\n저 회사들은 요즘 어떤 물건을 팔고 있을까?"),
            new StoryChapter("나만의 작은 사무실","총자산 1천만원","02-own-desk",
                "천만 원을 넘기자, 뒤섞인 자료부터 정리하고 싶어졌다.\n새 공책엔 회사마다 한 장씩 써 봐야겠어.",
                "같은 회사를 다룬 기사 두 개의 내용이 서로 달랐다.\n둘 중 어느 쪽이 최근 얘기지? 날짜부터 확인하자.",
                "서류 더미를 치우고 나니 책상 위에 빈자리가 생겼다.\n이 정도면 됐어. 여기서 차분히 살펴보자."),
            new StoryChapter("숫자 너머의 도시","총자산 1억원","03-city-view",
                "출근길에 계좌를 열었다. 총자산이 일억 원을 넘었다.\n오늘도 같은 버스를 타는데, 숫자는 꽤 달라졌네.",
                "빵집에 불이 켜지고, 골목마다 택배 상자가 쌓였다.\n화면에서 보던 회사들이 이렇게 가까이 있었구나.",
                "다리 위에서 잠깐 쉬며, 눈여겨보던 회사를 떠올렸다.\n주문이 늘었다던데, 비용은 얼마나 들었을까?"),
            new StoryChapter("혼자보다 넓은 시야","총자산 10억원","04-boardroom",
                "십억 원을 넘긴 뒤, 투자 이야기를 나누던 사람들을 만났다.\n혼자 적어 둔 생각인데… 같이 한번 봐 주실래요?",
                "같은 기사를 펼쳐 놓고도 의견은 서로 달랐다.\n아, 계약이 확정된 게 아니었네요. 제가 앞서갔어요.",
                "질문을 하나 꺼내자, 옆자리에서 자료를 펼쳐 보였다.\n그 부분은 어떻게 확인하셨어요? 저도 적어 둘게요."),
            new StoryChapter("더 넓은 세상으로","총자산 100억원","05-world-horizon",
                "백억 원을 넘긴 날, 처음 쓰던 공책을 다시 펼쳤다.\n세계 최고의 부자라니. 그땐 참 크게도 써 놨네.",
                "창밖 항구에는 낯선 나라로 떠나는 배들이 줄지어 있었다.\n저 배들이 어디로 가는지도 이제 궁금해지네.",
                "바람에 넘어간 공책 끝에는 아직 빈 페이지가 많았다.\n알아보고 싶은 회사가 많아. 오늘은 어디부터 살펴볼까?")
        };
    }
}
