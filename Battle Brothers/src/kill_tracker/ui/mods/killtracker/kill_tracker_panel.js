/*
 *  Kill Tracker - "Kill Record" panel for the character screen.
 *
 *  Adds a button to the character screen that opens a popup listing every enemy type
 *  the currently-selected brother has killed (counts + grand total). Data is fetched
 *  on demand from Squirrel via SQ.call('killtracker_queryRecord', [brotherId]).
 *
 *  Loaded after the base/Legends character screen JS, so it patches the live classes.
 */
"use strict";

(function () {
	if (typeof CharacterScreen === "undefined" || typeof CharacterScreenDatasource === "undefined")
	{
		console.error("KillTracker: character screen classes not found - panel disabled.");
		return;
	}

	// --- data bridge: fetch the selected brother's kill record from the backend ---
	CharacterScreenDatasource.prototype.killtracker_queryRecord = function (_brotherId)
	{
		if (this.mSQHandle !== null)
		{
			return SQ.call(this.mSQHandle, "killtracker_queryRecord", [_brotherId]);
		}
		return null;
	};

	// --- build and show the popup for the selected brother ---
	CharacterScreen.prototype.killtracker_openPanel = function ()
	{
		var bro = this.mDataSource.getSelectedBrother();
		if (bro === null || bro === undefined)
			return;

		var broId = (bro.id !== undefined) ? bro.id : bro.ID;
		if (broId === undefined || broId === null)
			return;

		var record = this.mDataSource.killtracker_queryRecord(broId);
		if (record === null || record === undefined)
			record = { total: 0, rows: [] };

		var popup = $(".character-screen").createPopupDialog("Kill Record", null, null, "killtracker-popup-dialog");
		popup.addPopupDialogCancelButton(function (_dialog) {
			_dialog.destroyPopupDialog();
		}, false);
		popup.findPopupDialogCancelButton().changeButtonText("Close");

		var container = $('<div class="killtracker-popup-container"/>');

		var total = (record.total !== undefined) ? record.total : 0;
		var header = $('<div class="killtracker-popup-header text-font-big font-color-title"/>');
		header.text("Total kills: " + total);
		container.append(header);

		var list = $('<div class="killtracker-popup-list"/>');
		var rows = record.rows || [];
		if (rows.length === 0)
		{
			var empty = $('<div class="killtracker-popup-empty text-font-normal font-color-label"/>');
			empty.text("This brother has not killed anything yet.");
			list.append(empty);
		}
		else
		{
			for (var i = 0; i < rows.length; ++i)
			{
				var row = $('<div class="killtracker-popup-row"/>');

				var name = $('<span class="killtracker-popup-name text-font-normal font-color-label"/>');
				name.text(rows[i].name);

				var count = $('<span class="killtracker-popup-count text-font-normal font-color-value"/>');
				count.text(rows[i].count);

				row.append(name);
				row.append(count);
				list.append(row);
			}
		}
		container.append(list);
		popup.addPopupDialogContent(container);
	};

	// --- inject the trigger button each time the screen DOM is (re)built ---
	var _create = CharacterScreen.prototype.create;
	CharacterScreen.prototype.create = function (_parentDiv)
	{
		_create.call(this, _parentDiv);

		try
		{
			var self = this;
			var btn = $('<div class="killtracker-button text-font-normal font-color-brown"/>');
			btn.text("Kill Record");
			btn.click(function () {
				self.killtracker_openPanel();
			});
			this.mCharacterScreen.append(btn);
		}
		catch (e)
		{
			console.error("KillTracker: failed to add button - " + e);
		}
	};
})();
