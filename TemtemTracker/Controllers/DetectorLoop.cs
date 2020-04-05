﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TemtemTracker.Data;

namespace TemtemTracker.Controllers
{
    public class DetectorLoop
    {

        //A dictionary used to bind temtem process IDs to spot locations and dimensions of windows
        //Technically a user could spawn indefinite windows and keep closing them, filling this up
        //That would reduce performance as well as increase memory usage and constitutes a memory leak
        //However in practice this will never reach a critical amount of consumption and is irrelevant
        //Checking if processes in here exist would consume valuable CPU resources for a non-issue
        //Thus this isn't addressed
        Dictionary<uint, TemtemWindowData> temtemWindows;

        //Spots for detecting in-combat status
        //Detects the orange and blue switch Temtem and Wait buttons

        //Detection spot 1
        private readonly int spot1RGB;

        //Detection spot 2
        private readonly int spot2RGB;

        //Detection spot 3
        private readonly int spot3RGB;

        //Detection spot 4
        private readonly int spot4RGB;

        //Spots for detecting out-of combat status
        //Detects 2 spots along the blue border of the minimap

        //Detection spot 5
        private readonly int spot5RGB;

        //Detection spot 6
        private readonly int spot6RGB;

        //The name of the window in question
        private static readonly string WINDOW_NAME = "Temtem";

        //Maximum distance between the color I'm expecting and color from the screen
        private readonly int maxAllowedColorDistance;

        private readonly Config config;

        private readonly TemtemTableController tableController;
        private readonly OCRController ocrController;

        //For errors
        private readonly bool loadFailed = false;

        public DetectorLoop(Config config, TemtemTableController tableController, OCRController ocrController)
        {
            this.config = config;
            this.tableController = tableController;
            this.ocrController = ocrController;

            spot1RGB = ColorTranslator.FromHtml(config.spot1RGB).ToArgb();
            spot2RGB = ColorTranslator.FromHtml(config.spot2RGB).ToArgb();
            spot3RGB = ColorTranslator.FromHtml(config.spot3RGB).ToArgb();
            spot4RGB = ColorTranslator.FromHtml(config.spot4RGB).ToArgb();
            spot5RGB = ColorTranslator.FromHtml(config.spot5RGB).ToArgb();
            spot6RGB = ColorTranslator.FromHtml(config.spot6RGB).ToArgb();

            this.maxAllowedColorDistance = config.maxAllowedColorDistance;

            temtemWindows = new Dictionary<uint, TemtemWindowData>();            
        }

        public bool LoadFailed()
        {
            return loadFailed;
        }

