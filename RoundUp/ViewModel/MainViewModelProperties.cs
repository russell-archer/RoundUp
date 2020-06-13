using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Device.Location;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.Phone.Maps.Controls;
using RArcher.Phone.Toolkit;
using RArcher.Phone.Toolkit.Common;
using RArcher.Phone.Toolkit.Location;
using RArcher.Phone.Toolkit.Location.Common;
using RArcher.Phone.Toolkit.Logging;
using RArcher.Phone.Toolkit.Mpns;
using RArcher.Phone.Toolkit.Mpns.Common;
using RArcher.Phone.Toolkit.Network;
using RArcher.Phone.Toolkit.Store;
using RArcher.Phone.Toolkit.Store.Enum;
using RoundUp.Common;
using RoundUp.Model;
using RoundUp.Enum;
using Microsoft.Devices;

namespace RoundUp.ViewModel
{
    /// <summary>ViewModel for MainView (properties and private members)</summary>
    public partial class MainViewModel : ModelBase, IMainViewModel, INotifyPropertyChanged
    {
        /// <summary>
        /// List of live invitees (if any) taking part in a session. The map will bind to this 
        /// collection to show location markers for all invitees
        /// </summary>
        [Dump]
        [AutoState(DefaultValue = null, SaveNullValues = true, RestoreNullValues = true)]
        public ObservableCollection<InviteeLocationMarker> Invitees
        {
            get { return _invitees ?? (_invitees = new ObservableCollection<InviteeLocationMarker>()); }
            set { _invitees = value; OnPropertyChanged(); OnPropertyChanged("HasInvitees"); }
        }

        [AutoState(DefaultValue = null, SaveNullValues = true, RestoreNullValues = true)]
        [Dump]
        public ObservableCollection<RouteInstruction> RouteInstructions
        {
            get { return _routeInstructions ?? (_routeInstructions = new ObservableCollection<RouteInstruction>());}
            set { _routeInstructions = value; OnPropertyChanged(); OnPropertyChanged("HasDirections"); }
        }

        /// <summary>True if we have a connection to the location service</summary>
        /// <remarks>Dependent properties: WaitingForLocationServiceToConnect, AllServicesConnected, IsSessionCancelable, AppBarIndexSelector</remarks>
        [Dump]
        [DoNotSaveState]
        public bool LocationServiceConnected 
        { 
            get { return _locationService.Connected; }
            set
            {
                // We use the setter simply to trigger the required property changed events. The value always resides in _locationService.Connected
                OnPropertyChanged();
                RaiseServicePropertyChangedEvents();
            }
        }

        /// <summary>True if we have received a channel URI back from the MPNS, false otherwise</summary>
        /// <remarks>Dependent properties: WaitingForLocationServiceToConnect, AllServicesConnected, IsSessionCancelable, AppBarIndexSelector</remarks>
        [Dump]
        [DoNotSaveState]
        public bool MpnsServiceConnected
        {
            get { return _mpnsService.Connected; }
            set 
            {
                // We use the setter simply to trigger the required property changed events. The value always resides in _mpns.Connected
                OnPropertyChanged();
                RaiseServicePropertyChangedEvents();
            }
        }

        /// <summary>True if we have a connection to the RoundUp Azure service</summary>
        /// <remarks>Dependent properties: WaitingForLocationServiceToConnect, AllServicesConnected, IsSessionCancelable, AppBarIndexSelector</remarks>
        [Dump]
        [DoNotSaveState]
        public bool RoundUpServiceConnected
        {
            get { return _roundUpService.Connected; }
            set
            {
                // We use the setter simply to trigger the required property changed events. The value always resides in _RoundUpService.Connected
                OnPropertyChanged();
                RaiseServicePropertyChangedEvents();
            }
        }

