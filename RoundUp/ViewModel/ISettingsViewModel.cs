namespace RoundUp.ViewModel
{
    public interface ISettingsViewModel
    {
        // Preference settings ------------------------------------------------

        /// <summary>Settings. User's name/alias</summary>
        string SettingsAlias { get; set; }  

        /// <summary>Settings. Always show directions to the RoundUp point when the device is an inviter</summary>
        bool SettingsShowDirectionsInviter { get; set; } 
        
        /// <summary>Settings. Always show directions to the RoundUp point when the device is an invitee</summary>
        bool SettingsShowDirectionsInvitee { get; set; }

        /// <summary>Settings. Allows the user to completely turn off location services. A requirement to pass the Windows Phone Store certification process</summary>
        bool SettingsLocationServicesOn { get; set; }

        /// <summary>Settings. Allows the user to completely turn off MPNS. A requirement to pass the Windows Phone Store certification process</summary>
        bool SettingsMpnsOn { get; set; } 
        
        /// <summary>Settings. Allows the user to turn off the ability to run under the lock screen. A requirement to pass the Windows Phone Store certification process</summary>
        bool SettingsRunUnderLockScreenOn { get; set; }

        /// <summary>Turn on/off location position tracking in the Location service</summary>
        bool SettingsTrackCurrentLocation { get; set; } 
        
        /// <summary>Settings. Allows the user to turn on/off continuous background execution (CBE)</summary>
        bool SettingsBackgroundExecutionOn { get; set; } 
        
        /// <summary>Settings (no UI control). Store a boolean value indicating if we've asked the user if it's OK to use location services. A requirement to pass the Windows Phone Store certification process</summary>
        bool SettingsUserPermissionLocationServices { get; set; }

        /// <summary>Settings (no UI control). Store a boolean value indicating if we've asked the user if it's OK to use the MPNS. A requirement to pass the Windows Phone Store certification process</summary>
        bool SettingsUserPermissionMpns { get; set; }

        /// <summary>Allows the user to turn on/off toast notifications</summary>
        bool SettingsToastOn { get; set; }

        /// <summary>When restarting the app, if a greater time period than this has elapsed since the app last ran, we won't attempt to restore the previous session (if any)</summary>
        int SettingsSessionDeadTimeout { get; set; }        

        // Methods ------------------------------------------------------------

        /// <summary>Save state to persistent (isolated) storage</summary>
        void SaveState();

        /// <summary>Restore state from to persistent (isolated) storage</summary>
        void RestoreState(); 
    }
}