        public void Detect()
        {
            //Pointer to the Temtem Window
            IntPtr temtemWindow;

            //Get the currently focused window
            IntPtr focused = User32.GetForegroundWindow();

            //If we're not focusing a window
            if (focused == IntPtr.Zero)
            {
                return;
            }
            //Check if the focused window is Temtem
            StringBuilder windowName = new StringBuilder(100);
            User32.GetWindowText(focused, windowName, 100);
            User32.GetWindowThreadProcessId(focused, out uint focusedWindowProcessID); //Inline variable declaration
            string focusedWindowProcessName = Process.GetProcessById((int)focusedWindowProcessID).ProcessName;
            if (windowName.ToString().Equals(WINDOW_NAME) && focusedWindowProcessName.Equals(WINDOW_NAME))
            {
                temtemWindow = focused;
                //Here we check if this is one of our already detected windows and if not we add it
                if (!temtemWindows.ContainsKey(focusedWindowProcessID))
                {
                    temtemWindows.Add(focusedWindowProcessID, new TemtemWindowData {
                        detectionSpots = new List<Point>(),
                        OCRViewports = new List<Rectangle>(),
                        gameWindowSize = new Size(0,0),
                        detectedBattle = false
                    });
                }
            }
            else
            {
                //We aren't in the right window
                return;
            }

            IntPtr hdcWindow = User32.GetDC(temtemWindow);

            User32.POINT lpPoint = new User32.POINT();

            User32.GetClientRect(temtemWindow, out User32.RECT bounds);
            User32.ClientToScreen(temtemWindow, ref lpPoint);

            ////Release the device context
            User32.ReleaseDC(temtemWindow, hdcWindow);

            Rectangle gameWindowRect = new Rectangle(lpPoint.X, lpPoint.Y,(bounds.right - bounds.left), (bounds.bottom - bounds.top));

            if (gameWindowRect.Height == 0 || gameWindowRect.Width == 0)
            {
                //If the rectangle is 0 high or wide, we've got a closing/closed window or an error
                return;
            }
            //If the game window dimensions have changed we need to recalculate all the spots.
            //This shouldn't happen often
            if (!gameWindowRect.Size.Equals(temtemWindows[focusedWindowProcessID].gameWindowSize))
            {
                temtemWindows[focusedWindowProcessID].gameWindowSize = gameWindowRect.Size;
                RecalculateDetectionElements(temtemWindows[focusedWindowProcessID], gameWindowRect);
            }

            

            if (temtemWindows[focusedWindowProcessID].detectedBattle == false)
            {
                //We'll only test for battle if we aren't in a battle
                List<Color> pixelColors = GetBattleDetectionColors(temtemWindows[focusedWindowProcessID], lpPoint);

                //We'll also get the OCR reading spots right here. It's a waste of resources if we aren't detecting battle, but is faster and more reliable when we do
                List<Bitmap> viewportImages = GetOCRViewportImages(lpPoint, temtemWindows[focusedWindowProcessID].OCRViewports);

                if (((ColorDistance(pixelColors[0], spot1RGB) < maxAllowedColorDistance &&
               ColorDistance(pixelColors[1], spot2RGB) < maxAllowedColorDistance) ||
               (ColorDistance(pixelColors[2], spot3RGB) < maxAllowedColorDistance &&
               ColorDistance(pixelColors[3], spot4RGB) < maxAllowedColorDistance)))
                {
                    temtemWindows[focusedWindowProcessID].detectedBattle = true;
                    //Do OCR operation. The OCR controller will dispose of the images so we're ok
                    List<string> results = ocrController.DoOCR(viewportImages);
                    results.ForEach(result => {
                        //Here we add the detected Temtem to the UI
                        tableController.AddTemtem(result);
                    });
                }
                
            }
            else if (temtemWindows[focusedWindowProcessID].detectedBattle == true)
            {
                //If we're in battle we'll test for being out of battle
                List<Color> pixelColors = GetOutOfBattleDetectionColors(temtemWindows[focusedWindowProcessID], lpPoint);

                if(ColorDistance(pixelColors[0], spot5RGB) < maxAllowedColorDistance &&
                    ColorDistance(pixelColors[1], spot6RGB) < maxAllowedColorDistance)
                {
                    //Set battle to false
                    temtemWindows[focusedWindowProcessID].detectedBattle = false;
                }
                
            }

        }

        private int ColorDistance(Color rgb1, int rgbInt)
        {
            byte a1 = rgb1.A;
            byte r1 = rgb1.R;
            byte g1 = rgb1.G;
            byte b1 = rgb1.B;

            Color rgb2 = Color.FromArgb(rgbInt);

            byte a2 = rgb2.A;
            byte r2 = rgb2.R;
            byte g2 = rgb2.G;
            byte b2 = rgb2.B;

            int distance = Math.Max((int)Math.Pow((r1 - r2), 2), (int)Math.Pow((r1 - r2 - a1 + a2), 2)) +
               Math.Max((int)Math.Pow((g1 - g2), 2), (int)Math.Pow((g1 - g2 - a1 + a2), 2)) +
               Math.Max((int)Math.Pow((b1 - b2), 2), (int)Math.Pow((b1 - b2 - a1 + a2), 2));

            return distance;
        }

        private List<Color> GetBattleDetectionColors(TemtemWindowData windowData, User32.POINT lpPoint)
        {
            List<Color> detectionSpotColors = new List<Color>();
            using (Bitmap pixel = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(pixel))
                {
                    for(int i = 0; i < 4; i++)
                    {
                        g.CopyFromScreen(new Point(lpPoint.X + windowData.detectionSpots[i].X, lpPoint.Y + windowData.detectionSpots[i].Y), new Point(0,0), pixel.Size);
                        detectionSpotColors.Add(pixel.GetPixel(0, 0));
                    }
                }
            }
            return detectionSpotColors;
        }

