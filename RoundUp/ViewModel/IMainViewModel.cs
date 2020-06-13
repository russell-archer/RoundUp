using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Device.Location;
using Microsoft.Phone.Maps.Controls;
using RArcher.Phone.Toolkit.Common;
using RArcher.Phone.Toolkit.Location.Common;
using RArcher.Phone.Toolkit.Store.Enum;
using RoundUp.Common;
using RoundUp.Model;
using RoundUp.Enum;

namespace RoundUp.ViewModel
{
    /// <summary>Defines the view model class for the main view</summary>
    public interface IMainViewModel
    {
        // Events -------------------------------------------------------------

        event PropertyChangedEventHandler PropertyChanged;
        event EventHandler<MapRouteEventArgs> MapRouteChanged;
        event EventHandler<EventArgs> StateRestored;
        event EventHandler<EventArgs> ShareUiVisibilityChanged;
        event EventHandler<LocationUpdateEventArgs> CurrentLocationChanged;
        event EventHandler<LocationUpdateEventArgs> InviteeSelected;
            
        // Properties ---------------------------------------------------------

        /// <summary>List of live invitees (if any) taking part in a session. The map will bind to this collection to show location markers for all invitees</summary>
        ObservableCollection<InviteeLocationMarker> Invitees { get; set; }

        /// <summary>List of text instructions for walking or driving to the RoundUp location</summary>
        ObservableCollection<RouteInstruction> RouteInstructions { get; set; }

        /// <summary>True if the MPNS service is connected (we have a valid channel uri), false otherwise</summary>
        bool MpnsServiceConnected { get; set; } 

        /// <summary>True if the location service is connected and location data is available, false otherwise</summary>
        bool LocationServiceConnected { get; set; }

        /// <summary>True if the RoundUp (Windows Azure Mobile Services) service is connected, false otherwise</summary>
        bool RoundUpServiceConnected { get; set; } 

        /// <summary>True if the device is an inviter, false otherwise</summary>
        bool IsInviter { get; set; } 

        /// <summary>True if the device is an invitee, false otherwise</summary>
        bool IsInvitee { get; set; }

        /// <summary>True if the location service is trying to connect, false otherwise</summary>
        bool WaitingForLocationServiceToConnect { get; }
        
        /// <summary>True if all services are connected, false otherwise</summary>
        bool AllServicesConnected { get; } 
        
        /// <summary>True if the user is an inviter and there's an active session, false otherwise</summary>
        bool IsSessionCancelable { get; }  
        
        /// <summary>True if the user is an invitee and has previously accepted an invite to a session, false otherwise</summary>
        bool IsInviteCancelable { get; }
        
        /// <summary>True if we are running under the lock screen, false otherwise</summary>
        bool IsRunningUnderLockScreen { get; set; }

        /// <summary>True if the user is walking to the roundup location, false if they're driving (or the session type hasn't been set)</summary>
        bool IsWalking { get; set; }
        
        /// <summary>True if the user has started a session (they're an inviter), or the user has accepted an invitee, false otherwise</summary>
        bool SessionTypeHasBeenSet { get; set; } 
        
        /// <summary>True if the invitee accept panel is to be displayed, false otherwise</summary>
        bool ShowAcceptInviteUI { get; set; }
        
        /// <summary>True if the share panel is displayed, false otherwise</summary>
        bool ShowShareUI { get; set; }

        /// <summary>True if the directions panel is displayed, false otherwise</summary>
        bool ShowDirectionsUI { get; set; }

        /// <summary>True if the invitees panel is displayed, false otherwise</summary>
        bool ShowInviteesUI { get; set; }

        /// <summary>True if a geric progress bar should be displayed, false otherwise</summary>
        bool ShowProgressBar { get; set; }

        /// <summary>True if the view's map should display the RoundUp location pushpin, false otherwise</summary>
        bool ShowRoundUpPointLocation { get; set; }

        /// <summary>True if we will allow the user to change the RoundUp location point by tapping the map control on the view, false otherwise</summary>
        bool AllowRoundUpPointLocationChange { get; set; }
               
        /// <summary>True if we're running inside the WP8 emulator</summary>
        bool IsEmulator { get; } 
        
        /// <summary>True if we're compiled for debug</summary>
        bool IsDebug { get; }

        /// <summary>True if we're running in trial mode, false otherwise</summary>
        bool IsTrialMode { get; }

        /// <summary>True if the app was launched with our custom uri association and there are valid start-up params available</summary>
        bool HasStartUpParams { get; set; }

        /// <summary>True if state has been restored (or if it's not neccessary to restore state), false otherwise)</summary>
        bool StateHasBeenRestored { get; set; }

        /// <summary>True if the map is showing landmarks</summary>
        bool MapLandmarksOn { get; set; }

        /// <summary>True if the map is showing pedestrian features</summary>
        bool MapPedestrianFeaturesOn { get; set; }

        /// <summary>True if we're showing the map control panel</summary>
        bool ShowMapControlPanel { get; set; }