        /// <summary>True if the location service is trying to connect, false otherwise</summary>
        [Dump]
        [DoNotSaveState]
        public bool WaitingForLocationServiceToConnect
        {
            get { return _waitingForLocationServiceToConnect; }
            set
            {
                if(_waitingForLocationServiceToConnect == value) return;
                _waitingForLocationServiceToConnect = value;
                OnPropertyChanged();
            }
        }

        /// <summary>True if all the necessary app services are connected, false otherwise</summary>
        [Dump]
        [DoNotSaveState]
        public bool AllServicesConnected { get { return LocationServiceConnected && RoundUpServiceConnected && MpnsServiceConnected; } }

        /// <summary>
        /// True if we are an inviter, have location, RoundUp service and MPNS all connected, AND we have a live session 
        /// with one or more invitees, false otherwise
        /// </summary>
        [Dump]
        [DoNotSaveState]
        public bool IsSessionCancelable
        {
            get
            {
                return  IsInviter && 
                        LocationServiceConnected && 
                        RoundUpServiceConnected && 
                        MpnsServiceConnected;  // Session has zero or more invitees 
            }
        }

        /// <summary>True if we're an Invitee and have accepted an invitation</summary>
        [Dump]
        [DoNotSaveState]
        public bool IsInviteCancelable
        {
            get
            {
                return  IsInvitee &&
                        LocationServiceConnected &&
                        RoundUpServiceConnected &&
                        MpnsServiceConnected &&
                        InviteeStatus == InviteeStatusValue.InviteeHasAccepted;
            }
        }

        /// <summary>True if we want to run under the lock screen, false otherwise</summary>
        [Dump]
        [AutoState(defaultValue: false)]
        public bool IsRunningUnderLockScreen
        {
            get { return _isRunningUnderLockScreen; }
            set { _isRunningUnderLockScreen = value; OnPropertyChanged();}
        }

        /// <summary>True if the user is walking to the roundup location, false if they're driving (or the session type hasn't been set)</summary>
        [Dump]
        [AutoState(defaultValue: true)]
        public bool IsWalking
        {
            get { return _isWalking; }
            set { _isWalking = value; OnPropertyChanged();}
        }

        /// <summary>True if the user's session is as an inviter, false otherwise</summary>
        [Dump]
        [AutoState(defaultValue: false)]
        public bool IsInviter
        {
            get { return _isInviter; }
            set 
            {
                _isInviter = value;
                SessionTypeHasBeenSet = _isInviter;

                OnPropertyChanged();
                OnPropertyChanged("AppBarIndexSelector");
            }
        }

        /// <summary>True if the user's session is as an invitee, false otherwise</summary>
        [Dump]
        [AutoState(defaultValue: false)]
        public bool IsInvitee
        {
            get { return _isInvitee; }
            set
            {
                _isInvitee = value;
                SessionTypeHasBeenSet = _isInvitee;

                if(_isInvitee) SettingsTrackCurrentLocation = true;  // Turn on location tracking (necessary if you're an invitee)

                OnPropertyChanged();
                OnPropertyChanged("IsInviteCancelable");
                OnPropertyChanged("AppBarIndexSelector");
            }
        }

        /// <summary>True if we are in either Inviter or Invitee mode</summary>
        [Dump]
        [AutoState(defaultValue: false)]
        public bool SessionTypeHasBeenSet
        {
            get { return _sessionTypeHasBeenSet; }
            set
            {
                _sessionTypeHasBeenSet = value;
                OnPropertyChanged();
            }
        }

        /// <summary>True if the View should show the Invite panel, false otherwise</summary>
        [Dump]
        [AutoState(defaultValue: false)]
        public bool ShowAcceptInviteUI
        {
            get { return _showAcceptInviteUi; }
            set { _showAcceptInviteUi = value; OnPropertyChanged();}
        }

        /// <summary>True if the share panel is displayed, false otherwise</summary>
        [Dump]
        [AutoState(defaultValue: false)]
        public bool ShowShareUI 
        {
            get { return _showShareUi; }
            set
            {
                if(_showShareUi == value) return;  // Nothing's changed so don't raise the events

                _showShareUi = value; 
                OnPropertyChanged(); 
                OnShareUiVisibilityChanged(null);
            }
        }

