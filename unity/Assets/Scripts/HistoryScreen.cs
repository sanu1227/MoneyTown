using System.Linq;
using UnityEngine;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        long? selectedReportDay;
        void DrawProgress(Transform parent)
        {
            var card = Card(parent, 18, 6);
            Label(card, "작은 목표", 25, Gold, 38);
            var flags = new[] { GoalFlags.FirstTrade, GoalFlags.FirstProfit, GoalFlags.AssetsTwoMillion, GoalFlags.Diversified, GoalFlags.AssetsTenMillion };
            var titles = new[] { "첫 주식 거래", "첫 실현수익", "총자산 200만원 달성", "서로 다른 기업 3곳 보유", "총자산 1,000만원 달성" };
            for (int i = 0; i < flags.Length; i++) {
                bool done = (state.Goals & flags[i]) != 0;
                Label(card, (done ? "✓ " : "○ ") + titles[i], 20, done ? Mint : Muted, 32);
            }
            Label(card, "완료 기록은 유지됩니다 · 자금 보상 없음", 16, Muted, 30);
            if (blocked) return;
            if (Progress.FundsExhausted(state)) {
                Label(card, "거래를 이어갈 자금이 없습니다", 22, Gold, 40);
                Label(card, "물류센터 알바로 하루를 보내고 일당 "+Money(Market.WarehouseWage)+"을 받아 다시 투자할 수 있습니다. 위의 알바 버튼을 눌러 보세요.", 19, Muted, 112);
            } else if (Progress.WaitingForFunds(state)) {
                Label(card, state.DepositDue > 0 ? "지금은 매수할 현금이 부족합니다. 알바로 하루를 보내도 예금 만기가 진행됩니다." : "현재 시세로 살 수 있는 주식이 없습니다. 물류센터 알바로 기본자금을 마련할 수 있습니다.", 19, Muted, 92);
            }
        }

        void DrawHistory()
        {
            Label(content, "거래와 결산 기록", 30, Gold, 52);
            Label(content, "이전 저장에 없는 거래·결산은 만들어내지 않습니다.", 17, Muted, 52);
            Button(content, "내 자산으로 돌아가기", () => SwitchTab(2), Raised, 62);
            Label(content, "일별 결산 · 최근 30일", 24, null, 42);
            if (state.DayReports.Length == 0) Label(content, "다음 날을 진행하면 첫 결산이 남습니다.", 20, Muted, 52);
            foreach (var item in state.DayReports.Reverse()) {
                var report = item;
                Button(content, report.Day + "일차  " + Signed(report.Profit) + "  ›", () => {
                    selectedReportDay = report.Day; tab = 4; RenderTab(); scroll.verticalNormalizedPosition = 1;
                }, Panel, 64);
            }
            Label(content, "거래 내역 · 최근 100건", 24, null, 42);
            if (state.Trades.Length == 0) Label(content, "이번 업데이트 이후의 거래부터 기록합니다.", 20, Muted, 52);
            foreach (var trade in state.Trades.Reverse()) {
                var card = Card(content, 14, 4);
                Label(card, trade.Day + "일차 · " + Market.Companies[trade.Company].Name + " · " + (trade.Buy ? "매수" : "매도"), 21, trade.Buy ? Red : Blue, 34);
                Label(card, trade.Quantity + "주 × " + Money(trade.Price) + " · 수수료 " + Money(trade.Fee), 18, Muted, 48);
                if (!trade.Buy) Label(card, "실현손익 " + Signed(trade.RealizedProfit) + " · 수수료 포함", 20, Movement(trade.RealizedProfit), 36);
            }
        }
    }
}