        /// <summary>True if we're showing the menu bar</summary>
        bool ShowMenuBar { get; set; }

        /// <summary>True if we're not waiting for an op to complete. This value is used to disable menu items and buttons</summary>
        bool OperationHasCompleted { get; set; }

        /// <summary>True if SessionStatus is either SessionStarted or SessionActive</summary>
        bool IsLiveSession { get; }

        /// <summary>True if location is shared by the creation of an invite code. If false, a Bing map uri will be generated</summary>
        bool ShareLocationByInviteCode { get; set; }

        /// <summary>True if the roundup logo should be displayed on top of the map, false otherwise</summary>
        bool ShowMapLogo { get; set; }

        /// <summary>Identifies which app bar should be displayed</summary>
        int AppBarIndexSelector { get; } 
        
        /// <summary>The Session table row id of the session</summary>
        int SessionId { get; set; } 
        
        /// <summary>The Invitee table row id</summary>
        int InviteeId { get; set; } 
        
        /// <summary>The 8-character string the acts as a short identifier of the device</summary>
        string ShortDeviceId { get; set; } 

        /// <summary>The 4-character string the acts as a short identifier of the inviter's device</summary>
        string InviterShortDeviceId { get; set; }  
        
        /// <summary>The message used by an inviter to invite others to join the session</summary>
        string InviteCodeText { get; set; }

        /// <summary>Text to display in the progress bar</summary>
        string ProgressBarText { get; set; }

        /// <summary>Text that can be used to denote the license mode (e.g. "Trial ({0} days left)", "Full", "Trial Expired")</summary>
        string LicenseModeText { get; }

        /// <summary>Returns the app's version as a string, suitable for display in the about screen</summary>
        string VersionText { get; }
        
        /// <summary>The map's zoom (1..20)</summary>
        double MapZoomLevel { get; set; }

        /// <summary>The map's pitch (0..75)</summary>
        double MapPitch { get; set; }

        /// <summary>The map's heading (0..360)</summary>
        double MapHeading { get; set; }

        /// <summary>How the user wants to share their location</summary>
        ShareLocationOption ShareLocationBy { get; set; }

        /// <summary>The map's mode (road, aerial, terrain, hybrid)</summary>
        MapCartographicMode MapMode { get; set; }

        /// <summary>The map's color mode (light/dark)</summary>
        MapColorMode MapDayNightColorMode { get; set; }
        
        /// <summary>The device's current location</summary>
        GeoCoordinate CurrentLocation { get; set; }  
        
        /// <summary>Encapsulated by RoundUpPointInfo (re-surfaced here as a property for convenience. The location at which the inviter and invitee(s) will RoundUp (note that for an inviter, RoundUpPointLocation == CurrentLocation if ShowRoundUpPointLocation is false)</summary>
        GeoCoordinate RoundUpPointLocation { get; set; } 
        
        /// <summary>The coordinates of the map's center spot (note that MapCenterLocation may not == CurrentLocation)</summary>
        GeoCoordinate MapCenterLocation { get; set; }

        /// <summary>The new roundup point requested by the inviter</summary>
        GeoCoordinate RequestedNewRoundUpPoint { get; set; }
        
        /// <summary>Encapsulates data related to the roundup (meet-up) location, including a GeoCoordinate for the location, address, distance</summary>
        RoundUpPoint RoundUpPointInfo { get; set; }
           
        /// <summary>The InviteCode (if any) used by the invitee to join a session</summary>
        InviteCode CurrentInviteCode { get; set; }
        
        /// <summary>The current status of the session</summary>
        SessionStatusValue SessionStatus { get; set; } 
        
        /// <summary>The current status of an invitee</summary>
        InviteeStatusValue InviteeStatus { get; set; } 

        /// <summary>Holds the license mode for the app (Trial, Full, etc.)</summary>
        LicenseMode License { get; }
        
        // Preference settings ------------------------------------------------

        /// <summary>Settings. User's name/alias</summary>
        string SettingsAlias { get; set; }  

        /// <summary>Settings. Always show directions to the RoundUp point when the device is an inviter</summary>
        bool SettingsShowDirectionsInviter { get; set; } 
        
        /// <summary>Settings. Always show directions to the RoundUp point when the device is an invitee</summary>
        bool SettingsShowDirectionsInvitee { get; set; }

        /// <summary>The DateTime when we were last alive (i.e. when we were last deactivated)</summary>
        DateTime SettingsLastAliveTime { get; set; }
        
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

        // Commands -----------------------------------------------------------

        /// <summary>Inviter. Starts a new session</summary>
        RelayCommand InviteCommand { get; set; } 
        
        /// <summary>Inviter. Used by an inviter to cancel a session</summary>
        RelayCommand CancelSessionCommand { get; set; } 
        
        /// <summary>Inviter. Menu item to send an SMS invite</summary>
        RelayCommand SendInviteSmsCommand { get; set; } 
        
        /// <summary>Inviter. Menu item to send an email invite</summary>
        RelayCommand SendInviteEmailCommand { get; set; } 
        
