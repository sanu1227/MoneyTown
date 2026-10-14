using System;

namespace MoneyTown
{
    // Practice has its own market. Restoring a step replays only this fixed scenario.
    public sealed class TutorialSession
    {
        public const int Count=10, Company=1, OfferIndex=5;
        public int Step { get; private set; }
        public GameState State { get; private set; }
        public IntelOffer Offer { get; private set; }
        public ClueRecord Clue => State.Clues.Length==0?null:State.Clues[0];
        TutorialSession()
        {
            State=Market.Create(122);
            for(int i=0;i<3;i++)State=AdvanceDay(State);
            Offer=Intelligence.Offers(State)[OfferIndex];
            if(Offer.Company!=Company||Offer.Grade!=4||Offer.Story!=17||!Offer.ClaimedPositive||!Offer.ActualPositive)
                throw new InvalidOperationException("튜토리얼 사건 구성을 확인하세요.");
        }
        static GameState AdvanceDay(GameState s)=>Market.AcknowledgeSettlement(Market.NextDay(s));
        public static TutorialSession Restore(int step)
        {
            if(step<0||step>Count)throw new ArgumentOutOfRangeException(nameof(step));
            var lesson=new TutorialSession();
            while(lesson.Step<step)lesson.Advance(lesson.Step,Company);
            return lesson;
        }
        public void Advance(int expectedStep,int answer=-1)
        {
            if(expectedStep!=Step||Step>=Count)throw new InvalidOperationException("이미 진행한 단계입니다.");
            if(Step==4&&answer!=Company)throw new InvalidOperationException("게임박람회와 연결되는 업종을 찾아보세요.");
            var changed=State;
            switch(Step) {
                case 1: changed=Market.Intraday(State);break;
                case 2: changed=Market.Trade(State,Company,1,true);break;
                case 3: changed=Intelligence.Buy(State,OfferIndex,State.Day);break;
                case 5: changed=AdvanceDay(State);break;
                case 6: changed=Market.Trade(State,Company,1,false);break;
                case 7: changed=Market.Deposit(State);break;
                case 8:
                    for(int i=0;i<3;i++)changed=AdvanceDay(changed);
                    break;
            }
            State=changed;Step++;
        }
    }
}