        /// <summary>True if the directions panel is displayed, false otherwise</summary>
        [Dump]
        [AutoState(defaultValue: false)]
        public bool ShowDirectionsUI
        {
            get { return _showDirectionsUi; }
            set { _showDirectionsUi = value; OnPropertyChanged();}
        }

        /// <summary>True if the invitees panel is displayed, false otherwise</summary>
        [Dump]
        [AutoState(defaultValue: false)]
        public bool ShowInviteesUI
        {
            get { return _showInviteesUi; }
            set { _showInviteesUi = value; OnPropertyChanged();}
        }

        /// <summary>True if a geric progress bar should be displayed, false otherwise</summary>
        [Dump]
        [AutoState(defaultValue: false)]
        public bool ShowProgressBar
        {
            get { return _showProgressBar; }
            set { _showProgressBar = value; OnPropertyChanged();}
        }

        /// <summary>
        /// True if the view's map should display the RoundUp location pushpin (because the inviter's
        /// current location is not being used as the RoundUp point), false otherwise (the inviter's
        /// current location is the RoundUp point)</summary>
        [Dump]
        [AutoState(defaultValue: false)]
        public bool ShowRoundUpPointLocation
        {
            get { return _showRoundUpPointLocation; }
            set { _showRoundUpPointLocation = value; OnPropertyChanged();}
        }

        /// <summary>True if we will allow the user to change the RoundUp location point, false otherwise</summary>
        [Dump]
        [AutoState(defaultValue: false)]
        public bool AllowRoundUpPointLocationChange
        {
            get { return _allowRoundUpPointLocationChange; }
            set
            {
                _allowRoundUpPointLocationChange = value;

                // Don't allow the map controls to be displayed at the same time
                if(value) ShowMapControlPanel = false; 

                OnPropertyChanged();
            }
        }

        /// <summary>True if we're running inside the WP8 emulator</summary>
        [Dump]
        [DoNotSaveState] 
        public bool IsEmulator
        {
            get { return Microsoft.Devices.Environment.DeviceType == DeviceType.Emulator; }
        }

#if DEBUG
        [Dump]
        [DoNotSaveState]
        public bool IsDebug { get { return true; }}
#else
        [DoNotSaveState] 
        public bool IsDebug { get { return false; }}
#endif

        /// <summary>True if we're running in trial mode, false otherwise</summary>
        [Dump]
        [DoNotSaveState]
        public bool IsTrialMode
        {
            get { return License == LicenseMode.Trial || License == LicenseMode.TrialExpired; }
        }

        /// <summary>True if the app was launched with our custom uri association and there are valid start-up params available</summary>
        [Dump]
        [AutoState(defaultValue: false)]
        public bool HasStartUpParams
        {
            get { return _hasStartUpParams; }
            set { _hasStartUpParams = value; OnPropertyChanged();}
        }

        /// <summary>True if state has been restored (or if it's not neccessary to restore state), false otherwise)</summary>
        [Dump]
        [DoNotSaveState]
        public bool StateHasBeenRestored
        {
            get { return _stateHasBeenRestored; }
            set { _stateHasBeenRestored = value; OnPropertyChanged(); }
        }

        /// <summary>True if the map is showing landmarks</summary>
        [Dump]
        [AutoState(defaultValue: false)]
        public bool MapLandmarksOn
        {
            get { return _mapLandmarksOn; }
            set { _mapLandmarksOn = value; OnPropertyChanged(); }
        }

        /// <summary>True if the map is showing pedestrian features</summary>
        [Dump]
        [AutoState(defaultValue: false)]
        public bool MapPedestrianFeaturesOn
        {
            get { return _mapPedestrianFeaturesOn; }
            set { _mapPedestrianFeaturesOn = value; OnPropertyChanged();}
        }

