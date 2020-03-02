package battleDetection;

import java.awt.AWTException;
import java.awt.Dimension;
import java.awt.Rectangle;
import java.awt.Robot;
import java.awt.image.BufferedImage;
import java.util.ArrayList;
import java.util.TimerTask;
import java.util.concurrent.atomic.AtomicBoolean;


import OCR.OCR;
import config.Config;
import config.ConfigLoader;
import config.ScreenConfig;
import temtemTable.TemtemTable;
import windowFinder.WindowFinder;


public class DetectorLoop extends TimerTask{
	
	//Spots for detecting in-combat status
	//Detects the orange and blue switch Temtem and Wait buttons
	
	//The colors could just as well be int since the bit values are important and not the actual values of the integers
	//But Java bitches when parsing something that would overflow into negative integers from a hex string
	//So everything is unnecessarily long. This has no impact on the performance. It's just annoying.
	
	//Detection spot 1
	private double spot1WidthPercentage;
	private double spot1HeightPercentage;
	private long spot1RGB;
	
	//Detection spot 2
	private double spot2WidthPercentage;
	private double spot2HeightPercentage;
	private long spot2RGB;
	
	//Detection spot 3
	private double spot3WidthPercentage;
	private double spot3HeightPercentage;
	private long spot3RGB;
	
	//Detection spot 4
	private double spot4WidthPercentage;
	private double spot4HeightPercentage;
	private long spot4RGB;
	
	//Spots for detecting out-of combat status
	//Detects 2 spots along the blue border of the minimap
	
	//Detection spot 5
	private double spot5WidthPercentage;
	private double spot5HeightPercentage;
	private long spot5RGB;
	
	//Detection spot 6
	private double spot6WidthPercentage;
	private double spot6HeightPercentage;
	private long spot6RGB;
	
	//Used to check if the window size changed
	Dimension gameWindowSize = new Dimension(0,0);
	
	//Maximum distance between the color I'm expecting and color from the screen
	private int maxAllowedColorDistance ;
	
	Config config;
	
	private AtomicBoolean detectedBattle;
	
	private TemtemTable table;
	
	OCR ocr;
	
	public DetectorLoop(Config config, Dimension size, OCR ocr, TemtemTable table) {
		
		this.config = config;
		
		this.spot1RGB = Long.decode(config.spot1RGB);
		this.spot2RGB = Long.decode(config.spot2RGB);
		this.spot3RGB = Long.decode(config.spot3RGB);
		this.spot4RGB = Long.decode(config.spot4RGB);
		this.spot5RGB = Long.decode(config.spot5RGB);
		this.spot6RGB = Long.decode(config.spot6RGB);
		
		this.maxAllowedColorDistance = config.maxAllowedColorDistance;
		
		
		detectedBattle = new AtomicBoolean(false);
		
		this.ocr = ocr;
		
		this.table = table;
	}

