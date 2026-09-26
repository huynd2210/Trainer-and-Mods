using UnityEngine;

namespace LonestarTrainer
{
    /// <summary>
    /// One-shot cheats. Every one of these can run from the main menu, so each
    /// guards its own state and reports back instead of throwing.
    /// </summary>
    internal static class GameActions
    {
        private const int CoinGrant = 250;

        private static PlayerShipData Ship()
        {
            try { return Singleton<ShipDatasManager>.Instance().currentPlayerShipData; }
            catch { return null; }
        }

        private static WantedProcess Run()
        {
            try { return Singleton<WantedManager>.Instance().wantedProcess; }
            catch { return null; }
        }

        public static void RepairToFull()
        {
            PlayerShipData ship = Ship();
            if (ship == null) { Overlay.Flash("No ship in play"); return; }

            int missing = ship.maxHP - ship.currentHP;
            if (missing <= 0) { Overlay.Flash("Hull already full"); return; }

            BattleManager battle = Singleton<BattleManager>.Instance();
            if (battle != null && battle.inBattle)
                ship.SetHP_Battle(missing);
            else
                ship.AddCurrent(ref ship.maxHP, ref ship.currentHP, missing);

            Overlay.Flash("Hull repaired to " + ship.currentHP + "/" + ship.maxHP);
        }

        public static void WinBattle()
        {
            BattleManager battle = Singleton<BattleManager>.Instance();
            if (battle == null || !battle.inBattle || battle.enemyShip == null)
            {
                Overlay.Flash("Not in a battle");
                return;
            }

            battle.ShipDownHandler(battle.enemyShip);
            Overlay.Flash("Battle won");
        }

        public static void AddCargoSlot()
        {
            PlayerShipData ship = Ship();
            if (ship == null) { Overlay.Flash("No ship in play"); return; }

            ship.SetLoad(1);
            Overlay.Flash("Cargo limit now " + ship.shipLoadLimit);
        }

        public static void AddCoins()
        {
            WantedProcess run = Run();
            if (run == null) { Overlay.Flash("No voyage in progress"); return; }

            run.SetCoinWithListener(run.starCoin + CoinGrant);
            Overlay.Flash("Star Coins: " + run.starCoin);
        }

        public static void AddMove()
        {
            PlayerShipData ship = Ship();
            if (ship == null) { Overlay.Flash("No ship in play"); return; }

            ship.SetMove_Normal(1);
            Overlay.Flash("Move: " + ship.currentMove + "/" + ship.maxMove);
        }

        /// <summary>
        /// Runs every frame while Infinite Star Coins is on. Refills only once the
        /// purse drops below half the cap, so the banner is not re-animated constantly.
        /// </summary>
        public static void MaintainCoins()
        {
            WantedProcess run = Run();
            if (run == null) return;

            int cap = Define.defineValue != null ? Define.defineValue.coinLimit : 999;
            if (cap <= 0) return;

            if (run.starCoin < cap / 2)
                run.SetCoinWithListener(cap);
        }
    }
}
