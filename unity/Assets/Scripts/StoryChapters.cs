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
                "시작은 작은 방, 낡은 책상, 그리고 백만 원이었다.\n창밖의 도시는 나와 상관없이 바쁘게 움직였다.",
                "신문을 펼쳤다. 누군가에게는 지나가는 소식이, 나에게는 첫 기회일지도 몰랐다.\n이제 내 판단으로 첫 장을 써 내려갈 차례다."),
            new StoryChapter("두 배가 된 가능성","총자산 200만원","01-first-step",
                "자산이 처음의 두 배가 되었다.\n숫자를 몇 번이나 다시 확인하고서야 긴 숨이 나왔다.",
                "운이 따라준 날도 있었고, 내 판단이 틀린 날도 있었다.\n공책에는 수익보다 그 이유를 먼저 적었다.",
                "옥상에서 내려다본 도시는 어제와 같았다.\n달라진 건, 그 안에서 기회를 찾는 내 눈이었다."),
            new StoryChapter("나만의 작은 사무실","총자산 1천만원","02-own-desk",
                "천만 원. 처음에는 멀기만 했던 숫자가 내 계좌에 자리 잡았다.\n책상을 정리하고 새 공책을 꺼냈다.",
                "지면 사이의 단서를 연결하는 시간이 길어졌다.\n비싼 정보도 직접 따져 보지 않으면 종이 한 장일 뿐이었다.",
                "아직 직원도 간판도 없다.\n그래도 이 책상만큼은, 내가 세상과 만나는 작은 사무실이다."),
            new StoryChapter("숫자 너머의 도시","총자산 1억원","03-city-view",
                "처음으로 억이라는 단위를 손에 넣었다.\n들뜬 마음으로 걷던 길에서, 평소에는 지나치던 풍경들이 보였다.",
                "새로 문을 연 가게, 물건을 나르는 사람들, 늦게까지 켜진 사무실.\n차트의 숫자는 결국 이곳에서 만들어지고 있었다.",
                "더 큰돈을 움직이게 된 만큼, 더 오래 생각하기로 했다.\n나는 이제 가격뿐 아니라 그 뒤의 이야기를 읽는다."),
            new StoryChapter("혼자보다 넓은 시야","총자산 10억원","04-boardroom",
                "십억 원을 넘긴 날, 작은 회의실에 사람들이 모였다.\n처음으로 내 판단을 다른 사람들 앞에서 설명했다.",
                "같은 뉴스를 읽어도 결론은 달랐다.\n반대 의견을 듣고 나서야 내가 놓친 조건이 보였다.",
                "모든 답을 아는 사람이 되고 싶었던 건 아니다.\n모르는 것을 인정하고도, 다음 선택을 할 수 있는 사람이 되고 싶었다."),
            new StoryChapter("더 넓은 세상으로","총자산 100억원","05-world-horizon",
                "백억 원. 작은 방에서 적었던 목표를 다시 펼쳤다.\n맨 위에는 여전히 ‘세계 최고의 부자’라는 말이 남아 있었다.",
                "이제 내 눈앞에는 더 넓은 시장과 아직 모르는 기회들이 있다.\n여기까지 왔다고 해서, 다음 선택이 쉬워지는 건 아니었다.",
                "나는 공책의 다음 장을 넘겼다.\n이야기는 아직 끝나지 않았다. 내일의 첫 줄도 내가 써 내려갈 것이다.")
        };
    }
}