        /// <summary>True if we're showing the map control panel</summary>
        [Dump]
        [DoNotSaveState]
        public bool ShowMapControlPanel
        {
            get { return _showMapControlPanel; }
            set { _showMapControlPanel = value; OnPropertyChanged();}
        }

        /// <summary>True if we're showing the menu bar</summary>
        [Dump]
        [DoNotSaveState]
        public bool ShowMenuBar
        {
            get { return _showMenuBar; }
            set { _showMenuBar = value; OnPropertyChanged();
            }
        }

        /// <summary>True if we're not waiting for an op to complete. This value is used to disable menu items and buttons</summary>
        [Dump]
        [DoNotSaveState]
        public bool OperationHasCompleted
        {
            get { return _operationHasCompleted; }
            set { _operationHasCompleted = value; OnPropertyChanged();}
        }

        /// <summary>True if SessionStatus is either SessionStarted or SessionActive</summary>
        [Dump]
        [DoNotSaveState]
        public bool IsLiveSession
        {
            get { return SessionStatus == SessionStatusValue.SessionActive || SessionStatus == SessionStatusValue.SessionStarted; }
        }

        /// <summary>True if location is shared by the creation of an invite code. If false, a Bing map uri will be generated</summary>
        [Dump]
        [AutoState(defaultValue: true)]
        public bool ShareLocationByInviteCode
        {
            get { return _shareLocationByInviteCode; }
            set { _shareLocationByInviteCode = value; OnPropertyChanged();}
        }

        /// <summary>True if the roundup logo should be displayed on top of the map, false otherwise</summary>
        [Dump]
        [DoNotSaveState]
        public bool ShowMapLogo
        {
            get { return _showMapLogo; }
            set { _showMapLogo = value; OnPropertyChanged();}
        }

        /// <summary>Returns an index that selects the relevant app bar</summary>
        [Dump]
        [DoNotSaveState]
        public int AppBarIndexSelector 
        { 
            get
            {
                // Inviter session
                if(IsInviter) return 1;

                // Invitee session
                if(IsInvitee) return 2 ;

                // Open session. Enable the share (start an Inviter session) and accept/decline invite buttons
                return 0;
            }
        }

        /// <summary>The session id for the current session. Will be -1 if invalid</summary>
        [Dump]
        [AutoState(defaultValue: -1)]
        public int SessionId
        {
            get { return _sessionId; }
            set { _sessionId = value; OnPropertyChanged(); }
        }

        /// <summary>The invitee id for the current session. Will be -1 if invalid</summary>
        [Dump]
        [AutoState(defaultValue: -1)]
        public int InviteeId
        {
            get { return _inviteeId; }
            set { _inviteeId = value; OnPropertyChanged();}
        }

        /// <summary>The device's ID in short (8-char) format</summary>
        [Dump]
        [DoNotSaveState]
        public string ShortDeviceId
        {
            get { return _shortDeviceId; }
            set { _shortDeviceId = value; OnPropertyChanged(); }
        }

        /// <summary>The inviter's device ID. Used in combination with the session id to uniquely identify a session</summary>
        [Dump]
        [AutoState(defaultValue: "")]
        public string InviterShortDeviceId
        {
            get { return _inviterShortDeviceId; }
            set { _inviterShortDeviceId = value; OnPropertyChanged();}
        }

        /// <summary>The text for the invite code (typed or cut and pasted by the user when joining a session)</summary>
        [Dump]
        [AutoState(defaultValue: "")]
        public string InviteCodeText
        {
            get { return _inviteCodeText; }
            set { _inviteCodeText = value; OnPropertyChanged();}
        }

        /// <summary>Text to display in the progress bar</summary>
        [Dump]
        [DoNotSaveState]
        public string ProgressBarText
        {
            get { return _progressBarText; }
            set { _progressBarText = value; OnPropertyChanged();}
        }

