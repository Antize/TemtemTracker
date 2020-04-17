﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;
using TemtemTracker.Data;

namespace TemtemTracker.Controllers
{
    public class TimerController
    {

        
        private static readonly int TIME_TRACKER_INTERVAL = 1000;
        private static readonly int INACTIVITY_CHECK_INTERVAL = 1000;
        private readonly int detectionLoopInterval;

        private readonly System.Timers.Timer detectionLoopTimer;
        private readonly System.Timers.Timer timeTrackerTimer;
        private readonly System.Timers.Timer autosaveTimer;
        private readonly System.Timers.Timer inactivityTimer;

        private readonly TemtemTableController tableController;
        private readonly DetectorLoop detectorLoop;
        private readonly SettingsController settingsController;

        private bool disableDetectionOnTimerPause;

        private readonly UserSettings userSettings;


        //To prevent reentrancy in the detector loop
        int _TimerLock = 0;
        int _AutosaveLock = 0;

        public TimerController(TemtemTableController tableController, DetectorLoop detectorLoop, Config config, UserSettings userSettings, SettingsController settingsController)
        {
            this.tableController = tableController;
            this.detectorLoop = detectorLoop;
            this.detectionLoopInterval = config.detectionLoopInterval;
            this.disableDetectionOnTimerPause = userSettings.disableDetectionWhileTimerPaused;
            this.userSettings = userSettings;
            this.settingsController = settingsController;
            settingsController.TimerPausedToggled += ToggleTimeTrackerTimerPaused;

            settingsController.DetectionDisabledChanged += SetDisableDetectionOnTimerPause;
            settingsController.InactivityTimerEnabledChanged += SetInactivityTimerEnabled;

            detectionLoopTimer = new System.Timers.Timer(detectionLoopInterval);
            timeTrackerTimer = new System.Timers.Timer(TIME_TRACKER_INTERVAL);
            autosaveTimer = new System.Timers.Timer();
            inactivityTimer = new System.Timers.Timer(INACTIVITY_CHECK_INTERVAL);

            //Set the interval for the autosave timer from the user settings
            //The interval is in minutes, the timer accepts miliseconds, the function converts
            SetAutosaveTimeInterval(null, userSettings.autosaveInterval);
            settingsController.AutosaveIntervalChanged += SetAutosaveTimeInterval;


            detectionLoopTimer.Elapsed += DetectionLoopListener;
            timeTrackerTimer.Elapsed += TimeTrackerListener;
            autosaveTimer.Elapsed += AutosaveListener;
            inactivityTimer.Elapsed += InactivityListener;

            detectionLoopTimer.AutoReset = true;
            timeTrackerTimer.AutoReset = true;
            autosaveTimer.AutoReset = true;
            inactivityTimer.AutoReset = true;

        }

        public void StartTimers()
        {
            detectionLoopTimer.Start();
            timeTrackerTimer.Start();
            autosaveTimer.Start();
            if (userSettings.pauseWhenInactive == true)
            {
                //We only want to enable this if the setting states it should be enabled
                inactivityTimer.Start();
            }
            
        }

        public void ToggleTimeTrackerTimerPaused(object sender, bool timerEnabled)
        {
            timeTrackerTimer.Enabled = timerEnabled;
            if (disableDetectionOnTimerPause)
            {
                //If we want to disable detection when the timer is paused
                if (timeTrackerTimer.Enabled)
                {
                    //If the timer was unpaused
                    detectionLoopTimer.Start();
                }
                else
                {
                    //The timer was paused
                    detectionLoopTimer.Stop();
                }
            }
        }

        public void DisposeTimers()
        {
            detectionLoopTimer.Stop();
            detectionLoopTimer.Dispose();
            timeTrackerTimer.Stop();
            timeTrackerTimer.Dispose();
            autosaveTimer.Stop();
            autosaveTimer.Dispose();
            inactivityTimer.Stop();
            inactivityTimer.Dispose();
        }

        public void SetAutosaveTimeInterval(object sender, int intervalMinutes)
        {
            autosaveTimer.Interval = intervalMinutes * 60000;
        }

        public void SetInactivityTimerEnabled(object sender, bool enabled)
        {
            inactivityTimer.Enabled = enabled;
        }

        public void SetDisableDetectionOnTimerPause(object sender, bool detectionDisabled)
        {
            disableDetectionOnTimerPause = detectionDisabled;
            if(!detectionDisabled && !detectionLoopTimer.Enabled)
            {
                //Detection isn't disabled anymore, but the timer is still stopped. Restart it
                detectionLoopTimer.Start();
            }
            if(detectionDisabled && !timeTrackerTimer.Enabled)
            {
                //We've disabled detection while the timer is stopped
                //and the timer IS stopped. Stop the detection loop
                detectionLoopTimer.Stop();
            }
        }

        private void DetectionLoopListener(Object source, System.Timers.ElapsedEventArgs e)
        {

            if (Interlocked.CompareExchange(ref _TimerLock, 1, 0) != 0) return;
            try
            {
                detectorLoop.Detect();
            }
            finally
            {
                Interlocked.Exchange(ref _TimerLock, 0);
            }

        }

        private void TimeTrackerListener(Object source, System.Timers.ElapsedEventArgs e)
        {
            tableController.IncrementTimer();
        }

        private void AutosaveListener(object source, System.Timers.ElapsedEventArgs e)
        {
            if (Interlocked.CompareExchange(ref _AutosaveLock, 1, 0) != 0) return;
            try
            {
                tableController.SaveTable();
            }
            finally
            {
                Interlocked.Exchange(ref _AutosaveLock, 0);
            }
        }

        private void InactivityListener(object sender, ElapsedEventArgs e)
        {
            //TO-DO: Figure out how to evaluate inactivity
            DateTime currentTime = DateTime.Now;
            DateTime lastChange = tableController.GetLastChangeTime();
            if (currentTime.Subtract(lastChange).TotalMinutes > userSettings.inactivityTreshold)
            {
                if (timeTrackerTimer.Enabled)
                {
                    settingsController.StopTimer();
                }
            }
        }
    }
}
