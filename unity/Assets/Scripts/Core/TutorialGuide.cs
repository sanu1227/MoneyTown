namespace MoneyTown
{
    public enum TutorialAction
    {
        None, OpenCompanies, SelectCompany, Buy, OpenInformation, OpenOffer, BuyInformation,
        ReturnExchange, NextDay, CloseSettlement, Sell, OpenAssets, Deposit, Finish
    }

    // The guide records successful actions in the real game; it never creates a practice market.
    public static class TutorialGuide
    {
        public const int Count=13;
        public static bool Active(GameState state) => state!=null&&state.TutorialActive&&state.TutorialStep<Count;
        public static GameState Start(GameState previous,bool restart)
        {
            Market.Validate(previous);
            var changed=previous.Copy();
            if(restart){changed.TutorialStep=0;changed.TutorialCompany=1;changed.TutorialOffer=-1;}
            if(restart||changed.TutorialDay==0)changed.TutorialDay=changed.Day;
            changed.TutorialActive=changed.TutorialStep<Count;
            return changed;
        }
        public static GameState Pause(GameState previous)
        {
            Market.Validate(previous);
            var changed=previous.Copy();changed.TutorialActive=false;return changed;
        }
        // Call only after the requested action succeeds, then persist the action and guide together.
        public static void Record(GameState changed,TutorialAction action,int value=-1)
        {
            Market.Validate(changed);
            if(!Active(changed)||(int)action!=changed.TutorialStep+1)return;
            if(action==TutorialAction.SelectCompany) {
                if(value<0||value>=Market.Companies.Length)return;
                changed.TutorialCompany=value;
            }
            if(action==TutorialAction.OpenOffer) {
                if(value<0||value>=Intelligence.DailyCount)return;
                changed.TutorialOffer=value;changed.TutorialDay=changed.Day;
            }
            if((action==TutorialAction.Buy||action==TutorialAction.Sell)&&value!=changed.TutorialCompany)return;
            if(action==TutorialAction.BuyInformation&&(value<0||value!=changed.TutorialOffer))return;
            changed.TutorialStep++;
            if(changed.TutorialStep==Count)changed.TutorialActive=false;
        }
    }
}
