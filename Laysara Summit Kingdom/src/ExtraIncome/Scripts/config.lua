-- Extra Income - settings.
-- Edit the values, save, restart the game. The per-map amounts you set in game
-- are kept separately, in amounts.txt next to this mod's Scripts folder.

return {
    -- Monthly amount for a map you have not set an amount on yet.
    DefaultAmount = 0,

    -- How much one press of + or - changes the amount. Hold Shift for BigStep.
    Step = 10,
    BigStep = 100,

    -- Name of the row in the income breakdown (hover the treasury to see it).
    Label = "Extra income",

    -- The game never lets the treasury go above its capacity (the "2000 / 2000"
    -- in the breakdown). true = the extra income follows that rule too.
    RespectTreasuryCap = true,
}
