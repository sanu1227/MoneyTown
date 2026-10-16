using System;
using System.Collections.Generic;

namespace MoneyTown
{
    public static class Story
    {
        public const int Count = 6;
        public static readonly IReadOnlyList<long> Thresholds = Array.AsReadOnly(new long[] {
            0, 2000000, 10000000, 100000000, 1000000000, 10000000000
        });

        public static void Record(GameState state)
        {
            Validate(state);
            long assets = Market.Total(state);
            while (state.StoryUnlocked < Count && assets >= Thresholds[state.StoryUnlocked])
                state.StoryUnlocked++;
        }

        public static int Next(GameState state)
        {
            Validate(state);
            return state.StorySeen < state.StoryUnlocked ? state.StorySeen : -1;
        }

        public static GameState Acknowledge(GameState previous, int index)
        {
            Market.Validate(previous);
            if (index < 0 || index != Next(previous))
                throw new InvalidOperationException("현재 이야기를 먼저 확인하세요.");
            var state = previous.Copy();
            state.StorySeen++;
            return Market.Validate(state);
        }

        public static void Validate(GameState state)
        {
            if (state == null || state.StoryUnlocked < 1 || state.StoryUnlocked > Count
                || state.StorySeen < 0 || state.StorySeen > state.StoryUnlocked)
                throw new InvalidOperationException("이야기 진행 기록이 손상되었습니다.");
        }
    }
}