        /// <summary>Text that can be used to denote the license mode (e.g. "Trial", "Full", "Trial Expired")</summary>
        [Dump]
        [DoNotSaveState]
        public string LicenseModeText
        {
            get
            {
                switch(License) 
                {
                    case LicenseMode.Trial:
                        var s = Strings.Get("LicenseMode_Trial");
                        return s.Replace("{0}", _storeService.TrialDaysRemaining.ToString(CultureInfo.InvariantCulture));
                    case LicenseMode.Full:
                        return Strings.Get("LicenseMode_Full");
                    case LicenseMode.TrialExpired:
                        return Strings.Get("LicenseMode_TrialExpired");
                    case LicenseMode.MissingOrRevoked:
                        return Strings.Get("LicenseMode_MissingOrRevoked");
                    default:
                        return Strings.Get("LicenseMode_Trial");
                }
            }
        }

        /// <summary>Returns the app's version as a string, suitable for display in the about screen</summary>
        [Dump]
        [DoNotSaveState]
        public string VersionText
        {
            get
            {
                try 
                {
                    return Assembly.GetExecutingAssembly().GetName().Version.ToString();
                }
                catch(Exception ex)
                {
                    Logger.Log(ex, new StackFrame(0, true));                    
                }

                return Strings.Get("About_Version_Content");
            }
        }

        /// <summary>How the user wants to share their location</summary>
        [Dump]
        [AutoState(defaultValue: ShareLocationOption.InviteCodeViaSms)]
        public ShareLocationOption ShareLocationBy
        {
            get { return _shareLocationBy; }
            set { _shareLocationBy = value; OnPropertyChanged();}
        }

        /// <summary>The map's mode (road, aerial, terrain, hybrid)</summary>
        [Dump]
        [AutoState(defaultValue: MapCartographicMode.Road)]
        public MapCartographicMode MapMode
        {
            get { return _mapMode; }
            set { _mapMode = value; OnPropertyChanged();}
        }

