using System;
using UnityEngine;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        void DrawRecoveryControls(Transform parent)
        {
            if (!blocked) return;
            GameState backup;
            try {
                store = store ?? new GameStore(GameRuntime.DataDirectory);
                backup = store.PreviewBackup();
            } catch (Exception) {
                Label(parent, "백업을 읽거나 검증하지 못했습니다. 기존 파일은 그대로 보존됩니다.", 19, Red, 80);
                return;
            }
            if (backup == null) {
                Label(parent, "복구할 백업이 없습니다. ‘새 게임 시작’은 새 진행을 만듭니다.", 19, Muted, 80);
                return;
            }
            Label(parent, "복구 가능한 백업 · " + backup.Day + "일차\n총자산 " + Money(Market.Total(backup)), 22, Mint, 80);
            Label(parent, "백업으로 복구하면 이 시점의 진행을 이어갑니다. 현재 파일과 백업은 별도 사본으로 보존됩니다.", 18, Muted, 84);
            Button(parent, "이 백업으로 복구", () => {
                GameState recovered;
                try {
                    recovered = store.RestoreBackup();
                } catch (Exception) {
                    Feedback("백업 복구 실패. 기존 진행은 유지됩니다. 저장 공간을 확인하세요.", true);
                    return;
                }
                state = recovered; blocked = false; resetPending = false;
                marketClock.Reset(); marketWasRunning = false;
                Feedback("백업의 " + state.Day + "일차 진행을 복구했습니다.");
                RenderTab(); scroll.verticalNormalizedPosition = 1;
            }, Hex("266957"), 68);
            Label(parent, "아래 ‘새 게임 시작’은 복구 대신 처음부터 시작합니다.", 18, Muted, 58);
        }
    }
}
