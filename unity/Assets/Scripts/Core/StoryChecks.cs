using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace MoneyTown
{
    public static class StoryChecks
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Reject(Action action)
        {
            try { action(); } catch (Exception) { return; }
            throw new Exception("Invalid story progress was accepted.");
        }
        static GameState WithAssets(long cash)
        {
            var state = Market.Create();
            state.Cash = state.DayStartAssets = cash;
            return state;
        }
        public static int Run()
        {
            CheckIntroduction(); CheckThresholds(); CheckTotal(); CheckDrawdown(); CheckOrderedQueue();
            CheckMarketActions(); CheckRoundTrip(); CheckLegacy(); CheckVersionElevenStore(); CheckCorruption();
            return 10;
        }
        static void CheckIntroduction()
        {
            var state = Market.Create(); byte[] before = SaveCodec.Encode(state);
            Check(state.StoryUnlocked == 1 && state.StorySeen == 0 && Story.Next(state) == 0, "A new game must start with its introduction.");
            var read = Story.Acknowledge(state, 0);
            Check(read.StorySeen == 1 && Story.Next(read) == -1 && before.SequenceEqual(SaveCodec.Encode(state)), "Reading the introduction mutated its input or left a pending story.");
            Reject(() => Story.Acknowledge(read, 0));
            Check(Story.Next(SaveCodec.Decode(SaveCodec.Encode(read))) == -1, "Reload replayed an acknowledged introduction.");
        }
        static void CheckThresholds()
        {
            Check(Story.Count == 6 && Story.Thresholds.SequenceEqual(new long[] { 0, 2000000, 10000000, 100000000, 1000000000, 10000000000 }), "Story milestones changed unexpectedly.");
            for (int i = 1; i < Story.Count; i++) {
                foreach (long delta in new long[] { -1, 0, 1 }) {
                    var state = WithAssets(Story.Thresholds[i] + delta);
                    Market.Validate(state);
                    Check(state.StoryUnlocked == 1, "Validation must not unlock stories.");
                    Story.Record(state);
                    Check(state.StoryUnlocked == (delta < 0 ? i : i + 1) && state.StorySeen == 0, "A story milestone fired at the wrong asset boundary.");
                }
            }
        }
        static void CheckTotal()
        {
            var state = WithAssets(1800000);
            state.DepositDue = state.Day + 3;
            state.Stocks[0].Quantity = 10; state.Stocks[0].Cost = 100000;
            Story.Record(state);
            Check(Market.Total(state) == 2000000 && state.StoryUnlocked == 2, "The story asset total must include cash, shares and deposit principal.");
        }
        static void CheckDrawdown()
        {
            var state = WithAssets(100000000); Story.Record(state);
            state.Cash = 0; Story.Record(state);
            Check(state.StoryUnlocked == 4, "A drawdown relocked an earned story.");
            for (int i = 0; i < 4; i++) state = Story.Acknowledge(state, i);
            state.Cash = 100000000; Story.Record(state);
            Check(Story.Next(state) == -1 && state.StorySeen == 4, "A rebound replayed completed stories.");
        }
        static void CheckOrderedQueue()
        {
            var state = WithAssets(10000000000); Story.Record(state);
            for (int i = 0; i < Story.Count; i++) {
                byte[] before = SaveCodec.Encode(state);
                Check(Story.Next(state) == i, "A leap did not queue stories in order.");
                Reject(() => Story.Acknowledge(state, i + 1));
                Reject(() => Story.Acknowledge(state, i - 1));
                Check(before.SequenceEqual(SaveCodec.Encode(state)), "A rejected acknowledgment mutated progress.");
                var next = Story.Acknowledge(state, i);
                Check(before.SequenceEqual(SaveCodec.Encode(state)), "Acknowledgment must return a new state.");
                state = next;
            }
            Check(Story.Next(state) == -1 && state.StorySeen == Story.Count, "The final story remained pending.");
            Reject(() => Story.Acknowledge(state, Story.Count));
        }
        static void CheckMarketActions()
        {
            Func<GameState, GameState>[] actions = {
                s => Market.Trade(s, 4, 1, true), s => Market.Deposit(s),
                s => Market.Intraday(s), s => Market.NextDay(s), s => Market.WorkWarehouse(s)
            };
            foreach (var action in actions) {
                var state = WithAssets(2002000); byte[] before = SaveCodec.Encode(state);
                var changed = action(state);
                Check(changed.StoryUnlocked == 2 && before.SequenceEqual(SaveCodec.Encode(state)), "A market action missed story progress or mutated its input.");
            }
            var settlement = Market.NextDay(Market.Create()); settlement.Cash = 2000000;
            Check(Market.AcknowledgeSettlement(settlement).StoryUnlocked == 2, "Settlement acknowledgment missed a milestone.");
        }
        static void CheckRoundTrip()
        {
            var state = WithAssets(1000000000); Story.Record(state);
            state = Story.Acknowledge(Story.Acknowledge(state, 0), 1);
            byte[] bytes = SaveCodec.Encode(state); var restored = SaveCodec.Decode(bytes);
            Check(BitConverter.ToInt32(bytes, 4) == SaveCodec.CurrentVersion && restored.StoryUnlocked == 5 && restored.StorySeen == 2
                && Story.Next(restored) == 2 && bytes.SequenceEqual(SaveCodec.Encode(restored)), "Story progress did not round-trip in the current version.");
            byte[] legacy = CoreChecks.LegacyBytes(state, 11);
            var oldBody = legacy.Take(legacy.Length - 32).ToArray();
            var newBody = CoreChecks.LegacyBytes(state, 12).Take(legacy.Length - 32).ToArray();
            Array.Copy(BitConverter.GetBytes(11), 0, newBody, 4, 4);
            Check(oldBody.SequenceEqual(newBody), "Version twelve must append story fields after the unchanged version-eleven body.");
        }
        static void CheckLegacy()
        {
            var state = WithAssets(100000000); Progress.Record(state);
            for (int version = 1; version <= 11; version++) {
                byte[] bytes = CoreChecks.LegacyBytes(state, version); var restored = SaveCodec.Decode(bytes);
                Check(restored.StoryUnlocked == 1 && restored.StorySeen == 0 && Story.Next(restored) == 0,
                    "Legacy decoding must default to an unseen introduction without recording wealth.");
                Check(Market.Total(restored) == Market.Total(state) && (restored.Goals & GoalFlags.AssetsTenMillion) != 0,
                    "Legacy story defaults changed money or existing goal recovery.");
                Story.Record(restored);
                Check(restored.StoryUnlocked == 4 && restored.StorySeen == 0, "Startup recording missed legacy wealth.");
                Check(bytes.SequenceEqual(CoreChecks.LegacyBytes(state, version)), "Legacy read changed its source.");
            }
        }
        static void CheckVersionElevenStore()
        {
            string directory = Path.Combine(Path.GetTempPath(), "moneytown-story-" + Guid.NewGuid().ToString("N"));
            try {
                var state = Market.Trade(WithAssets(10000000), 4, 17, true);
                state.Stocks[4].Price = 1234; state.Stocks[4].History[state.Stocks[4].History.Length - 1] = 1234;
                var store = new GameStore(directory); string path = Path.Combine(directory, "game-unity-v1.bin");
                byte[] bytes = CoreChecks.LegacyBytes(state, 11); File.WriteAllBytes(path, bytes);
                var restored = store.Load();
                Check(!store.PricesMigrated && restored.Stocks[4].Price == 1234 && restored.Stocks[4].Quantity == 17
                    && Market.Total(restored) == Market.Total(state) && File.ReadAllBytes(path).SequenceEqual(bytes),
                    "Version-eleven loading must not repeat the old price migration or rewrite the save.");
                Story.Record(restored); store.Save(Story.Acknowledge(restored, 0));
                var again = store.Load();
                Check(!store.PricesMigrated && again.StoryUnlocked == restored.StoryUnlocked && again.StorySeen == 1
                    && Market.Total(again) == Market.Total(state), "Story saving changed wealth or failed to persist progress.");
            } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
        static void CheckCorruption()
        {
            byte[] bytes = CoreChecks.LegacyBytes(Market.Create(), 12);
            foreach (var pair in new[] { new[] { 0, 0 }, new[] { 7, 0 }, new[] { 1, -1 }, new[] { 1, 2 }, new[] { int.MaxValue, 0 } }) {
                var state = Market.Create(); state.StoryUnlocked = pair[0]; state.StorySeen = pair[1];
                Reject(() => SaveCodec.Encode(state)); Reject(() => Story.Next(state)); Reject(() => Story.Record(state));
                Reject(() => Story.Acknowledge(state, 0));
                Check(state.StoryUnlocked == pair[0] && state.StorySeen == pair[1], "Invalid story handling changed input.");
                var body = bytes.Take(bytes.Length - 32).ToArray();
                Array.Copy(BitConverter.GetBytes(pair[0]), 0, body, body.Length - 8, 4);
                Array.Copy(BitConverter.GetBytes(pair[1]), 0, body, body.Length - 4, 4);
                using (var hash = SHA256.Create()) Reject(() => SaveCodec.Decode(body.Concat(hash.ComputeHash(body)).ToArray()));
            }
            var missing = bytes.Take(bytes.Length - 36).ToArray();
            using (var hash = SHA256.Create()) Reject(() => SaveCodec.Decode(missing.Concat(hash.ComputeHash(missing)).ToArray()));
            var invalidAssets = WithAssets(-1);
            Reject(() => Story.Record(invalidAssets));
            Check(invalidAssets.StoryUnlocked == 1 && invalidAssets.StorySeen == 0, "A failed wealth calculation changed story progress.");
        }
    }
}