        /// <summary>The map's color mode (light/dark)</summary>
        [Dump]
        [AutoState(defaultValue: MapColorMode.Light)]
        public MapColorMode MapDayNightColorMode
        {
            get { return _mapDayNightColorMode; }
            set { _mapDayNightColorMode = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// The device's current location. The View's map Center property binds to this property, 
        /// so we provide a default value to prevent data binding exceptions
        /// </summary>
        [Dump]
        [AutoState]
        public GeoCoordinate CurrentLocation
        {
            get { return _currentLocation ?? new GeoCoordinate(0, 0, 0); }
            set 
            { 
                _currentLocation = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// The location at which the inviter and invitee(s) will RoundUp (note that RoundUpPointLocation may 
        /// not == CurrentLocation, although it often will do)
        /// </summary>
        [Dump]
        [AutoState]
        public GeoCoordinate RoundUpPointLocation
        {
            get { return RoundUpPointInfo.Location ?? new GeoCoordinate(0, 0, 0); }
            set
            {
                try
                {
                    Logger.Log("RoundUpPointLocation changed");
                    RoundUpPointInfo.Location = value; // Setting the location will cause the address and distance to be re-calculated

                    // Get directions to the new RoundUp point (as long as the RoundUp point's not our current location)
                    if (!RoundUpPointInfo.Location.Equals(CurrentLocation))
                        this.GetRouteCommand.Execute(null);

                    OnPropertyChanged();
                }
                catch(Exception ex)
                {
                    Logger.Log(ex, new StackFrame(0, true));
                }
            }
        }

        /// <summary>The new roundup point requested by the inviter</summary>
        [Dump]
        [AutoState]
        public GeoCoordinate RequestedNewRoundUpPoint
        {
            get { return _requestedNewRoundUpPoint; }
            set { _requestedNewRoundUpPoint = value; OnPropertyChanged();}
        }

        /// <summary>The InviteCode (if any) used by the invitee to join a session</summary>
        [Dump(DumpProperties = true)]
        [AutoState]
        public InviteCode CurrentInviteCode
        {
            get { return _currentInviteCode ?? (_currentInviteCode = new InviteCode()); }
            set { _currentInviteCode = value; OnPropertyChanged();}
        }

        /// <summary>
        /// Encapsulates data related to the roundup (meet-up) location, including a GeoCoordinate 
        /// for the location, address, distance, etc. 
        /// </summary>
        [Dump(DumpProperties = true)]
        [AutoState]
        public RoundUpPoint RoundUpPointInfo
        {
            get { return _roundUpPointInfo ?? (_roundUpPointInfo = new RoundUpPoint()); }
            set { _roundUpPointInfo = value; OnPropertyChanged(); }
        }

        /// <summary>The coordinates of the map's center spot (note that MapCenterLocation may not == CurrentLocation)</summary>
        [Dump]
        [AutoState]
        public GeoCoordinate MapCenterLocation
        {
            get { return _mapCenterLocation ?? new GeoCoordinate(0, 0, 0); }
            set { _mapCenterLocation = value; OnPropertyChanged();}
        }

        /// <summary>The zoom level for the location map (1..20)</summary>
        [Dump]
        [AutoState(defaultValue: 17)]
        public double MapZoomLevel
        {
            get { return _mapZoomLevel; }
            set
            {
                // Ignore values outside the min/max range
                if(value < 1 || value > 20) return;  

                _mapZoomLevel = value; 
                OnPropertyChanged();
            }
        }

        /// <summary>The map's pitch (0..75)</summary>
        [Dump]
        [AutoState(defaultValue: 0)]
        public double MapPitch
        {
            get { return _mapPitch; }
            set
            {
                // Ignore values outside the min/max range
                if(value < 0 || value > 75) return;  
                
                _mapPitch = value; 
                OnPropertyChanged();
            }
        }

        /// <summary>The map's heading (0..360)</summary>
        [Dump]
        [AutoState(defaultValue: 0)]
        public double MapHeading
        {
            get { return _mapHeading; }
            set
            {
                // Ignore values outside the min/max range
                if(value < 0 || value > 360) return;  
                
                _mapHeading = value; 
                OnPropertyChanged();
            }
        }

        /// <summary>The status of the current session (see SessionStatusValue enum)</summary>
        /// <remarks>Dependent properties: IsSessionCancelable, AppBarIndexSelector, IsLiveSession</remarks>
        [Dump]
        [AutoState(defaultValue: 0)]
        public SessionStatusValue SessionStatus
        {
            get { return _sessionStatus; }
            set
            {
                _sessionStatus = value;
                OnPropertyChanged();
                OnPropertyChanged("IsSessionCancelable");
                OnPropertyChanged("AppBarIndexSelector");
                OnPropertyChanged("IsLiveSession");
            }
        }

        /// <summary>The invitee status for the current session (see InviteeStatusValue enum)</summary>        
        [Dump]
        [AutoState(defaultValue: 0)]
        public InviteeStatusValue InviteeStatus
        {
            get { return _inviteeStatus; }
            set
            {
                _inviteeStatus = value;
                OnPropertyChanged();
                OnPropertyChanged("IsInviteCancelable");
                OnPropertyChanged("AppBarIndexSelector");
            }
        }

        /// <summary>Holds the license mode for the app (Trial, Full, etc.)</summary>
        [Dump]
        [DoNotSaveState]
        public LicenseMode License
        {
            get { return _storeService.License; }
        }

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

        /// <summary>The DateTime when we were last alive (i.e. when we were last deactivated)</summary>
        [Dump]
        [AutoSetting]
        public DateTime SettingsLastAliveTime
        {
            get { return _settingsLastAliveTime; }
            set { _settingsLastAliveTime = value; OnPropertyChanged(); }
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
            set
            {
                if(_settingsLocationServicesOn == value) return;  // No change

                _settingsLocationServicesOn = value;

                if(_settingsLocationServicesOn) InitLocationService();
                else DisableLocationService();

                OnPropertyChanged();
            }
        }

        /// <summary>Settings. Allows the user to completely turn off MPNS. A requirement to pass the Windows Phone Store certification process</summary>
        [Dump]
        [AutoSetting(defaultValue: true)]
        public bool SettingsMpnsOn
        {
            get { return _settingsMpnsOn; }
            set
            {
                if(_settingsMpnsOn == value) return;  // No change

                _settingsMpnsOn = value;

                if(_settingsMpnsOn && _mpnsService != null && !_mpnsService.Connected && !_mpnsService.Connecting) InitMpns();
                if(!_settingsMpnsOn && _mpnsService != null) DisableMpns();

                OnPropertyChanged();
            }
        }

        /// <summary>Get/set location position tracking in the Location service</summary>
        [Dump]
        [AutoSetting(defaultValue: true)]
        public bool SettingsTrackCurrentLocation
        {
            get { return _settingsTrackCurrentLocation; }
            set
            {
                if(_settingsTrackCurrentLocation == value && _locationService != null && _locationService.TrackCurrentLocation == value) return;

                _settingsTrackCurrentLocation = value;

                if(_locationService != null) _locationService.TrackCurrentLocation = value;

                if(value) TurnLocationTrackingOnCommand.Execute(null);
                else TurnLocationTrackingOffCommand.Execute(null);

                OnPropertyChanged();
            }
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

        // Private members ----------------------------------------------------

        private ObservableCollection<InviteeLocationMarker> _invitees;
        private ObservableCollection<RouteInstruction> _routeInstructions;

        private MpnsChannelHelper _mpnsChannelHelper;
        private LocationServiceHelper _locationServiceHelper;
        private GeoCoordinate _currentLocation;
        private GeoCoordinate _mapCenterLocation;
        private GeoCoordinate _requestedNewRoundUpPoint;
        private RoundUpPoint _roundUpPointInfo;
        private InviteCode _currentInviteCode;
        private DateTime _settingsLastAliveTime; 
        
        private readonly IMpnsService _mpnsService;
        private readonly IRoundUpService _roundUpService;
        private readonly ILocationService _locationService;
        private readonly INetworkService _networkService;
        private readonly IStoreService _storeService;

        private SessionStatusValue _sessionStatus;
        private InviteeStatusValue _inviteeStatus;
        private MapCartographicMode _mapMode;
        private MapColorMode _mapDayNightColorMode;
        private ShareLocationOption _shareLocationBy;

        private bool _isInviter;
        private bool _isInvitee;
        private bool _sessionTypeHasBeenSet;
        private bool _showRoundUpPointLocation;
        private bool _allowRoundUpPointLocationChange;
        private bool _showAcceptInviteUi;
        private bool _showShareUi;
        private bool _hasStartUpParams;
        private bool _isRunningUnderLockScreen;
        private bool _isWalking;
        private bool _showProgressBar;
        private bool _stateHasBeenRestored;
        private bool _initializingMpns;
        private bool _initializingLocationService;
        private bool _haveSubscribedToMpnsEvents;
        private bool _mapLandmarksOn;
        private bool _mapPedestrianFeaturesOn;
        private bool _showMapControlPanel;
        private bool _showMenuBar;
        private bool _operationHasCompleted;
        private bool _waitingForLocationServiceToConnect;
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
        private bool _shareLocationByInviteCode;
        private bool _showDirectionsUi;
        private bool _showInviteesUi;
        private bool _showMapLogo;
        
        private int _sessionId;
        private int _inviteeId;
        private int _settingsSessionDeadTimeout;

        private double _mapZoomLevel;
        private double _mapPitch;
        private double _mapHeading;

        private string _shortDeviceId;
        private string _inviteCodeText;
        private string _settingsAlias;
        private string _inviterShortDeviceId;
        private string _progressBarText;
    }
}