        /// <summary>Inviter. Menu item which starts the process of setting a new RoundUp point (shows a prompt to the user to tap the map to set the new location)</summary>
        RelayCommand StartSetNewRoundUpPointCommand { get; set; }
        
        /// <summary>Inviter. Invoked from the View's code-behind (Map.Tapped event) to set the map point for the new RoundUp point. We *ignore* all exec calls until the user starts the process byt selecting the StartSetNewRoundUpPointCommand menu. This prevents map taps in normal usage from accidentally setting a new RoundUp point</summary>
        RelayCommand SetNewRoundUpPointCommand { get; set; } 
        
        /// <summary>Invitee. Starts the process of accepting an invite. Prompts for invite code</summary>
        RelayCommand StartAcceptInviteCommand { get; set; } 
        
        /// <summary>Invitee. Completes the process of accepting an invite. Inserts a row in the Invitee Azure table</summary>
        RelayCommand CompleteAcceptInviteCommand { get; set; } 
        
        /// <summary>Invitee. Allows the user to cancel the start of accepting an invite (cancels the invite code prompt)</summary>
        RelayCommand CancelAcceptInviteCommand { get; set; } 
              
        /// <summary>Invitee. Cancel a previously accepted invitation. A notification is sent to the inviter</summary>
        RelayCommand CancelAcceptedInvitationCommand { get; set; } 

        /// <summary>Inviter and Invitee. Ad-hoc refresh of current device position. Will update the CurrentLocation property</summary>
        RelayCommand RefreshCurrentLocationCommand { get; set; } 
        
        /// <summary>Inviter and Invitee. Turns on location tracking</summary>
        RelayCommand TurnLocationTrackingOnCommand { get; set; }  
        
        /// <summary>Inviter and Invitee. Turns off location tracking</summary>
        RelayCommand TurnLocationTrackingOffCommand { get; set; } 
        
        /// <summary>Inviter and Invitee. Gets the route from a start GeoCoordinate to an end point (e.g. to the RoundUp point)</summary>
        RelayCommand GetRouteCommand { get; set; }

        /// <summary>Attempts to restore a previous session, if it's still alive</summary>
        RelayCommand RestorePreviousSessionCommand { get; set; }

        /// <summary>Allow the user to purchase the app (upgrade from trial)</summary>
        RelayCommand PurchaseCommand { get; set; }

        /// <summary>Allow the user to rate and review the app in the store</summary>
        RelayCommand RateAndReviewCommand { get; set; }

        /// <summary>Show the map control panel</summary>
        RelayCommand ShowMapControlPanelCommand { get; set; }

        /// <summary>Show the share panel</summary>
        RelayCommand ShowSharePanelCommand { get; set; }

        /// <summary>Show the directions panel</summary>
        RelayCommand ShowDirectionsPanelCommand { get; set; }

        /// <summary>Show the invitees panel</summary>
        RelayCommand ShowInviteesPanelCommand { get; set; }

        /// <summary>Sets the MapCatographicMode property based on a command property value</summary>
        RelayCommand MapCartographicConverterCommand { get; set; }

        /// <summary>Show the help panorama</summary>
        RelayCommand ShowHelpCommand { get; set; }

        /// <summary>Centers the map on a particular invitee</summary>
        RelayCommand CenterMapOnInviteeCommand { get; set; }

        /// <summary>Centers the map on the device's current location</summary>
        RelayCommand CenterMapOnDeviceCommand { get; set; }

        /// <summary>Checks for missed notifications</summary>
        RelayCommand SyncNotificationsCommand { get; set; }

        /// <summary>Allows the user's location to be shared via Bing maps</summary>
        RelayCommand ShareLocationViaWebCommand { get; set; }

        /// <summary>Displays the About view</summary>
        RelayCommand ShowAboutViewCommand { get; set; }

        /// <summary>Displays the Settings view</summary>
        RelayCommand ShowSettingsViewCommand { get; set; }

        /// <summary>Handles the selection of a route waypoint (route directions list)</summary>
        RelayCommand RouteWaypointSelectedCommand { get; set; }

        /// <summary>Executed when the menu is shown/hidden</summary>
        RelayCommand MenuStateChangedCommand { get; set; }

        /// <summary>Dump this object's state, along with state from core services</summary>
        RelayCommand DumpCommand { get; set; }

        // Events to commands -------------------------------------------------

        /// <summary>Handler for the map loaded event. Sets up the necessary production api keys (MapLoadedEventToCommand)</summary>
        RelayCommand MapLoadedCommand { get; set; } 

        // Methods ------------------------------------------------------------

        /// <summary>The View calls this method to notify the ViewModel that the hardware Back button was pressed</summary>
        void BackKeyPress(CancelEventArgs args);

        /// <summary>Save state to persistent (isolated) storage</summary>
        void SaveState();

        /// <summary>Restore state from to persistent (isolated) storage</summary>
        void RestoreState();

        /// <summary>Dump our internal state to the console</summary>
        void Dump();
    }
}
