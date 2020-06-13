using System;
using System.ComponentModel;
using System.Diagnostics;
using RArcher.Phone.Toolkit;
using RArcher.Phone.Toolkit.Common;
using RArcher.Phone.Toolkit.Logging;

namespace RoundUp.ViewModel
{
    public class SettingsViewModel : ModelBase, ISettingsViewModel, INotifyPropertyChanged
    {
        // Preference settings ------------------------------------------------

        /// <summary>The alias used in the text of an invite</summary>
        [Dump]
        [AutoSetting(defaultValue: "")]
        public string SettingsAlias
        {
            get
            {
                if(string.IsNullOrEmpty(_settingsAlias)) _settingsAlias = DeviceHelper.DeviceName();
                return _settingsAlias;
            }
            set
            {
                _settingsAlias = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Settings. Always show directions to the RoundUp point when the device is an inviter</summary>
        [Dump]
        [AutoSetting(defaultValue: false)]
        public bool SettingsShowDirectionsInviter
        {
            get { return _settingsShowDirectionsInviter; }
            set { _settingsShowDirectionsInviter = value; OnPropertyChanged(); }
        }

        /// <summary>Settings. Always show directions to the RoundUp point when the device is an invitee</summary>
        [Dump]
        [AutoSetting(defaultValue: true)]
        public bool SettingsShowDirectionsInvitee
        {
            get { return _settingsShowDirectionsInvitee; }
            set { _settingsShowDirectionsInvitee = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Settings. Allows the user to completely turn off location services. 
        /// This is a requirement to pass the Windows Phone Store certification process
        /// </summary>
        [Dump]
        [AutoSetting(defaultValue: true)]
        public bool SettingsLocationServicesOn
        {
            get { return _settingsLocationServicesOn; }
            set { _settingsLocationServicesOn = value; OnPropertyChanged(); }
        }

        /// <summary>Settings. Allows the user to completely turn off MPNS. A requirement to pass the Windows Phone Store certification process</summary>
        [Dump]
        [AutoSetting(defaultValue: true)]
        public bool SettingsMpnsOn
        {
            get { return _settingsMpnsOn; }
            set { _settingsMpnsOn = value; OnPropertyChanged(); }
        }

        /// <summary>Get/set location position tracking in the Location service</summary>
        [Dump]
        [AutoSetting(defaultValue: true)]
        public bool SettingsTrackCurrentLocation
        {
            get { return _settingsTrackCurrentLocation; }
            set { _settingsTrackCurrentLocation = value; OnPropertyChanged(); }
        }

        /// <summary>Settings (no UI control). Store a boolean value indicating if we've asked the user if it's OK to use the MPNS. A requirement to pass the Windows Phone Store certification process</summary>
        [Dump]
        [AutoSetting(defaultValue: false)]
        public bool SettingsUserPermissionMpns
        {
            get { return _settingsUserPermissionMpns; }
            set { _settingsUserPermissionMpns = value; OnPropertyChanged();}
        }
        
        /// <summary>
        /// Settings (no UI control). Store a boolean value indicating if we've asked the user if it's OK to 
        /// use location services. A requirement to pass the Windows Phone Store certification process
        /// </summary>
        [Dump]
        [AutoSetting(defaultValue: false)]
        public bool SettingsUserPermissionLocationServices
        {
            get { return _settingsUserPermissionLocationServices; }
            set { _settingsUserPermissionLocationServices = value; OnPropertyChanged();}
        }

        /// <summary>
        /// When restarting the app, if a greater time period than this (in minutes) has elapsed since the app last ran, 
        /// we won't attempt to restore the previous session (if any)
        /// </summary>
        [Dump]
        [AutoSetting(defaultValue: 5)]
        public int SettingsSessionDeadTimeout
        {
            get { return _settingsSessionDeadTimeout; }
            set { _settingsSessionDeadTimeout = value; OnPropertyChanged(); }
        }

        /// <summary>Allows the user to turn off the ability to run under the lock screen</summary>
        [Dump]
        [AutoSetting(defaultValue: true)]
        public bool SettingsRunUnderLockScreenOn
        {
            get { return _settingsRunUnderLockScreenOn; }
            set { _settingsRunUnderLockScreenOn = value; OnPropertyChanged();}
        }

        /// <summary>Allows the user to turn on/off continuous background execution (CBE)</summary>
        [Dump]
        [AutoSetting(defaultValue: true)]
        public bool SettingsBackgroundExecutionOn
        {
            get { return _settingsBackgroundExecutionOn; }
            set { _settingsBackgroundExecutionOn = value; OnPropertyChanged();}
        }

        /// <summary>Allows the user to turn on/off toast notifications</summary>
        [Dump]
        [AutoSetting(defaultValue: true)]
        public bool SettingsToastOn
        {
            get { return _settingsToastOn; }
            set { _settingsToastOn = value; OnPropertyChanged(); }
        }

        // Privates -----------------------------------------------------------
        
        private bool _settingsShowDirectionsInviter;
        private bool _settingsShowDirectionsInvitee;
        private bool _settingsLocationServicesOn;
        private bool _settingsTrackCurrentLocation;
        private bool _settingsUserPermissionLocationServices;
        private bool _settingsRunUnderLockScreenOn;
        private bool _settingsBackgroundExecutionOn;
        private bool _settingsMpnsOn;
        private bool _settingsUserPermissionMpns;
        private bool _settingsToastOn;
        private int _settingsSessionDeadTimeout;
        private string _settingsAlias;

        // Methods ------------------------------------------------------------

        public SettingsViewModel() : base("MainViewModel")
        {
            // Note that we're using the save/restore state id of "MainViewModel".
            // This allows us to share settings with the main view model
        }

        /// <summary>Save state to persistent (isolated) storage</summary>
        public override void SaveState()
        {
            Logger.Log("AboutViewModel.SaveState");

            SaveAutoSetting();  // Save all settings marked with the [AutoSetting] attribute
            //SaveAutoState();  // Save all properties marked with the [AutoState] attribute
        }

        /// <summary>Restore state from to persistent (isolated) storage</summary>
        public override void RestoreState()
        {
            Logger.Log("AboutViewModel.RestoreState");

            try 
            { 
                RestoreAutoSetting();  // Restore all SETTINGS marked with the [AutoSetting] attribute
                //RestoreState();
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }
    }
}