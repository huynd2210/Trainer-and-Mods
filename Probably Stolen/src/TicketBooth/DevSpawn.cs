using System.Linq;
using UnityEngine;

namespace TicketBooth
{
    // Developer tools only (dev.txt next to the game's exe): Ctrl+Shift+T sends the next
    // kind of fake to the front of the queue, cycling through every trick in order, to
    // check them or take screenshots. Ring the bell to call them in. Does nothing in
    // normal play, and the claim is never saved.
    internal static class DevSpawn
    {
        private static GameObject host;
        private static int next;

        internal static void Enable()
        {
            if (host != null || !DevTools.IsEnabled)
                return;
            host = new GameObject("TicketBoothDevSpawn");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Hotkey>();
            Debug.Log("[TicketBooth] Dev tools on: Ctrl+Shift+T sends the next fake ticket holder to the counter.");
        }

        internal static void Disable()
        {
            if (host != null)
                Object.Destroy(host);
            host = null;
        }

        internal static void SpawnNext()
        {
            StoreClientManager manager = PlayerStore.Instance?.storeClientManager;
            if (!State.Ready || manager == null)
                return;
            ClaimFactory.Context ctx = Context();
            var tricks = ClaimFactory.AllTricks.ToList();
            for (int tries = 0; tries < tricks.Count; tries++)
            {
                var (kind, flaw) = tricks[next];
                next = (next + 1) % tricks.Count;
                // State.NextClaimId keeps ids unique; the claim itself stays out of the save.
                Claim claim = ClaimFactory.ForgeryOf(Daily.Rng, State.NextClaimId(), ctx, kind, flaw);
                if (claim == null)
                    continue;   // a lottery trick before any draw
                manager.AddNextClient(Claims.CreateClaimant(claim));
                Debug.Log("[TicketBooth] Dev: next in line is a fake " + (kind == Claim.Lottery ? "lottery ticket" : "scratcher")
                          + " (" + flaw + "), " + claim.Data + ", asking " + claim.Asked);
                return;
            }
        }

        // The forger's usual view of the draw. Not licensed for a draw yet: the game's own
        // latest draw, so lottery fakes can still be seen.
        private static ClaimFactory.Context Context()
        {
            ClaimFactory.Context ctx = Claims.ForgeryContext(State.Today);
            PlayerStore store = PlayerStore.Instance;
            if (ctx.DrawEdition <= 0 && !string.IsNullOrEmpty(store.currentWinningLotteryNumber) && store.currentLotteryEdition > 1)
            {
                ctx.DrawEdition = store.currentLotteryEdition - 1;
                ctx.DrawNumbers = store.currentWinningLotteryNumber;
                ctx.DrawDay = State.Today;
            }
            return ctx;
        }

        private sealed class Hotkey : MonoBehaviour
        {
            private void Update()
            {
                if (Input.GetKeyDown(KeyCode.T)
                    && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                    SpawnNext();
            }
        }
    }
}
