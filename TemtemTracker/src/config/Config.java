package config;

import java.util.ArrayList;

public class Config {

	public double lumaChance;
	
	//Colours for the various detection spots
	public String spot1RGB;
	public String spot2RGB;
	public String spot3RGB;
	public String spot4RGB;
	public String spot5RGB;
	public String spot6RGB;
	
	// Maximum distance between the color I'm expecting and color from the screen
	public int maxAllowedColorDistance;
	
	public ArrayList<ScreenConfig> aspectRatios;

}
