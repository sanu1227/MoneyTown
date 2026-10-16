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
                "작은 방. 낡은 책상. 시작 자금 백만 원.\n좋아. 지금 가진 것부터 시작하자.",
                "신문 한쪽의 작은 기사가 눈에 들어왔다.\n이 소식, 어떤 회사와 이어질까?"),
            new StoryChapter("두 배가 된 가능성","총자산 200만원","01-first-step",
                "계좌에 찍힌 숫자, 2,000,000원.\n진짜… 두 배가 됐네?",
                "맞힌 날도, 판단이 빗나간 날도 있었다.\n얼마 벌었는지보다, 왜 벌었는지 적자.",
                "옥상 아래 도시는 어제처럼 바빴다.\n같은 풍경인데, 이제는 기회가 보여."),
            new StoryChapter("나만의 작은 사무실","총자산 1천만원","02-own-desk",
                "천만 원을 넘긴 날, 새 공책을 꺼냈다.\n이 책상도 이제 좀 정리해야겠어.",
                "신문과 메모 사이로 단서들이 이어졌다.\n비싼 정보라고 답까지 주는 건 아니지.",
                "직원도 간판도 없는 작은 책상 하나.\n여기가 내 첫 사무실이야."),
            new StoryChapter("숫자 너머의 도시","총자산 1억원","03-city-view",
                "익숙한 출근길. 달라진 건 내 계좌였다.\n일억 원이라니… 아직 실감이 안 나.",
                "가게의 불빛, 배달 상자, 늦은 사무실.\n차트 속 숫자가 여기서 만들어지는구나.",
                "더 큰돈 앞에서 발걸음을 잠깐 멈췄다.\n가격만 보지 말자. 그 뒤의 이야기도."),
            new StoryChapter("혼자보다 넓은 시야","총자산 10억원","04-boardroom",
                "십억 원. 작은 회의실에 사람들이 모였다.\n내 생각을 말하는데 왜 이렇게 떨리지?",
                "같은 뉴스에서 서로 다른 결론이 나왔다.\n잠깐. 내가 그 조건을 놓쳤네.",
                "공책에 새로운 질문 하나를 적었다.\n모르면 물어보자. 혼자 다 알 수는 없으니까."),
            new StoryChapter("더 넓은 세상으로","총자산 100억원","05-world-horizon",
                "백억 원. 오래된 목표를 다시 펼쳤다.\n세계 최고의 부자… 아직 가야 할 길이 있네.",
                "창밖으로 더 넓은 시장이 펼쳐졌다.\n여기까지 왔어도, 다음 선택은 신중하게.",
                "공책의 빈 페이지가 바람에 살짝 흔들렸다.\n내 이야기는 아직 끝나지 않았어.")
        };
    }
}