	@Override
	public void run() {
		try {
			
			Robot robot = new Robot();
			Rectangle gameWindow = new Rectangle();
			
			//For loading the detection spots
			ScreenConfig screenConfig;
			
			//Local game window size. Is compared to the one stored in the class instance to verify if the size changed
			Dimension gameWindowSize;
			
			//The actual screenshot
			BufferedImage screenShot;
			
			gameWindow = WindowFinder.findTemtemWindow(config);
			if(gameWindow == null) {
				//Failed to find window, return
				return;
			}
			
			gameWindowSize = new Dimension(gameWindow.width, gameWindow.height);
			
			if(gameWindowSize != this.gameWindowSize) {
				this.gameWindowSize = gameWindowSize;
				//We haven't found an aspect ratio yet, or the window aspect ratio changed
				screenConfig = ConfigLoader.getConfigForAspectRatio(config.aspectRatios, gameWindowSize);
				
				this.spot1WidthPercentage = screenConfig.spot1WidthPercentage;
				this.spot1HeightPercentage = screenConfig.spot1HeightPercentage;
				
				this.spot2WidthPercentage = screenConfig.spot2WidthPercentage;
				this.spot2HeightPercentage = screenConfig.spot2HeightPercentage;
				
				this.spot3WidthPercentage = screenConfig.spot3WidthPercentage;
				this.spot3HeightPercentage = screenConfig.spot3HeightPercentage;
				
				this.spot4WidthPercentage = screenConfig.spot4WidthPercentage;
				this.spot4HeightPercentage = screenConfig.spot4HeightPercentage;
				
				this.spot5WidthPercentage = screenConfig.spot5WidthPercentage;
				this.spot5HeightPercentage = screenConfig.spot5HeightPercentage;
				
				this.spot6WidthPercentage = screenConfig.spot6WidthPercentage;
				this.spot6HeightPercentage = screenConfig.spot6HeightPercentage;
			}
			
			//Take a screenshot of the actual window region
			screenShot = robot.createScreenCapture(gameWindow);	
		
			//In-battle detection
			BufferedImage pixel1 = screenShot.getSubimage((int)Math.ceil(spot1WidthPercentage*screenShot.getWidth()), (int)Math.ceil(spot1HeightPercentage*screenShot.getHeight()), 1, 1);
			BufferedImage pixel2 = screenShot.getSubimage((int)Math.ceil(spot2WidthPercentage*screenShot.getWidth()), (int)Math.ceil(spot2HeightPercentage*screenShot.getHeight()), 1, 1);
			BufferedImage pixel3 = screenShot.getSubimage((int)Math.ceil(spot3WidthPercentage*screenShot.getWidth()), (int)Math.ceil(spot3HeightPercentage*screenShot.getHeight()), 1, 1);
			BufferedImage pixel4 = screenShot.getSubimage((int)Math.ceil(spot4WidthPercentage*screenShot.getWidth()), (int)Math.ceil(spot4HeightPercentage*screenShot.getHeight()), 1, 1);
			
			//Out-of-battle detection
			BufferedImage pixel5 = screenShot.getSubimage((int)Math.ceil(spot5WidthPercentage*screenShot.getWidth()), (int)Math.ceil(spot5HeightPercentage*screenShot.getHeight()), 1, 1);
			BufferedImage pixel6 = screenShot.getSubimage((int)Math.ceil(spot6WidthPercentage*screenShot.getWidth()), (int)Math.ceil(spot6HeightPercentage*screenShot.getHeight()), 1, 1);
			
			if(detectedBattle.get() == false &&
			   colorDistance(pixel1.getRGB(0, 0), spot1RGB)<maxAllowedColorDistance &&
			   colorDistance(pixel2.getRGB(0, 0), spot2RGB)<maxAllowedColorDistance &&
			   colorDistance(pixel3.getRGB(0, 0), spot3RGB)<maxAllowedColorDistance &&
			   colorDistance(pixel4.getRGB(0, 0), spot4RGB)<maxAllowedColorDistance) {
					
					detectedBattle.set(true);
					System.out.println("Detected battle!");
					ArrayList<String> results = ocr.doOCR(config);
					if(results.size()>0) {
						results.forEach(result->{
							table.addTemtem(result);
						});
					}
					
			}
			else if(detectedBattle.get() == true &&
					colorDistance(pixel5.getRGB(0, 0), spot5RGB)<maxAllowedColorDistance &&
					colorDistance(pixel6.getRGB(0, 0), spot6RGB)<maxAllowedColorDistance)
			{
				detectedBattle.set(false);
				System.out.println("Detected out-of-battle!");
			}
			
		} catch (AWTException e) {
			e.printStackTrace();
		} 
	}
	
	private int colorDistance(int rgb1, long rgb2) {
		int a1 = (0xFF & (rgb1>>24));
		int r1 = (0xFF & (rgb1>>16));
		int g1 = (0xFF & (rgb1>>8));
		int b1 = (0xFF & rgb1);
		
		int a2 = (int)(0xFF & (rgb2>>24));
		int r2 = (int)(0xFF & (rgb2>>16));
		int g2 = (int)(0xFF & (rgb2>>8));
		int b2 = (int)(0xFF & rgb2);
		
		int distance = Math.max((int) Math.pow((r1-r2), 2), (int) Math.pow((r1-r2 - a1+a2),2)) +
			   Math.max((int) Math.pow((g1-g2), 2), (int) Math.pow((g1-g2 - a1+a2),2)) +
			   Math.max((int) Math.pow((b1-b2), 2), (int) Math.pow((b1-b2 - a1+a2),2));
		
		//System.out.println("Color distance: " + distance);
		return distance;
	}

}