        private List<Color> GetOutOfBattleDetectionColors(TemtemWindowData windowData, User32.POINT lpPoint)
        {
            List<Color> detectionSpotColors = new List<Color>();
            using (Bitmap pixel = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(pixel))
                {
                    for(int i = 4; i < 6; i++)
                    {
                        g.CopyFromScreen(new Point(lpPoint.X + windowData.detectionSpots[i].X, lpPoint.Y + windowData.detectionSpots[i].Y), new Point(0, 0), pixel.Size);
                        detectionSpotColors.Add(pixel.GetPixel(0, 0));
                    }
                }
            }
            return detectionSpotColors;
        }

        private List<Bitmap> GetOCRViewportImages(User32.POINT lpPoint, List<Rectangle> OCRViewports)
        {
            List<Bitmap> viewportImages = new List<Bitmap>();
            OCRViewports.ForEach(viewport => {
                Bitmap viewportBitmap = new Bitmap(viewport.Width, viewport.Height, PixelFormat.Format32bppArgb);
                using(Graphics g = Graphics.FromImage(viewportBitmap))
                {
                    g.CopyFromScreen(lpPoint.X + viewport.X, lpPoint.Y + viewport.Y, 0, 0, viewport.Size);
                }
                viewportImages.Add(viewportBitmap);
            });
            return viewportImages;
        }

        private void RecalculateDetectionElements(TemtemWindowData windowData, Rectangle gameWindowRect)
        {
            //For loading the detection spots
            ScreenConfig screenConfig;

            screenConfig = ConfigLoader.GetConfigForAspectRatio(config, windowData.gameWindowSize);

            double spot1WidthPercentage = screenConfig.spot1WidthPercentage;
            double spot1HeightPercentage = screenConfig.spot1HeightPercentage;

            double spot2WidthPercentage = screenConfig.spot2WidthPercentage;
            double spot2HeightPercentage = screenConfig.spot2HeightPercentage;

            double spot3WidthPercentage = screenConfig.spot3WidthPercentage;
            double spot3HeightPercentage = screenConfig.spot3HeightPercentage;

            double spot4WidthPercentage = screenConfig.spot4WidthPercentage;
            double spot4HeightPercentage = screenConfig.spot4HeightPercentage;

            double spot5WidthPercentage = screenConfig.spot5WidthPercentage;
            double spot5HeightPercentage = screenConfig.spot5HeightPercentage;

            double spot6WidthPercentage = screenConfig.spot6WidthPercentage;
            double spot6HeightPercentage = screenConfig.spot6HeightPercentage;

            //Create points for the spots
            windowData.detectionSpots = new List<Point>
            {
                new Point((int)Math.Ceiling(spot1WidthPercentage * gameWindowRect.Width), (int)Math.Ceiling(spot1HeightPercentage * gameWindowRect.Height)),
                new Point((int)Math.Ceiling(spot2WidthPercentage * gameWindowRect.Width), (int)Math.Ceiling(spot2HeightPercentage * gameWindowRect.Height)),
                new Point((int)Math.Ceiling(spot3WidthPercentage * gameWindowRect.Width), (int)Math.Ceiling(spot3HeightPercentage * gameWindowRect.Height)),
                new Point((int)Math.Ceiling(spot4WidthPercentage * gameWindowRect.Width), (int)Math.Ceiling(spot4HeightPercentage * gameWindowRect.Height)),
                new Point((int)Math.Ceiling(spot5WidthPercentage * gameWindowRect.Width), (int)Math.Ceiling(spot5HeightPercentage * gameWindowRect.Height)),
                new Point((int)Math.Ceiling(spot6WidthPercentage * gameWindowRect.Width), (int)Math.Ceiling(spot6HeightPercentage * gameWindowRect.Height))
            };

            //Calculate frame locations and widths for the new size/aspect ratio
            double frame1PercentageLeft = screenConfig.frame1PercentageLeft;
            double frame1PercentageTop = screenConfig.frame1PercentageTop;

            double frame2PercentageLeft = screenConfig.frame2PercentageLeft;
            double frame2PercentageTop = screenConfig.frame2PercentageTop;

            double frameWidthPercentage = screenConfig.frameWidthPercentage;
            double frameHeightPercentage = screenConfig.frameHeightPercentage;

            Size frameSize = new Size((int)Math.Ceiling(gameWindowRect.Size.Width * frameWidthPercentage),
                    (int)Math.Ceiling(gameWindowRect.Size.Height * frameHeightPercentage));

            Point frame1Location = new Point((int)Math.Ceiling(gameWindowRect.Size.Width * frame1PercentageLeft),
            (int)Math.Ceiling(gameWindowRect.Size.Height * frame1PercentageTop));
            Point frame2Location = new Point((int)Math.Ceiling(gameWindowRect.Size.Width * frame2PercentageLeft),
            (int)Math.Ceiling(gameWindowRect.Size.Height * frame2PercentageTop));
            //Create a new list of OCR viewports
            windowData.OCRViewports = new List<Rectangle>
            {
                new Rectangle(frame1Location, frameSize),
                new Rectangle(frame2Location, frameSize)
            };
        }

    }
}
