// Stat columns recorded once per in-game year: C(game int variable, label, kind).
// Adding a stat is one line here; run files and the report pick it up by name.
//   K_LEVEL    a value you have right now (gold, land); the report charts it and keeps the peak
//   K_COUNTER  a running total the game keeps per playthrough; lifetime adds each run's final value
//   K_INCOME / K_EXPENSE  this year's turn-report line, reset every year; lifetime adds every year
// Year must stay first.
C(Year, "Year", K_LEVEL)
C(YourGold, "Gold", K_LEVEL)
C(YourMen, "Men", K_LEVEL)
C(YourLand, "Land", K_LEVEL)
C(PublicOpinion, "Public opinion", K_LEVEL)
C(YourPeasants, "Peasants", K_LEVEL)
C(YourSoldiers, "Soldiers", K_LEVEL)
C(YourKnights, "Knights", K_LEVEL)
C(YourBandits, "Bandits", K_LEVEL)
C(YourTribal, "Tribal warriors", K_LEVEL)
C(YourTribalBerserk, "Tribal berserkers", K_LEVEL)
C(BattleCount, "Battles", K_COUNTER)
C(TotalKills, "Enemies slain", K_COUNTER)
C(TotalDeaths, "Troops lost", K_COUNTER)
C(TotalTaxGold, "Taxes", K_INCOME)
C(TotalTributeGold, "Tribute", K_INCOME)
C(TotalMineIncome, "Mines", K_INCOME)
C(TotalLocationGold, "Locations", K_INCOME)
C(TotalCaravanGold, "Caravans", K_INCOME)
C(TotalPillagedGold, "Pillaging", K_INCOME)
C(TotalOtherIncome, "Trade & harvest", K_INCOME)
C(TotalWagesGold, "Wages", K_EXPENSE)
C(TotalExpenseGold, "Other expenses", K_EXPENSE)
C(TotalTrainingCost, "Training", K_EXPENSE)
C(TotalRobbedGold, "Pillaged by raiders", K_EXPENSE)
