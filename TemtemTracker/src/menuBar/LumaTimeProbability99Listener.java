package menuBar;

import java.awt.event.ActionEvent;
import java.awt.event.ActionListener;

import config.UserSettings;
import temtemTableUI.TemtemTableUI;

public class LumaTimeProbability99Listener implements ActionListener {

	private TemtemTableUI tableUI;
	private UserSettings userSettings;

	public LumaTimeProbability99Listener(TemtemTableUI tableUI, UserSettings userSettings) {
		this.tableUI = tableUI;
		this.userSettings = userSettings;
	}

	@Override
	public void actionPerformed(ActionEvent e) {
		userSettings.timeToLumaProbability = 0.9999;
		tableUI.recalculateLumaTimes();
	}

}
