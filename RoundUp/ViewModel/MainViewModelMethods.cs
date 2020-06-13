using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Device.Location;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Cimbalino.Phone.Toolkit.Services;
using Microsoft.Phone.Notification;
using Microsoft.Phone.Shell;
using RArcher.Phone.Toolkit;
using RArcher.Phone.Toolkit.Common;
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
using Microsoft.Phone.Maps.Controls;
using Microsoft.Phone.Maps.Services;
using RoundUp.View;
using ILocationService = RArcher.Phone.Toolkit.Location.ILocationService;

namespace RoundUp.ViewModel
{
    /// <summary>ViewModel for MainView (methods)</summary>
    public partial class MainViewModel : ModelBase, IMainViewModel, INotifyPropertyChanged
    {
        /// <summary>Default constructor calls the Initialize() method</summary>
        public MainViewModel()
        {
            _roundUpService  = IocContainer.Get<IRoundUpService>();
            _locationService = IocContainer.Get<ILocationService>();
            _mpnsService     = IocContainer.Get<IMpnsService>();
            _networkService  = IocContainer.Get<INetworkService>();
            _storeService    = IocContainer.Get<IStoreService>();

            InitProperties();
            InitCommands();
            InitEventHandlers();

            // Initialize our key services...
            InitRoundUpService();   // Wires-up event handlers and then calls Connect()
            InitLocationService();  // Wires-up event handlers, checks we have permission, and then calls Connect()
            InitMpns();             // Wires-up event handlers and then calls Connect()
            InitStoreService();     // Checks our license
        }

        /// <summary>Dumps the internal state of key objects into a string array</summary>
        /// <returns>Returns the internal state of key objects into a string array. The array will be null if not complied for debug</returns>
        #if DEBUG
        public override void Dump()
        {
            // Dump the state of the view model and various services
            base.Dump();
            
            _mpnsService.Dump();
            _roundUpService.Dump();
            _locationService.Dump();
            _storeService.Dump();
        }
        #else
        /// <summary>Dumps the internal state of key objects into a string array</summary>
        /// <returns>Returns the internal state of key objects into a string array. The array will be null if not complied for debug</returns>
        public override void Dump() { }
        #endif

        /// <summary>The View calls this method to notify the ViewModel that the hardware Back button was pressed</summary>
        public void BackKeyPress(CancelEventArgs args)
        {
            Logger.Log("Hardware Back button was pressed");  
          
            // Normally, hitting the back button will deactivate the app. However, if the user is presented
            // with a message box and they hit back, the message box will close (with a cancel result returned).
            // If one of our "panels" is open and back is pressed, close the panel
            if(ShowMapControlPanel) 
            {
                ShowMapControlPanelCommand.Execute(null);  // Hide the map control panel
                args.Cancel = true;
            }
            else if(ShowShareUI) 
            {
                ShowSharePanelCommand.Execute(null);  // Hide the share panel
                args.Cancel = true;
            }
            else if(ShowAcceptInviteUI) 
            {
                CancelAcceptInviteCommand.Execute(null);  // Hide the share panel
                args.Cancel = true;
            }
            else if(ShowInviteesUI) 
            {
                ShowInviteesPanelCommand.Execute(null);  // Hide the invitees panel
                args.Cancel = true;
            }
            else if(ShowDirectionsUI) 
            {
                ShowDirectionsPanelCommand.Execute(null);  // Hide the directions panel
                args.Cancel = true;
            }
        }

        /// <summary>Save model state. Trigged when the View handles the OnNavigatedFrom() event</summary>
        public override void SaveState()
        {
            Logger.Log("MainViewModel.SaveState");

            SettingsLastAliveTime = DateTime.Now;  // Set the last active time so it can be saved via SaveAutoSetting()

            SaveAutoSetting();  // Save all settings marked with the [AutoSetting] attribute
            SaveAutoState();  // Save all properties marked with the [AutoState] attribute

            // Save the state of the various services
            _mpnsService.SaveState();
            _roundUpService.SaveState();
            _locationService.SaveState(SettingsBackgroundExecutionOn, IsLiveSession);
            _storeService.SaveState();

            // Stop all timers
            if(_mpnsChannelHelper.WaitingForMpnsChannelUri) _mpnsChannelHelper.StopWaiting();

            RefreshFlipTile();  // Update our tile
        }

        /// <summary>
        /// Restore model state. Trigged when the View handles the OnNavigatedTo() event.
        /// Note that this method gets called AFTER the ctor and after the key services have been initialized (and connected).
        /// However, the costructor is NOT called if objects and state are preserved. This is indicated by the 
        /// App.IsApplicationInstancePreserved flag, which is set in the Application_Launching and Application_Activated handlers
        /// in App.xaml.cs, and also in the OnNavigatedFrom() handlers in views other than the main view
        /// </summary>
        public async override void RestoreState()
        {
            Logger.Log(App.IsApplicationInstancePreserved
                ? "MainViewModel.RestoreState: objects will NOT be renewed; no need to restore state"
                : "MainViewModel.RestoreState: objects will be recreated; need to restore state");

            try 
            { 
                if (!App.IsApplicationInstancePreserved)
                { 
                    // App was tombstoned/closed - need to re-create all objects and restore settings/properties from isolated storage
                    // 
                    // This happens when the app's first launched or returning to life having been closed, in which case the
                    // OS calls the view model's constructor

                    RestoreAutoSetting();  // Restore all SETTINGS marked with the [AutoSetting] attribute

                    // Request the app to keep running when the lock screen's engaged.
                    // IdleDetectionMode.Disabled means allow app to run under the lock screen.
                    // This can throw an exception because you can't enable idle detection after disabling it
                    PhoneApplicationService.Current.ApplicationIdleDetectionMode = SettingsRunUnderLockScreenOn ?
                        IdleDetectionMode.Disabled : IdleDetectionMode.Enabled;

                    // When were we last alive? See if we need to try and restore the previous session
                    if(SettingsLastAliveTime.CompareTo(DateTime.Now.Subtract(TimeSpan.FromMinutes(SettingsSessionDeadTimeout))) >= 0)
                    {
                        // Only restore previous session if we don't have startup params
                        if(InviteCodeHelper.LaunchInviteCode == null)
                            await RestorePreviousSession();  // Will call RestoreState() for this object and the key service objects
                    }
                }
                else
                {
                    // App was deactivated - all objects are still in-memory, no need to restore state
                    
                    // If necessary, restore settings because they may have been changed by the settings view
                    if(App.MostRecentView.Contains(typeof(SettingsView).Name)) RestoreAutoSetting();  
                    
                    // Special power-saving case for location services:
                    // If background execution is OFF then call RestoreState (which re-connects location services), 
                    // or, if background execution is ON and we don't have a live session, then call RestoreState (because it will have been disabled to save power)
                    // If background execution is ON and we DO have a live session, no action (because location services will have been left on)

                    if(!SettingsBackgroundExecutionOn || !IsLiveSession)
                    {
                        _locationService.RestoreState();

                        // Make sure to turn location tracking back on if the user allows it
                        if(SettingsTrackCurrentLocation) _locationService.TrackCurrentLocation = true;
                    }

                    // Refresh our current location
                    await _locationService.GetCurrentPositionAsync();
                }

                StateHasBeenRestored = true;
                OnStateRestored(EventArgs.Empty);  // Raise the StateRestored event

                // We always need to look for start-up params however the app starts
                InitStartUpParams();

                // Have we missed any notifications while we've been deactivated or tombstoned?
                await ResendMissedNotificationsAsync();

                // Update our tile appropriately
                RefreshFlipTile();

                // Get our license status from the store
                await _storeService.RefreshLicenseCacheAsync();

                // Make sure our license is valid, otherwise prompt the user to purchase through the store
                if(!CheckLicense()) _storeService.Purchase();
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary>Checks the app's license</summary>
        /// <returns>Returns true if we have a valid full or trial license, therwise returns false</returns>
        private bool CheckLicense()
        {
            // Always restore store settings 
            _storeService.RestoreState();

            // Do we have a full license?
            if(_storeService.IsFullLicense) return true;

            // Has the trial started (if not, start the trial now)
            if(!_storeService.TrialHasStarted)
            {
                _storeService.StartTrial();
                return true;
            }

            // Trial expired yet?
            if(!_storeService.TrialHasExpired) return true; // Trial period still valid

            MessageBoxHelper.Show(Strings.Get("TrialHasExpired"), Strings.Get("TrialHasExpiredTitle"), false);
            return false;  // The user should be prompted to purchase a full license
        }

        /// <summary>Attempts to restore the previous session</summary>
        /// <returns>Returns true if the session was restore, false otherwise</returns>
        private async Task RestorePreviousSession()
        {
            Logger.Log("Will attempt to restore previous session");

            try
            {
                var sid = GetStateItem<int>("SessionId");
                if(sid == -1)
                {
                    App.CanRestorePreviousSession = false;
                    Logger.Log("Previous session was never alive - will not restore");
                    return;
                }

                var connected = await CheckNetworkAvailable();
                if(!connected) return;  // No network - can't check for a previous session

                if(await _roundUpService.IsSessionAliveAsync(sid))
                {
                    App.CanRestorePreviousSession = true;
                    Logger.Log(string.Format("Session (SessionID = {0}) is still alive - restoring", sid));

                    // Restore all properties marked with the [AutoState] attribute
                    RestoreAutoState();

                    // Restore all properties marked with the [AutoState] attribute for our key services
                    _roundUpService.RestoreState();
                    _mpnsService.RestoreState();
                    _locationService.RestoreState();

                    // Make sure to turn location tracking back on if the user allows it
                    if(SettingsTrackCurrentLocation) _locationService.TrackCurrentLocation = true;

                    RaiseServicePropertyChangedEvents();
                }
                else
                {
                    App.CanRestorePreviousSession = false;
                    Logger.Log(string.Format("Session (SessionID = {0}) is dead or never alive - will not restore", sid));
                }
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));                
            }
        }

        /// <summary>Checks to see if any notifications have been sent by the mpns but not received by us (e.g. the app was deactivated)</summary>
        private async Task ResendMissedNotificationsAsync()
        {
            if (_mpnsService == null || _roundUpService == null || (SessionId == -1 && InviteeId == -1)) return;

            try
            {
                // Ask the toolkit MPNS service for a list of notifications that have been received by us
                var receivedNotifications = _mpnsService.Notifications.OfType<RoundUpNotification>().ToList();

                // First, does the SessionId for the current session match the SessionId for stored notifications?
                // If it doesn't then we discard all the stored notifications
                if(receivedNotifications == null || receivedNotifications.Count == 0) Logger.Log("No stored notifications"); 
                else if(SessionId != receivedNotifications[0].SessionId)
                {
                    Logger.Log(string.Format(
                        "Locally saved notifications are for SessionId = {0}. Does not match current SessionId ({1}). Discarding saved notifications",
                        receivedNotifications[0].SessionId,
                        SessionId));  
                    
                    _mpnsService.Notifications.Clear();
                }
                else Logger.Log(string.Format("SessionId for saved notifications matches current SessionId ({0})", SessionId));

                Logger.Log("Getting stored notifications from Azure");
                var connected = await CheckNetworkAvailable();
                if(!connected) return;  // No network - can't check for missed notifications

                // Get all the notifications applicable to us as either an inviter or invitee
                var sentNotifications = await _roundUpService.GetStoredNotificationsAsync(SessionId, (IsInvitee ? InviteeId : -1), IsInviter);
                if(sentNotifications == null)
                {
                    Logger.Log("There are no applicable notifications stored in Azure");
                    return;
                }

                // See if any of the stored notifications have not been received. Note that only "key"
                // notifications are stored like SessionStarted, SessionHasEnded, etc. (e.g. location 
                // update notifications aren't stored) 

                // Before replaying messages in the order they were sent, see if the session has been ended or cancelled
                // Messages that cause the current session to end are: SessionCancelledByInviter, SessionHasEnded, SessionDead
                // Note that although we're potentially enumerating the list of notifications twice, it's normally quite short (5..10 messages)
                Logger.Log("Looking to see if there are missed notifications...");
                foreach(var sentNotification in sentNotifications)
                {
                    if (string.CompareOrdinal(sentNotification.MessageId, "SessionCancelledByInviter") == 0 ||
                        string.CompareOrdinal(sentNotification.MessageId, "SessionHasEnded") == 0 ||
                        string.CompareOrdinal(sentNotification.MessageId, "SessionDead") == 0)
                    {
                        Logger.Log(string.Format("  Session has finished ({0}). No need to replay other messages", sentNotification.MessageId));
                        _mpnsService.ResendMissedNotification(sentNotification);
                        return;  // The session's finished, no need to replay other messages
                    }
                }

                // Replay missed notifications
                foreach (var sentNotification in sentNotifications)
                {
                    var received = false;
                    if (receivedNotifications != null)
                    {
                        foreach (var receivedNotification in receivedNotifications)
                        {
                            if(sentNotification.InviteeId == -1)
                            {
                                // Message is session related
                                if(string.CompareOrdinal(receivedNotification.MessageId, sentNotification.MessageId) == 0)
                                {
                                    Logger.Log("  Already received session-related message " + sentNotification.MessageId);
                                    received = true;
                                    break;
                                }
                            }
                            else
                            {
                                // Message is related to an invitee
                                if( string.CompareOrdinal(receivedNotification.MessageId, sentNotification.MessageId) == 0  &&
                                    receivedNotification.InviteeId == sentNotification.InviteeId)
                                {
                                    Logger.Log(string.Format(
                                        "  Already received message {0} related to InviteeId {1}", 
                                        sentNotification.MessageId,
                                        sentNotification.InviteeId));

                                    received = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!received) _mpnsService.ResendMissedNotification(sentNotification);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary>Initialize various properties</summary>
        private void InitProperties()
        {
            try
            {
                SessionTypeHasBeenSet = false;
                IsInviter = false;
                IsInvitee = false;
                SessionStatus = SessionStatusValue.NotSet;
                InviteeStatus = InviteeStatusValue.NotSet;
                SessionId = -1;
                InviteeId = -1;
                ShortDeviceId = DeviceHelper.GetShortDigitDeviceId();
                InviterShortDeviceId = string.Empty;
                ShowAcceptInviteUI = false;
                ShowShareUI = false;
                ShowMapControlPanel = false;
                ShowDirectionsUI = false;
                ShowInviteesUI = false;
                ShowProgressBar = false;
                ShowRoundUpPointLocation = false;
                ShowMenuBar = true;
                InviteCodeText = string.Empty;
                MapZoomLevel = 17;
                AllowRoundUpPointLocationChange = false;
                HasStartUpParams = false;
                OperationHasCompleted = true;
                WaitingForLocationServiceToConnect = false;
                ShareLocationBy = ShareLocationOption.InviteCodeViaSms;
                ShareLocationByInviteCode = true;

                // Privates:
                _initializingMpns = false;
                _initializingLocationService = false;
                _haveSubscribedToMpnsEvents = false;
                _mpnsChannelHelper = new MpnsChannelHelper(_mpnsService);
                _locationServiceHelper = new LocationServiceHelper(_locationService);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));                                
            }
        }

        /// <summary>Reset all our properties back to default values, normally done when a session ends</summary>
        private void ResetProperties()
        {
            try
            {
                Logger.Log("Resetting: ");
                Logger.Log("  Reset properties to default values");
                
                SessionTypeHasBeenSet = false;
                IsInviter = false;
                IsInvitee = false;
                SessionStatus = SessionStatusValue.NotSet;
                InviteeStatus = InviteeStatusValue.NotSet;
                SessionId = -1;
                InviteeId = -1;
                InviterShortDeviceId = string.Empty;
                ShowAcceptInviteUI = false;
                ShowShareUI = false;
                ShowMapControlPanel = false;
                ShowDirectionsUI = false;
                ShowInviteesUI = false;
                ShowProgressBar = false;
                ShowRoundUpPointLocation = false;
                ShowMenuBar = true;
                InviteCodeText = string.Empty;
                AllowRoundUpPointLocationChange = false;
                HasStartUpParams = false;
                OperationHasCompleted = true;
                WaitingForLocationServiceToConnect = false;
                ShareLocationBy = ShareLocationOption.InviteCodeViaSms;
                ShareLocationByInviteCode = true;

                // Privates:
                _haveSubscribedToMpnsEvents = false;

                if(RouteInstructions != null)
                {
                    Logger.Log("  Clearing route instructions");
                    RouteInstructions.Clear();  // Clear any (text) directions to the roundup point
                }

                // Clear the invitee push pins from the map. Note that simply Clear() or new'ing Invitees throws
                // a threading exception (and it's not a UI thread sync issue), which is why I remove them one at a time...
                if(Invitees != null)
                {
                    Logger.Log("  Clearing invitee list");

                    var nInvitees = Invitees.Count;
                    for(var i = 0; i < nInvitees; i++) Invitees.RemoveAt(0);
                }

                // Fire the MapRouteChanged event to allow the View to remove the old route from the map control
                Logger.Log("  Clearing map route");
                OnMapRouteChanged(new MapRouteEventArgs {NewRoute = null});

                // Clear all our stored notifications
                Logger.Log("  Clearing notifications");
                _mpnsService.Notifications.Clear();

                // Update our tile
                RefreshFlipTile();
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));                
            }
        }

        /// <summary>Initialize view model event handlers</summary>
        private void InitEventHandlers()
        {
            try
            {
                // Hook into the root frame Obscured/Unobscured event so we can flag if we're running 
                // under the lock screen or not
                if(App.RootFrame == null) return;
                
                App.RootFrame.Obscured += (o, args) =>
                {
                    if(args == null || IsRunningUnderLockScreen == args.IsLocked) return;
                    IsRunningUnderLockScreen = args.IsLocked;
                    if(IsRunningUnderLockScreen) Logger.Log("The app is running under the lock screen");
                };

                App.RootFrame.Unobscured += (o, args) =>
                {
                    IsRunningUnderLockScreen = false;
                };

            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));                                
            }
        }

        /// <summary>Set up the delegates for all the RelayCommand commands</summary>
        private void InitCommands()
        {
            try
            {
                InviteCommand = new RelayCommand(DoInviteCommand);
                CancelSessionCommand = new RelayCommand(DoCancelSessionCommand);
                StartAcceptInviteCommand = new RelayCommand(DoStartAcceptInviteCommand);
                CompleteAcceptInviteCommand = new RelayCommand(DoCompleteAcceptInviteCommand);
                CancelAcceptInviteCommand = new RelayCommand(DoCancelAcceptInviteCommand);
                CancelAcceptedInvitationCommand = new RelayCommand(DoCancelAcceptedInvitationCommand);
                SendInviteSmsCommand = new RelayCommand(DoSendInviteSmsCommand);
                SendInviteEmailCommand = new RelayCommand(DoSendInviteEmailCommand);
                StartSetNewRoundUpPointCommand = new RelayCommand(DoStartSetNewRoundUpPointCommand);
                SetNewRoundUpPointCommand = new RelayCommand(DoSetNewRoundUpPointCommand);
                RefreshCurrentLocationCommand = new RelayCommand(DoRefreshCurrentLocationCommand);
                TurnLocationTrackingOnCommand = new RelayCommand(DoTurnLocationTrackingOnCommand);
                TurnLocationTrackingOffCommand = new RelayCommand(DoTurnLocationTrackingOffCommand);
                MapLoadedCommand = new RelayCommand(DoMapLoadedCommand);
                GetRouteCommand = new RelayCommand(DoGetRouteCommand);
                RestorePreviousSessionCommand = new RelayCommand(DoRestorePreviousSessionCommand);
                PurchaseCommand = new RelayCommand(DoPurchaseCommand);
                RateAndReviewCommand = new RelayCommand(DoRateAndReviewCommand);
                ShowMapControlPanelCommand = new RelayCommand(DoShowMapControlPanelCommand);
                MapCartographicConverterCommand = new RelayCommand(DoMapCartographicConverterCommand);
                ShowHelpCommand = new RelayCommand(DoShowHelpCommand);
                CenterMapOnInviteeCommand = new RelayCommand(DoCenterMapOnInviteeCommand);
                CenterMapOnDeviceCommand = new RelayCommand(DoCenterMapOnDeviceCommand);
                SyncNotificationsCommand = new RelayCommand(DoSyncNotificationsCommand);
                ShareLocationViaWebCommand = new RelayCommand(DoShareLocationViaWebCommand);
                ShowAboutViewCommand = new RelayCommand(DoShowAboutViewCommand);
                ShowSettingsViewCommand = new RelayCommand(DoShowSettingsViewCommand);
                ShowSharePanelCommand = new RelayCommand(DoShowSharePanelCommand);
                ShowDirectionsPanelCommand = new RelayCommand(DoShowDirectionsPanelCommand);
                ShowInviteesPanelCommand = new RelayCommand(DoShowInviteesPanelCommand);
                MenuStateChangedCommand = new RelayCommand(DoMenuStateChangedCommand);
                RouteWaypointSelectedCommand = new RelayCommand(DoRouteWaypointSelectedCommand);
                DumpCommand = new RelayCommand(DoDumpCommand);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));                                
            }
        }

        /// <summary>Initialize the MPNS service. Raises the MpnsServiceConnected event when the service is connected</summary>
        private void InitMpns()
        {
            try
            {
                if(_mpnsService.Connected || _mpnsService.Connecting || _initializingMpns) return;

                _initializingMpns = true;  // Prevents additional calls to InitMpns while we're waiting for the user to give us permission

                Logger.Log("InitMpns");

                // Before starting to actually use MPNS, have we asked the user's permission?
                if(!WeHavePermissionToUseMpns())
                {
                    _initializingMpns = false;
                    return;
                }

                // Set the channel URI name
                _mpnsService.ChannelName = "RoundUpMPNS";

                // Tell the MPNS service what type of custom notification we want to use
                _mpnsService.Notification = new RoundUpNotification();

                // Subscribe to the various MPNS events
                SubscribeToMpnsEvents();

                _initializingMpns = false;

                // Connect to MPNS
                _mpnsService.Connect();
            }
            catch (Exception ex)
            {
                _initializingMpns = false;
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        private void SubscribeToMpnsEvents()
        {
            if(_haveSubscribedToMpnsEvents) return;
            _haveSubscribedToMpnsEvents = true;

            _mpnsService.PushNotification += OnMpnsPushNotification;
            _mpnsService.ChannelOpen += OnChannelOpen;
            _mpnsService.PropertyChanged += MpnsServiceOnPropertyChanged;
            _mpnsService.ChannelError += OnChannelError;
            _mpnsService.ChannelDisconnected += OnChannelDisconnected;
        }

        /// <summary>Initialize the RoundUp Windows Azure service. Raises the RoundUpServiceConnected event when the service is connected</summary>
        private void InitRoundUpService()
        {
            try
            {
                if(_roundUpService.Connected) return;

                Logger.Log("InitRoundUpService");

                _roundUpService.PropertyChanged += (sender, args) =>
                {
                    if(string.Compare(args.PropertyName, "Connected", StringComparison.Ordinal) == 0)
                        RoundUpServiceConnected = _roundUpService.Connected;
                };

                _roundUpService.Connect();
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary>Initialize the location service. Raises the LocationServiceConnected event when the service is connected</summary>
        private async void InitLocationService()
        {
            try
            {
                if(_locationService.Connected || _locationService.IsConnecting || _initializingLocationService) return;

                _initializingLocationService = true;

                // Before starting to actually use any location services, have we asked the user's permission?
                if(!WeHavePermissionToUseLocationServices())
                {
                    _initializingLocationService = false;
                    return;
                }

                Logger.Log("InitLocationService");

                _locationService.CurrentLocationChanged += LocationServiceOnCurrentLocationChanged;
                _locationService.PropertyChanged += LocationServiceOnPropertyChanged;

                // Show the "getting your location" progress. If we still haven't got a location fix after
                // 20-secs we'll hide the progress bar
                WaitingForLocationServiceToConnect = true;
                _locationServiceHelper.StartWaiting(() => { WaitingForLocationServiceToConnect = false; }, () =>
                {
                    WaitingForLocationServiceToConnect = false;
                    MessageBoxHelper.Show(Strings.Get("CannotGetLocation"), Strings.Get("CannotGetLocationTitle"), false);
                });

                _initializingLocationService = false;
                _locationService.Connect();
                await _locationService.GetCurrentPositionAsync();
            }
            catch (Exception ex)
            {
                _initializingLocationService = false;
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary>Initialize the store helper and check the status of the license</summary>
        private void InitStoreService()
        {
            try
            {
                Logger.Log("InitStoreService");
                
                _storeService.PropertyChanged += (sender, args) =>
                {
                    if(string.Compare(args.PropertyName, "License", StringComparison.Ordinal) == 0)
                    {
                        // Update our properties to reflect the underlying service's state
                        OnPropertyChanged("License");
                        OnPropertyChanged("LicenseModeText");
                        OnPropertyChanged("IsTrialMode");
                    }
                };

                _storeService.PurchaseReminder += (sender, reason) =>
                {
                    if( reason == PurchaseReminderReason.TrialExpirationReminderLimit  ||
                        reason == PurchaseReminderReason.UsageCount)
                    {
                        var s = Strings.Get("PurchaseReminder").Replace(
                            "{0}", _storeService.TrialDaysRemaining.ToString(CultureInfo.InvariantCulture));

                        MessageBoxHelper.Show(s, Strings.Get("PurchaseReminderTitle"), false);
                    }
                };

                _storeService.RateReminder += (sender, args) =>
                {
                    var result = MessageBoxHelper.Show(Strings.Get("RateAppReminder"), Strings.Get("RateAppReminderTitle"), MessageBoxButton.OKCancel);
                    if(result == MessageBoxResult.Cancel) return; // User didn't want to rate

                    _storeService.RateAndReview();
                };
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }            
        }

        /// <summary>
        /// Checks to see if we were started using the custom rndup: uri association. If so, the UriMapper class
        /// will have setup the necessary params in the static InviteCodeHelper.LaunchInviteCode field
        /// </summary>
        private void InitStartUpParams()
        {
            if (InviteCodeHelper.LaunchInviteCode == null) return;

            HasStartUpParams = true;  // Flag that as soon as all serviecs are connected we'll attempt to auto-accept an invite
        }

        /// <summary>Disables the location service and unsubscribes from the CurrentLocationChanged and PropertyChanged events</summary>
        private void DisableLocationService()
        {
            try
            {
                if (_locationService == null) return;

                _locationService.Disable();
                _locationService.CurrentLocationChanged -= LocationServiceOnCurrentLocationChanged;
                _locationService.PropertyChanged -= LocationServiceOnPropertyChanged;
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary>Disables the MPNS and unsubscribes from the PushNotification, ChannelOpen and PropertyChanged events</summary>
        private void DisableMpns()
        {
            try
            {
                if(_mpnsService == null) return;

                _mpnsService.Disable();
                _mpnsService.PushNotification -= OnMpnsPushNotification;
                _mpnsService.ChannelOpen -= OnChannelOpen;
                _mpnsService.PropertyChanged -= LocationServiceOnPropertyChanged;
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary>
        /// Handles the location service's OnPropertyChanged event. Primarily handles "Connected" and "CurrentLocation"
        /// property changes. Also provides default handling of the MovementThreshold and TrackCurrentLocation property changes
        /// </summary>
        /// <param name="sender">The sender of the event</param>
        /// <param name="propertyChangedEventArgs">Details of the changed property</param>
        private void LocationServiceOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            try
            {
                if(string.Compare(propertyChangedEventArgs.PropertyName, "Connected", StringComparison.Ordinal) == 0)
                {
                    // We need to make sure we're running on the UI thread or we'll get data binding
                    // exceptions. This is caused because the GeolocatorOnStatusChanged event is raised 
                    // on a different thread from the UI
                    Deployment.Current.Dispatcher.BeginInvoke(async () =>
                    {
                        LocationServiceConnected = _locationService.Connected;
                        if(!LocationServiceConnected) return;

                        MapZoomLevel = InitialMapZoomLevel;
                        CurrentLocation = await _locationService.GetCurrentPositionAsync();
                        MapCenterLocation = CurrentLocation;  // Initially centre the map on the current location
                    });
                    return;
                }

                if(string.Compare(propertyChangedEventArgs.PropertyName, "CurrentLocation", StringComparison.Ordinal) == 0)
                {
                    return; // Discard the notification. We handle location changes via the location service's CurrentLocationChanged event
                }

                // Default handler for the following LocationService property changed events:
                // MovementThreshold, TrackCurrentLocation...
                if(Deployment.Current.Dispatcher.CheckAccess()) OnPropertyChanged(propertyChangedEventArgs.PropertyName);
                else Deployment.Current.Dispatcher.BeginInvoke(() => OnPropertyChanged(propertyChangedEventArgs.PropertyName));
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));                                
            }
        }

        /// <summary>Handles the location service's CurrentLocationChanged event</summary>
        /// <param name="sender">The sender of the event </param>
        /// <param name="args">Coordinates of the new location, plus an flag showing if the device has moved by the threshold value</param>
        private void LocationServiceOnCurrentLocationChanged(object sender, LocationUpdateEventArgs args)
        {
            HandleLocationChanged(args);
        }

        /// <summary>Common handler for the location service's CurrentLocationChanged event and the CurrentLocation property change</summary>
        /// <param name="args">The GeoCoordinate of the new location, plus an flag showing if the device has moved by the threshold value</param>
        private void HandleLocationChanged(LocationUpdateEventArgs args)
        {
            try
            {
                // Sync thread access as both the CurrentLocation property change and CurrentLocationChanged 
                // event arrive on a non-UI thread
                Deployment.Current.Dispatcher.BeginInvoke(async () =>
                {
                    Logger.Log("Location change handled");

                    if(CurrentLocation.Equals(args.Location)) return;  // No actual location change

                    // Set the current location to be the new location
                    CurrentLocation = args.Location;
                    MapCenterLocation = CurrentLocation;

                    // Raise the CurrentLocationChanged event, this allows the view to place the current location "sonar"
                    // animation at the correct location on the map
                    OnCurrentLocationChanged(new LocationUpdateEventArgs{ DeltaHasExceededThreshold = true, Location = CurrentLocation });

                    // By default, the RoundUp point is the inviter's current location. 
                    // If ShowRoundUpPointLocation is *true* then the inviter's current location and the RoundUp 
                    // point are different locations, and we don't need to keep CurrentLocation and 
                    // RoundUpPointLocation in sync. If ShowRoundUpPointLocation is false then CurrentLocation
                    // and ShowRoundUpPointLocation should always have the same value
                    if (!ShowRoundUpPointLocation) RoundUpPointLocation = CurrentLocation;

                    // Broadcast the location change if we're an invitee that's part of a live session.
                    // We also need to update our (invitee's) view of the roundup pushpin with a recalculated distance
                    if (IsInvitee &&
                        InviteeId != -1 &&
                        SessionId != -1 &&
                        CurrentLocation != null &&
                        SessionStatus == SessionStatusValue.SessionActive &&
                        InviteeStatus == InviteeStatusValue.InviteeHasAccepted)
                    {
                        // First, update the roundup pushpin with the new distance the invitee has to travel
                        RoundUpPointInfo.DistanceToRoundUpPoint = _locationService.GetDistance(CurrentLocation, RoundUpPointLocation);

                        // If the device hasn't moved by >= the appropriate movement threshold then don't broadcast the 
                        // location change (avoids too many MPNS notifications)
                        if(!args.DeltaHasExceededThreshold)
                        {
                            Logger.Log("Location changed event handled. No broadcast because movement threshold has not been reached");
                            await AreWeThereYet();
                            return;
                        }

                        // If the MPNS channel isn't connected, wait until it is (the call to DoCompleteInviteCommand is in the handler)
                        if (!_mpnsService.Connected)
                        {
                            Logger.Log("Start wait for MPNS ChannelOpen (HandleLocationChanged)");
                            _mpnsChannelHelper.StartWaiting(DoCompleteHandleLocationChanged, MpnsWaitSilentTimedout);  // Non-critical MPNS failure (silent fail)
                        }
                        else await DoCompleteHandleLocationChanged();
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                _mpnsChannelHelper.WaitingForMpnsChannelUri = false;
            }
        }

        /// <summary>
        /// Completes the handling of HandleLocationChanged. This method should not be called directly - it's
        /// called from HandleLocationChanged (if MPNS is connected) or OnChannelOpenDoCompleteHandleLocationChanged
        /// (when MPNS becomes connected)
        /// </summary>
        private async Task DoCompleteHandleLocationChanged()
        {
            try
            {
                // Broadcast the location change
                Logger.Log("Requesting broadcast of location update");

                if(!await CheckNetworkAvailable(showProgressBar: false)) return;  // Can't broadcast location update - this is non-critical
                
                var result = await _roundUpService.UpdateInviteeLocationAsync(
                    InviteeId,
                    SessionId,
                    _mpnsService.ChannelUri.ToString(),
                    CurrentLocation.Latitude,
                    CurrentLocation.Longitude,
                    InviterShortDeviceId,
                    SettingsAlias,
                    string.Empty);

                switch(result)
                {
                    case RoundUpServiceOperationResult.OperationSuccess:
                        break;

                    case RoundUpServiceOperationResult.MpnsNotificationLimitExceeded:
                        Logger.Log("MPNS notification limit exceeded", new StackFrame(0, true));

                        if(App.IsRunningInBackground) return;  // Ignore the error for now, as we're running in the bg

                        MessageBoxHelper.Show(Strings.Get("MpnsLimitExceeded"), Strings.Get("MpnsLimitExceededTitle"), false);
                        return;

                    default:
                        Logger.Log("RoundUpService.UpdateInviteeLocation() returned an error", new StackFrame(0, true));
                        break;
                }

                // Have we arrived at the roundup location?  
                await AreWeThereYet();
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary>Decides if we've arrived at the roundup point. If we have, requests the necessary MPNS notifications</summary>
        private async Task<bool> AreWeThereYet()
        {
            try
            {
                // Have we arrived at the roundup location?  
                if(_locationService.LocationsAreClose(RoundUpPointInfo.DistanceToRoundUpPoint))
                {
                    Logger.Log("We have arrived at the roundup location");

                    // Broadcast the arrival to the inviter
                    var broadcastResult = await _roundUpService.InviteeHasArrivedAsync(
                        InviteeId,
                        SessionId,
                        _mpnsService.ChannelUri.ToString(),
                        CurrentLocation.Latitude,
                        CurrentLocation.Longitude,
                        InviterShortDeviceId,
                        SettingsAlias,
                        string.Empty);

                    if(SettingsToastOn) MessageBoxHelper.ShowToast(Strings.Get("YouHaveArrived"), Strings.Get("YouHaveArrivedTitle"));

                    if(IsInvitee) ResetProperties();

                    switch(broadcastResult)
                    {
                        case RoundUpServiceOperationResult.OperationSuccess:
                            break;

                        case RoundUpServiceOperationResult.MpnsNotificationLimitExceeded:
                            Logger.Log("MPNS notification limit exceeded", new StackFrame(0, true));

                            // Display an error, only if we're not running in the background
                            if(App.IsRunningInBackground) break;

                            MessageBoxHelper.Show(Strings.Get("MpnsLimitExceeded"), Strings.Get("MpnsLimitExceededTitle"), false);
                            break;

                        default:
                            Logger.Log("RoundUpService.InviteeHasArrived() returned an error", new StackFrame(0, true));
                            break;
                    }

                    return true;  // We've arrived
                }                
            }
            catch(Exception ex) 
            {
                Logger.Log(ex, new StackFrame(0, true));
            }

            return false;
        }

        /// <summary>Handles all notifications from the MPNS</summary>
        /// <param name="sender">The object that raised the notification</param>
        /// <param name="args">The notification sent from the MPNS</param>
        private void OnMpnsPushNotification(object sender, MpnsNotificationEventArgs args)
        {
            try
            {
                var roundUpNotification = args.Notification as RoundUpNotification;
                if(roundUpNotification == null)
                {
                    Logger.Log("Error: Unable to convert incoming MPNS notification to a RoundUpNotification");
                    return;
                }

                RoundUpNotificationMessage messageId;

                if (!RoundUpNotificationMessage.TryParse(roundUpNotification.MessageId, true, out messageId))
                {
                    Logger.Log("Unknown notification received: " + roundUpNotification.MessageId);
                    return;
                }

                Logger.Log("Incoming notification: " + messageId.ToString());

                switch (messageId)
                {
                    // Note that for all the following notifications we need to make sure we're running 
                    // on the UI thread or we'll get data binding exceptions. This is caused because the 
                    // OnHttpNotificationReceived event is raised on a different thread from the UI

                    case RoundUpNotificationMessage.InvalidMessage:
                        Logger.Log("InvalidMessage notification received");
                        break;

                    case RoundUpNotificationMessage.SessionStarted:
                        // This notification now obsolete - all processing for a new session
                        // is handled directly in DoCompleteInviteCommand()
                        Logger.Log("Obsolete notification (SessionStarted) received");
                        break;

                    case RoundUpNotificationMessage.SessionCancelledByInviter:     // Session has been cancelled by the inviter
                        if(!IsInvitee) return;  // This message should only be broadcast to invitees

                        Deployment.Current.Dispatcher.BeginInvoke(() =>
                        {
                            var message = string.Format(
                                "{0} {1}", 
                                string.IsNullOrEmpty(roundUpNotification.Data) ? 
                                    Strings.Get("MissingInviterName") : 
                                    roundUpNotification.Data, Strings.Get("SessionHasBeenCancelledByInviter")); // {inviter} has cancelled the session

                            MessageBoxHelper.Show(message, Strings.Get("SessionHasBeenCancelledByInviterTitle"), SettingsToastOn);

                            ResetProperties();
                        });
                        break;
                
                    case RoundUpNotificationMessage.InviteeHasAccepted:
                        // An invitee has just accepted (the session will have a SessionStatusId of SessionActive)
                        // The invitee id and invitee name/alias will be included in the InviteeId and Data fields
                        // of the "InviteeHasAccepted" notification

                        if(IsInviter)
                        {
                            Deployment.Current.Dispatcher.BeginInvoke(() =>
                            {
                                // Mark the session as SessionActive and add the invitee to our list of live invitees - 
                                // the map binds to the Invitees collection and displays location markers
                                var inviteeLocation = new GeoCoordinate(roundUpNotification.Latitude, roundUpNotification.Longitude);
                                var invitee = new InviteeLocationMarker
                                {
                                    id = roundUpNotification.InviteeId,
                                    Location = inviteeLocation,
                                    Name = roundUpNotification.Data,
                                    InstantMessage = string.Empty,
                                    DistanceToRoundUpPoint = _locationService.GetDistance(inviteeLocation, RoundUpPointLocation)
                                };

                                SessionStatus = SessionStatusValue.SessionActive;
                                Invitees.Add(invitee);

                                // Tell the user the invitee accepted the invite. The name of the invitee is in args.Notification.Data 
                                if(SettingsToastOn)
                                    MessageBoxHelper.ShowToast(
                                        string.Format("{0} {1}", 
                                        roundUpNotification.Data, 
                                        Strings.Get("InviteeHasAcceptedToast")), 
                                        Strings.Get("InviteeHasAcceptedToastTitle"));

                                Logger.Log(
                                    string.Format("{0} accepted an invite. InviteeId = {1}. Invitee location = lat {2}, long {3}",
                                    roundUpNotification.Data,
                                    roundUpNotification.InviteeId,
                                    roundUpNotification.Latitude,
                                    roundUpNotification.Longitude));

                                RefreshFlipTile();
                            });
                        }
                        else
                        {
                            // This notification now obsolete - all processing for a new session
                            // is handled directly in DoCompleteInviteCommand()
                            Logger.Log("Obsolete notification (InviteeHasAccepted for invitee) received");
                        }

                        break;
                
                    case RoundUpNotificationMessage.InviteeLocationUpdate:           // Invitee has updated their location
                        if(!IsInviter) return;  // This message should only be broadcast to inviters

                        Deployment.Current.Dispatcher.BeginInvoke(() =>
                        {
                            // Find the right InviteeLocationMarker object in Invitees and update it's location
                            var id = roundUpNotification.InviteeId;
                            var q = from i in Invitees where i.id == id select i;
                            var inviteeToUpdate = q.FirstOrDefault();
                            if(inviteeToUpdate == null) return;

                            inviteeToUpdate.Location = new GeoCoordinate(roundUpNotification.Latitude, roundUpNotification.Longitude);
                            inviteeToUpdate.DistanceToRoundUpPoint = _locationService.GetDistance(inviteeToUpdate.Location, RoundUpPointLocation);
                        });
                        break;
                
                    case RoundUpNotificationMessage.InviteeHasCancelled:             // Invitee has cancelled participation in the session
                        if(!IsInviter) return;  // This message should only be broadcast to inviters

                        Deployment.Current.Dispatcher.BeginInvoke(() =>
                        {
                            // Find the invitee who cancelled and remove them
                            var id = roundUpNotification.InviteeId;
                            var q = from i in Invitees where i.id == id select i;
                            var inviteeToUpdate = q.FirstOrDefault();
                            if(inviteeToUpdate == null) 
                            {
                                Logger.Log("Unable to find invitee that cancelled");
                                return; 
                            }

                            // Remove the invitee from our list
                            Invitees.Remove(inviteeToUpdate);

                            // Do we have any invitees still in the session?
                            var qInvitees = from i in Invitees where i.DistanceToRoundUpPoint > 0 select i;
                            if(qInvitees.Any()) 
                            {
                                // Tell the user the invitee has cancelled 
                                if(SettingsToastOn)
                                    MessageBoxHelper.ShowToast(
                                        string.Format(
                                            "{0} {1}",
                                            string.IsNullOrEmpty(roundUpNotification.Data) ? Strings.Get("MissingInviteeName") : roundUpNotification.Data,
                                            Strings.Get("InviteeHasCancelledToast")),
                                        Strings.Get("InviteeHasCancelledToastTitle"));

                                RefreshFlipTile();

                                return;  // Session's still live - we have invitees who've not arrived or cancelled
                            }

                            // No invitees left - prompt the user if they want to cancel
                            if(!App.IsRunningInBackground)
                            {
                                var result = MessageBoxHelper.Show(
                                    Strings.Get("AllInviteesHaveCancelled"), 
                                    Strings.Get("AllInviteesHaveCancelledTitle"), 
                                    MessageBoxButton.OKCancel);

                                if(result == MessageBoxResult.Cancel) return; // Don't close the session

                                CloseSession();
                            }
                            else
                            {
                                CloseSession();  // Auto-close the session
                                if(SettingsToastOn) 
                                    MessageBoxHelper.ShowToast(Strings.Get("AllInviteesHaveCancelledToast"), Strings.Get("AllInviteesHaveCancelledToastTitle"));
                            }
                        });
                        break;
                
                    case RoundUpNotificationMessage.InviteeHasArrived:               // Invitee has arrived at the inviter's location 

                        Deployment.Current.Dispatcher.BeginInvoke(() =>
                        {
                            // Find the invitee that has arrived
                            var id = roundUpNotification.InviteeId;
                            var q = from i in Invitees where i.id == id select i;
                            var inviteeToUpdate = q.FirstOrDefault();
                            if (inviteeToUpdate == null) return;

                            // Remove the invitee from our list and from the map
                            Invitees.Remove(inviteeToUpdate);

                            // Tell the user the invitee has arrived at the roundup point 
                            if(SettingsToastOn)
                                MessageBoxHelper.ShowToast(
                                    string.Format(
                                        "{0} {1}",
                                        string.IsNullOrEmpty(roundUpNotification.Data) ? Strings.Get("MissingInviteeName") : roundUpNotification.Data,
                                        Strings.Get("InviteeHasArrivedToast")),
                                    Strings.Get("InviteeHasArrivedToastTitle"));

                            // Update our tile
                            RefreshFlipTile();

                            // Is this the end of the session (have all invitees arrived?)
                            var qInvitees = from i in Invitees where i.DistanceToRoundUpPoint > 0 select i;
                            if(qInvitees.Any()) return;  // Session's still live - we have invitees who've not arrived yet

                            if(!App.IsRunningInBackground)
                            {
                                var result = MessageBoxHelper.Show(
                                    Strings.Get("EndSession"), 
                                    Strings.Get("AllInviteesArrived"), 
                                    MessageBoxButton.OKCancel);

                                if(result == MessageBoxResult.Cancel) return;

                                CloseSession();
                            }
                            else
                            {
                                CloseSession();  // Auto-close the session
                                if(SettingsToastOn) MessageBoxHelper.ShowToast(Strings.Get("AllInviteesArrived"), Strings.Get("AllInviteesArrivedSessionComplete"));
                            } 
                        });
              
                        break;

                    case RoundUpNotificationMessage.RoundUpLocationChange:            // Inviter has changed the RoundUp location

                        // Inviters should never get this notification. For inviters, the new RoundUp location (RoundUpPointLocation)
                        // is set in either DoStartSetNewRoundUpPointCommand or DoSetNewRoundUpPointCommand
                        if(IsInviter) return;  

                        // Sync threading context
                        Deployment.Current.Dispatcher.BeginInvoke(() =>
                        {
                            RoundUpPointLocation = new GeoCoordinate(roundUpNotification.Latitude, roundUpNotification.Longitude);
                        });
                        break;

                    case RoundUpNotificationMessage.SessionHasEnded:                // Session has ended (all invitees arrived)
                        Deployment.Current.Dispatcher.BeginInvoke(() =>
                        {
                            ResetProperties();
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary>Handles the MPNS ChannelOpen event, which is raised whenever the channel uri is set or changed</summary>
        /// <param name="sender">The object that raised the event</param>
        /// <param name="e">The new channel uri</param>
        private void OnChannelOpen(object sender, Uri e)
        {
            // Handle the ChannelUriChanged event correctly for UI thread access
            if(Deployment.Current.Dispatcher.CheckAccess()) DoOnChannelUriChanged();
            else Deployment.Current.Dispatcher.BeginInvoke(DoOnChannelUriChanged);
        }

        /// <summary>Handles the MPNS ChannelError event, rasied when there's some serious MPNS problem</summary>
        /// <param name="sender">The object that raised the event</param>
        /// <param name="e">Details of the error</param>
        private void OnChannelError(object sender, NotificationChannelErrorEventArgs e)
        {
            // All the errors handled here should be considered serious enough for the user to quit the app
            // using the hardware back button

            if(e == null)
            {
                Deployment.Current.Dispatcher.BeginInvoke(() => MessageBoxHelper.Show(
                    Strings.Get("MpnsChannelError_Unknown"), 
                    Strings.Get("MpnsChannelError_UnknownTitle"), 
                    false));

                return;
            }

            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                switch(e.ErrorType)
                {
                    case ChannelErrorType.ChannelOpenFailed:
                        // Notes on ChannelErrorType.ChannelOpenFailed (error code -2129589901).
                        //
                        // I saw this error during development on the Lumia 520 device (but not on the 920 device or the
                        // emulators). It happens after calling the Open() on the HttpNotificationChannel in our Connect() 
                        // method. It seems that particular devices suddenly get this problem (it's like their MPNS
                        // connection status gets "locked" somehow). In my case, resetting (to factory default, then
                        // restoring settings from backup) cured the problem. So, it's not a genuine MPNS or RoundUp
                        // issue. But one to be aware of in case other users get it (i.e. we can point user to a
                        // support article).
                        //
                        // This error can also be caused by the device not having the date and time correctly set.
                        // 
                        // See the fowllowing posts for details:
                        // http://social.msdn.microsoft.com/Forums/wpapps/en-US/0c9132f7-9faa-423c-8bf5-9d585c83838c/cant-open-push-channel-all-the-suddenhelp
                        // http://stackoverflow.com/questions/5154135/httpnotificationchannel-open-throwing-invalidoperationexception-failed-to-op

                        MessageBoxHelper.Show(Strings.Get("MpnsChannelError_ChannelOpenFailed"), Strings.Get("MpnsChannelErrorTitle"), false);
                        break;

                    case ChannelErrorType.NotificationRateTooHigh:
                        // The rate at which our Azure scripts are requesting notifications is too high
                        MessageBoxHelper.Show(Strings.Get("MpnsChannelError_NotificationRateTooHigh"), Strings.Get("MpnsChannelError_NotificationRateTooHighTitle"), false);
                        break;

                    case ChannelErrorType.PowerLevelChanged:
                        // The power level in the device dropped to the point where it can't use MPNS
                        MessageBoxHelper.Show(Strings.Get("MpnsChannelError_PowerLevelChanged"), Strings.Get("MpnsChannelError_PowerLevelChangedTitle"), false);
                        break;

                    case ChannelErrorType.PayloadFormatError:
                    case ChannelErrorType.MessageBadContent:
                    case ChannelErrorType.Unknown:
                        // General error
                        MessageBoxHelper.Show(
                            Strings.Get("MpnsChannelError_Unknown"), 
                            Strings.Get("MpnsChannelError_UnknownTitle"), 
                            false);

                        break;
                }
            });
        }

        /// <summary>
        /// Handles the MPNS ChannelDisconnected event. This event is raised when the MPNS connection 
        /// status changes to disconnected. This can happen after a period of inactivity.
        /// If we're an inviter we immediately ask the MPNS to re-connect. If we're an invitee we'll
        /// request re-connection (via MpnsChannelHelper.StartWaiting()) the next time an operation
        /// requires the MPNS
        /// </summary>
        /// <param name="sender">The object that raised the event</param>
        /// <param name="e">Not used (will be null)</param>
        private void OnChannelDisconnected(object sender, EventArgs e)
        {
            // If we're an invitee, we request re-connection (via MpnsChannelHelper.StartWaiting()) 
            // the next time an operation requires the MPNS
            if(IsInvitee) return;  

            InitMpns();  // Re-init MPNS
        }


        /// <summary>Handles the MPNS PropertyChanged event</summary>
        private void MpnsServiceOnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if(string.Compare(args.PropertyName, "Connected", StringComparison.Ordinal) == 0)
            {
                // We need to make sure we're running on the UI thread or we'll get data binding
                // exceptions. This is caused because the HttpNotificationChannel.ChannelUriUpdated
                // event is raised on a different thread from the UI
                if(Deployment.Current.Dispatcher.CheckAccess()) MpnsServiceConnected = _mpnsService.Connected;
                else Deployment.Current.Dispatcher.BeginInvoke(() => MpnsServiceConnected = _mpnsService.Connected);
            }
        }

        /// <summary>Handles the MPNS ChannelOpen event (called from OnChannelOpen)</summary>
        private async void DoOnChannelUriChanged()
        {
            // A channel uri update can happen when we have a live session
            try
            {
                // Need to make sure that our state has been restored before attempting to see if
                // we need to update the channel uri associated with the device in the Azure mobile
                // services Session or Invitee table
                if(StateHasBeenRestored) await DoCompleteOnChannelUriChanged();
                else this.StateRestored += OnStateRestored;  // Wait until state's been restored
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error waiting for state to restore before updating Channel URI", new StackFrame(0, true));
            }
        }

        private async void OnStateRestored(object sender, EventArgs eventArgs)
        {
            try
            {
                StateRestored -= OnStateRestored;
                await DoCompleteOnChannelUriChanged();                
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error on completion of wait for state to restore (before updating Channel URI)", new StackFrame(0, true));
            }
        }

        /// <summary>Completes the process of changing the channel uri associated with the device in the appropriate Azure table</summary>
        private async Task DoCompleteOnChannelUriChanged()
        {
            // A channel uri update can happen when we have a live session. 
            // If the session isn't live we just ignore the event. If it happens when we have live open session:
            // For inviter ==> UPDATE the row = session ID in the Azure Session table
            // For invitee ==> UPDATE the row = session ID && invitee ID in the Azure Invitee table

            try
            {
                // Need to make sure that our state has been restored before attempting to see if
                // we need to update the channel uri associated with the device in the Azure mobile
                // services Session or Invitee table

                if(!SessionTypeHasBeenSet) return;  // If session type has not been set it's not a live session (nothing to update in Azure)

                RoundUpServiceOperationResult result;
                if(IsInviter)
                {
                    Logger.Log("MPNS Channel URI has changed for a live inviter session. Will update Azure Session table");

                    if(!await CheckNetworkAvailable())
                    {
                        MessageBoxHelper.Show(Strings.Get("NoNetworkConnection"), string.Empty, false);
                        return;
                    }

                    result = await _roundUpService.UpdateInviterChannelUriAsync(
                        SessionId,
                        _mpnsService.ChannelUri.ToString(),
                        InviterShortDeviceId,
                        _locationService.CurrentLocation.Latitude,
                        _locationService.CurrentLocation.Longitude,
                        SettingsAlias,
                        string.Empty);
                }
                else
                {
                    Logger.Log("MPNS Channel URI has changed for a live invitee session. Will update Azure Invitee table");

                    if(!await CheckNetworkAvailable())
                    {
                        MessageBoxHelper.Show(Strings.Get("NoNetworkConnection"), string.Empty, false);
                        return;
                    }

                    result = await _roundUpService.UpdateInviteeChannelUriAsync(
                        InviteeId,
                        SessionId,
                        _mpnsService.ChannelUri.ToString(),
                        _locationService.CurrentLocation.Latitude,
                        _locationService.CurrentLocation.Longitude,
                        InviterShortDeviceId,
                        SettingsAlias,
                        string.Empty);
                }

                if(result == RoundUpServiceOperationResult.OperationSuccess) Logger.Log("MPNS Channel URI successfully updated");
                else Logger.Log("Unable to update Channel URI", new StackFrame(0, true));
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error updating Channel URI", new StackFrame(0, true));
            }
        }

        /// <summary>Marks the session as completed</summary>
        private async void CloseSession()
        {
            // Are we waiting for the previous operation to complete?
            if(!OperationHasCompleted) return;
            OperationHasCompleted = false;  // Start a new operation

            if(_mpnsChannelHelper.WaitingForMpnsChannelUri) return;
            try
            {
                // If the MPNS channel isn't connected, wait until it is (the call to DoCompleteCloseSession is in the handler)
                if (!_mpnsService.Connected)
                {
                    Logger.Log("Start wait for MPNS ChannelOpen (CloseSession)");

                    ProgressBarText = Strings.Get("WaitingForPushNotificationService");
                    ShowProgressBar = true;

                    _mpnsChannelHelper.StartWaiting(DoCompleteCloseSession, MpnsWaitTimedout);
                }
                else await DoCompleteCloseSession();
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error closing session", new StackFrame(0, true));
                ShowProgressBar = false;
                _mpnsChannelHelper.WaitingForMpnsChannelUri = false;
                OperationHasCompleted = true;
            }
        }

        /// <summary>Completes processing of CloseSession. Do not call this method directly - call CloseSession</summary>
        private async Task DoCompleteCloseSession()
        {
            try
            {
                if(!await CheckNetworkAvailable()) return;  // No network connection - non-critical at this point

                ProgressBarText = Strings.Get("ProgressBarTextCloseSession");  // "closing roundup session"
                ShowProgressBar = true;

                var result = await _roundUpService.CloseSessionAsync(
                    SessionId, 
                    _mpnsService.ChannelUri.ToString(), 
                    CurrentLocation.Latitude, 
                    CurrentLocation.Longitude, 
                    InviterShortDeviceId, 
                    SettingsAlias, 
                    string.Empty);

                switch(result)
                {
                    case RoundUpServiceOperationResult.OperationSuccess:
                        break;

                    case RoundUpServiceOperationResult.MpnsNotificationLimitExceeded:
                        Logger.Log("MPNS notification limit exceeded", new StackFrame(0, true));

                        MessageBoxHelper.Show(Strings.Get("MpnsLimitExceeded"), Strings.Get("MpnsLimitExceededTitle"), false);
                        break;

                    default:
                        Logger.Log("RoundUpService.CloseSessionAsync() returned an error", new StackFrame(0, true));
                        break;
                }
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error closing session", new StackFrame(0, true));
            }
            finally
            {
                ShowProgressBar = false;
                OperationHasCompleted = true;
            }
        }

        /// <summary>
        /// Start a new session by registering with the RoundUp service as an inviter. If a session is already 
        /// active, or the user is an invitee, an InvalidOperationException is thrown
        /// </summary>
        /// <param name="obj">Not used</param>
        private async void DoInviteCommand(object obj)
        {
            try
            {
                // Hide the share panel
                if(ShowShareUI) ShowShareUI = false;

                // How does the user want to share their location?
                // This value will be used once the new session has been created on Azure (in the case where the invitee has roundup)
                // or immediately (if the invitee doesn't have roundup)
                if(obj != null)
                {
                    var shareBy = int.Parse(obj.ToString());
                    switch(shareBy)
                    {
                        case 0:
                            // SMS
                            ShareLocationBy = ShareLocationByInviteCode ? ShareLocationOption.InviteCodeViaSms : ShareLocationOption.BingMapViaSms;
                            break;

                        case 1:
                            // Email
                            ShareLocationBy = ShareLocationByInviteCode ? ShareLocationOption.InviteCodeViaEmail : ShareLocationOption.BingMapViaEmail;
                            break;

                        case 2:
                            // Clipboard
                            ShareLocationBy = ShareLocationByInviteCode ? ShareLocationOption.InviteCodeViaClipboard : ShareLocationOption.BingMapViaClipboard;
                            break;
                    }
                }
                else ShareLocationBy = ShareLocationOption.InviteCodeViaSms;

                // Does the invitee NOT have roundup?
                if(ShareLocationBy == ShareLocationOption.BingMapViaSms ||
                   ShareLocationBy == ShareLocationOption.BingMapViaEmail ||
                   ShareLocationBy == ShareLocationOption.BingMapViaClipboard)
                {
                    ShareLocationViaWebCommand.Execute(null);
                    return;
                }

                // Do we already have a live session? If so, just share the roundup invite code by sms, email or clipboard
                if(IsLiveSession)
                {
                    if(IsInvitee)
                    {
                        // An invitee on a live session can't create a new session
                        MessageBoxHelper.Show(Strings.Get("CantCreateNewSessionWhenInvitee"), Strings.Get("CantCreateNewSessionWhenInviteeTitle"), false);
                        return;
                    }

                    ShareLocationViaInviteCode(ShareLocationBy);
                    return;
                }

                // Are all the key services on?
                if(!AllKeyServicesAreOn()) 
                {
                    ShowKeyServicesOffMessage();
                    return;
                }

                // Are we waiting for the previous operation to complete?
                if(!OperationHasCompleted) return;
                OperationHasCompleted = false;  // Start a new operation

                // Trial expired?
                if(_storeService.IsTrialLicense && _storeService.TrialHasExpired)
                {
                    MessageBoxHelper.Show(Strings.Get("TrialHasExpired"), Strings.Get("TrialHasExpiredTitle"), false);
                    OperationHasCompleted = true;
                    return;
                }

                if(_mpnsChannelHelper.WaitingForMpnsChannelUri || IsInviter || IsInvitee) return;

                // Don't allow a new session to start if we don't have a firm location fix on the inviter
                if(!LocationServiceConnected || 
                    RoundUpPointLocation == null || 
                    (RoundUpPointLocation.Latitude.CompareTo(0) == 0 && RoundUpPointLocation.Longitude.CompareTo(0) == 0))
                {
                    _locationService.Dump();
                    MessageBoxHelper.Show(Strings.Get("NoStartNewSessionNoLocation"), Strings.Get("NoStartNewSessionNoLocationTitle"), false);
                    OperationHasCompleted = true;
                    return;
                }

                _locationService.IsWalking = true;
                _locationService.ResetMovementThresholdsToDefaults();  // Set the movement thresholds to their defaults

                if(!await CheckNetworkAvailable())
                {
                    MessageBoxHelper.Show(Strings.Get("NoNetworkConnection"), string.Empty, false);
                    OperationHasCompleted = true;
                    return;
                }

                // If the MPNS channel isn't connected, wait until it is (the call to DoCompleteInviteCommand is in the handler)
                if(!_mpnsService.Connected)
                {
                    Logger.Log("Start wait for MPNS ChannelOpen (DoInviteCommand)");

                    ProgressBarText = Strings.Get("WaitingForPushNotificationService");
                    ShowProgressBar = true;

                    _mpnsChannelHelper.StartWaiting(DoCompleteInviteCommand, MpnsWaitTimedout);
                }
                else await DoCompleteInviteCommand();
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                ShowProgressBar = false;
                _mpnsChannelHelper.WaitingForMpnsChannelUri = false;
                OperationHasCompleted = true;
            }
        }

        /// <summary>
        /// Should not be called directly: call DoInviteCommand, which will call this method.
        /// Completes the process of creating a new session by registering with the RoundUp service as an inviter. 
        /// If a session is already active, or the user is an invitee, an InvalidOperationException is thrown.
        /// All services must be connected before this method is called as no checks are made as to availability.
        /// </summary>
        private async Task DoCompleteInviteCommand()
        {
            try
            {
                ProgressBarText = Strings.Get("ProgressBarTextCreateSession");  // "creating roundup session"
                ShowProgressBar = true;

                IsInviter = true;  // Flag we're an inviter. Processing is completed in OnMpnsPushNotification (see SessionStarted)

                var result = await _roundUpService.RegisterAsInviterAsync(
                    _mpnsService.ChannelUri.ToString(), // Channel URI
                    RoundUpPointLocation.Latitude,      // Latitude of RoundUp location
                    RoundUpPointLocation.Longitude,     // Longitude of RoundUp location
                    ShortDeviceId,                      // Short device id - this becomes the InviterShortDeviceId
                    SettingsAlias,                      // Name (alias) of inviter
                    null);                              // Address of RoundUp location

                switch(result.Result)
                {
                    case RoundUpServiceOperationResult.OperationSuccess:
                        ShowProgressBar = false;

                        if(result.SessionId == -1) throw new Exception("Unable to start new session. Invalid SessionId returned by Azure");
                        SessionId = result.SessionId;
                        InviterShortDeviceId = ShortDeviceId;

                        Logger.Log("New session started: SessionId = " + SessionId.ToString(CultureInfo.InvariantCulture));

                        // Manually add the "SessionStarted" notification to our MPNS object
                        var notification = new RoundUpNotification
                        {
                            id = -1,
                            Recipient = 0,
                            SessionId = result.SessionId,
                            InviteeId = -1,
                            MessageId = "SessionStarted",
                            Data = "",
                            ShortDeviceId = InviterShortDeviceId,
                            Latitude = RoundUpPointLocation.Latitude,
                            Longitude = RoundUpPointLocation.Longitude
                        };

                        _mpnsService.Notifications.Add(notification);

                        // Create text that the inviter can send (i.e. via SMS, Email, etc.) to invite 
                        // others to meet at the RoundUp point
                        InviteCodeText = InviteCodeHelper.CreateInviteCodeText(
                            SessionId,
                            InviterShortDeviceId,
                            SettingsAlias);

                        SessionStatus = SessionStatusValue.SessionStarted;

                        // Tell the user a new session has been created (a new Session row has been inserted and 
                        // we've received the session id, etc.)
                        MessageBoxHelper.Show(Strings.Get("NewSessionSuccess"), Strings.Get("NewSessionSuccessTitle"), SettingsToastOn);

                        // Allow the user to share the invite code code SMS, Email, etc.
                        ShareLocationViaInviteCode(ShareLocationBy);

                        RefreshFlipTile();
                        break;

                    case RoundUpServiceOperationResult.MpnsNotificationLimitExceeded:
                        IsInviter = false;
                        ShowProgressBar = false;
                        Logger.Log("MPNS notification limit exceeded", new StackFrame(0, true));
                        MessageBoxHelper.Show(Strings.Get("MpnsLimitExceeded"), Strings.Get("MpnsLimitExceededTitle"), false);
                        break;

                    default:
                        IsInviter = false;
                        ShowProgressBar = false;
                        Logger.Log("RoundUpService.RegisterAsInviter() returned an error", new StackFrame(0, true));
                        MessageBoxHelper.Show(Strings.Get("CannotStartNewSession"), Strings.Get("CannotStartNewSessionTitle"), SettingsToastOn);
                        break;
                }
            }
            catch(Exception ex)
            {
                IsInviter = false;
                ShowProgressBar = false;
                Logger.Log(ex, new StackFrame(0, true));
                MessageBoxHelper.Show(Strings.Get("CannotStartNewSession"), Strings.Get("CannotStartNewSessionTitle"), false);
            }
            finally
            {
                OperationHasCompleted = true;
            }
        }

        /// <summary>Starts the process of an invitee accepting an invite (displays the Invite code entry form)</summary>
        /// <param name="obj">Not used</param>
        private void DoStartAcceptInviteCommand(object obj)
        {
            try
            {
                if(!AllKeyServicesAreOn())
                {
                    ShowKeyServicesOffMessage();
                    return;
                }

                if (IsInviter) throw new InvalidOperationException("Cannot start new session. User already registered as an inviter");
                if (IsInvitee) throw new InvalidOperationException("Cannot start new session. User already registered as an invitee");

                // Don't allow the user to accept an invite if we don't have a firm location fix on their location
                if(!LocationServiceConnected) _locationService.Connect();
                if(CurrentLocation == null || (CurrentLocation.Latitude.CompareTo(0) == 0 && CurrentLocation.Longitude.CompareTo(0) == 0))
                {
                    MessageBoxHelper.Show(Strings.Get("NoStartNewSessionNoLocation"), Strings.Get("NoStartNewSessionNoLocationTitle"), false);
                    return;
                }

                if( HasStartUpParams && 
                    InviteCodeHelper.LaunchInviteCode != null &&
                    InviteCodeHelper.LaunchInviteCode.SessionId != -1)
                {
                    InviteCodeText = InviteCodeHelper.CreateInviteCodeText(
                        InviteCodeHelper.LaunchInviteCode.SessionId,
                        InviteCodeHelper.LaunchInviteCode.InviterShortDeviceId,
                        InviteCodeHelper.LaunchInviteCode.InviterAlias,
                        true);
                }

                ShowPanelOverlay(PanelOverlay.AcceptInvite, true);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                MessageBoxHelper.Show(Strings.Get("CannotStartInviteeSession"), Strings.Get("CannotStartInviteeSessionTitle"), false);

                ShowAcceptInviteUI = false;
            }
        }

        /// <summary>Allows the user to cancel-out of the prompt for the invite code</summary>
        /// <param name="obj">Not used</param>
        private void DoCancelAcceptInviteCommand(object obj)
        {
            ShowPanelOverlay(PanelOverlay.AcceptInvite, false);
        }

        /// <summary>
        /// Accept an invitation (initiated by the user tapping the accept button on the invite code entry form). 
        /// A notification is sent to the inviter
        /// </summary>
        /// <param name="obj">0 == walk, 1 == drive</param>
        private async void DoCompleteAcceptInviteCommand(object obj)
        {
            try
            {
                var tmp = obj as string;
                IsWalking = (string.IsNullOrEmpty(tmp) || tmp.StartsWith("walk"));

                _locationService.IsWalking = IsWalking;  // Tell the location service if we're walking or driving
                _locationService.ResetMovementThresholdsToDefaults();  // Set the movement thresholds to their defaults initially

                // Parse the invite code text
                CurrentInviteCode = InviteCodeHelper.Parse(InviteCodeText);
                if (CurrentInviteCode == null) return;

                await AcceptInvite();
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary>
        /// Continues the process of accepting an invite, which was initiated by the user tapping the accept button
        /// on the invite code entry form
        /// </summary>
        private async Task AcceptInvite()
        {
            // Are we waiting for the previous operation to complete?
            if(!OperationHasCompleted)
            {
                Logger.Log("Cannot accept invite. Previous operation has not completed");
                return;
            }

            OperationHasCompleted = false;  // Start a new operation
            
            // Trial expired?
            if(_storeService.IsTrialLicense && _storeService.TrialHasExpired)
            {
                MessageBoxHelper.Show(Strings.Get("TrialHasExpired"), Strings.Get("TrialHasExpiredTitle"), false);
                OperationHasCompleted = true;
                return;
            } 
            
            HasStartUpParams = false;

            // Get the RoundUp service to register us as an invitee to the session
            // This inserts a row in the Azure Invitee table, updates the Session table, and
            // sends notifications to both the inviter and invitee

            try
            {
                if(!LocationServiceConnected ||
                    _mpnsChannelHelper.WaitingForMpnsChannelUri || 
                    IsInviter || 
                    IsInvitee || 
                    CurrentInviteCode == null || 
                    CurrentInviteCode.SessionId == -1 || 
                    string.IsNullOrEmpty(CurrentInviteCode.InviterShortDeviceId))
                {
                    OperationHasCompleted = true;
                    return;
                }

                // Save the inviter's short device id - we'll need this is subsequent calls
                // to the Azure service as an additional "security key"
                InviterShortDeviceId = CurrentInviteCode.InviterShortDeviceId;

                if(!await CheckNetworkAvailable())
                {
                    MessageBoxHelper.Show(Strings.Get("NoNetworkConnection"), string.Empty, false);
                    OperationHasCompleted = true;
                    return;
                }

                // If the MPNS channel isn't connected, wait until it is and then proceed
                if(!_mpnsService.Connected)
                {
                    Logger.Log("Start wait for MPNS ChannelOpen (AcceptInvite)");

                    ProgressBarText = Strings.Get("WaitingForPushNotificationService"); 
                    ShowProgressBar = true;

                    _mpnsChannelHelper.StartWaiting(DoCompleteAcceptInvite, MpnsWaitTimedout);
                }
                else await DoCompleteAcceptInvite();
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                MessageBoxHelper.Show(Strings.Get("CannotStartInviteeSession"), Strings.Get("CannotStartInviteeSessionTitle"), false);

                ShowProgressBar = false;
                _mpnsChannelHelper.WaitingForMpnsChannelUri = false;
                ShowAcceptInviteUI = false;
                OperationHasCompleted = true;
            }        
        }

        /// <summary>
        /// Completes processing of acceptance of an invitation. Do not call this method directly - call 
        /// DoCompleteAcceptInviteCommand, which will result in a call to this method
        /// </summary>
        private async Task DoCompleteAcceptInvite()
        {
            // Get the RoundUp service to register us as an invitee to the session
            // This inserts a row in the Azure Invitee table, updates the Session table, and
            // sends notifications to both the inviter and invitee

            try
            {
                ProgressBarText = Strings.Get("ProgressBarTextJoiningSession");  // "joining roundup session"
                ShowProgressBar = true;

                // Flag we're an invitee. Processing is completed in OnMpnsPushNotification (see InviteeHasAccepted).
                // We set the flag here rather than when the op has successfully (i.e. we have a
                // RoundUpServiceOperationResult.OperationSuccess return from RegisterAsInviteeAsync) is that
                // sometimes the notification from MPNS (via the Azure Invitee Insert script) arrives BEFORE
                // RegisterAsInviteeAsync returns (and continued processing in OnMpnsPushNotification relies
                // on the fact the we're a (potential) invitee)
                IsInvitee = true;  

                var result = await _roundUpService.RegisterAsInviteeAsync(
                    CurrentInviteCode.SessionId,        // Session id (forms part of the invite code)
                    _mpnsService.ChannelUri.ToString(), // Channel URI for the invitee
                    CurrentLocation.Latitude,           // Latitude of invitee
                    CurrentLocation.Longitude,          // Longitude of the invitee
                    InviterShortDeviceId,               // The *inviter's* short device id (forms part of the invite code)
                    SettingsAlias,                      // Name (alias) of invitee
                    null);                              // Address of the invitee's current location

                switch (result.Result)
                {
                    case RoundUpServiceOperationResult.OperationSuccess:
                        if(result.InviteeId == -1) throw new Exception("Unable to join session. Invalid InviteeId returned by Azure");

                        ShowProgressBar = false;
                        IsInvitee = true;
                        SessionStatus = SessionStatusValue.SessionActive;
                        InviteeStatus = InviteeStatusValue.InviteeHasAccepted;
                        SessionId = result.SessionId;  // Save our session id
                        InviteeId = result.InviteeId;  // Save our invitee id
                        ShowRoundUpPointLocation = true;

                        // Manually add the "InviteeHasAccepted" notification to our MPNS object
                        var notification = new RoundUpNotification
                        {
                            id = -1,
                            Recipient = 1,
                            SessionId = result.SessionId,
                            InviteeId = result.InviteeId,
                            MessageId = "InviteeHasAccepted",
                            Data = result.Data,  // Inviter name
                            ShortDeviceId = InviterShortDeviceId,
                            Latitude = result.Latitude,
                            Longitude = result.Longitude
                        };

                        _mpnsService.Notifications.Add(notification);

                        // We're an invitee. We need to pick up the location of the RoundUp point set by
                        // the inviter and display it on the map
                        RoundUpPointLocation = new GeoCoordinate(result.Latitude, result.Longitude);

                        // Show the user that we're now connected to the session
                        var s = Strings.Get("AcceptInviteSuccess").Replace("{0}", CurrentInviteCode.InviterAlias);
                        MessageBoxHelper.Show(s, Strings.Get("AcceptInviteSuccessTitle"), SettingsToastOn);

                        // At this point we know the start and end point locations for the invitee.
                        // Have we already arrived (before we started)?
                        if(await AreWeThereYet()) return;

                        // Is it a long journey that's likely to result in a large number of MPNS notifications?
                        // If so, we adjust the movement threshold values in the location service
                        // Estimated number of notifications = Distance to roundup point / Default movement threshold
                        // If required, New movement threshold = Distance / MaxSafeMpnsNotificationLimit

                        if(RoundUpPointInfo != null && RoundUpPointInfo.DistanceToRoundUpPoint > 0)
                        {
                            var longJourney = false;
                            double estimatedNotifications;

                            if(IsWalking)
                            {
                                estimatedNotifications = RoundUpPointInfo.DistanceToRoundUpPoint / _locationService.MovementThresholdWalking;
                                if(estimatedNotifications > MaxSafeMpnsNotificationLimit)
                                {
                                    _locationService.MovementThresholdWalking = Math.Round(RoundUpPointInfo.DistanceToRoundUpPoint/MaxSafeMpnsNotificationLimit, 2);
                                    longJourney = true;
                                }
                            }
                            else
                            {
                                estimatedNotifications = RoundUpPointInfo.DistanceToRoundUpPoint/_locationService.MovementThresholdDriving;
                                if(estimatedNotifications > MaxSafeMpnsNotificationLimit)
                                {
                                    _locationService.MovementThresholdDriving = Math.Round(RoundUpPointInfo.DistanceToRoundUpPoint/MaxSafeMpnsNotificationLimit, 2);
                                    longJourney = true;
                                }
                            }

                            if(longJourney)
                            {
                                // Warn the user that this is a long journey.
                                //
                                // "You are attempting to use roundup for a long journey of {0} km.
                                // Too avoid sending a large number of push notifications, progress updates 
                                // will only be broadcast to the person who invited you every {1} km."

                                var dist = Math.Round(RoundUpPointInfo.DistanceToRoundUpPoint/1000, 2);
                                var updates = Math.Round(_locationService.MovementThresholdDriving/1000, 2);
                                var msg = Strings.Get("LongJourney").Replace("{0}", dist.ToString(CultureInfo.InvariantCulture));
                                msg = msg.Replace("{1}", updates.ToString(CultureInfo.InvariantCulture));
                                MessageBoxHelper.Show(msg, Strings.Get("LongJourneyTitle"), false);
                            }
                        }

                        // Tell the user they're connected to the inviter. The name of the inviter is in args.Notification.Data 
                        if(SettingsToastOn)
                            MessageBoxHelper.ShowToast(
                                string.Format("{0} {1}", Strings.Get("NewInviteeSuccessToast"), result.Data),
                                Strings.Get("NewInviteeSuccessToastTitle"));

                        Logger.Log(string.Format("I accepted an invite from {0}. SessionId = {1}. RoundUpPointLocation = lat {2}, long {3}",
                            result.Data,
                            result.SessionId,
                            result.Latitude,
                            result.Longitude));

                        RefreshFlipTile();
                        break;

                    case RoundUpServiceOperationResult.MpnsNotificationLimitExceeded:
                        IsInvitee = false;
                        ShowProgressBar = false;
                        Logger.Log("RegisterAsInviteeAsync: MPNS notification limit exceeded");
                        MessageBoxHelper.Show(Strings.Get("MpnsLimitExceeded"), Strings.Get("MpnsLimitExceededTitle"), false);
                        break;

                    case RoundUpServiceOperationResult.SessionDoesNotExist:
                        IsInvitee = false;  
                        ShowProgressBar = false;
                        Logger.Log("RegisterAsInviteeAsync: Session does not exist");
                        MessageBoxHelper.Show(Strings.Get("SessionDoesNotExist"), Strings.Get("SessionDoesNotExistTitle"), SettingsToastOn);
                        break;

                    case RoundUpServiceOperationResult.SessionIsNotAlive:
                        IsInvitee = false;  
                        ShowProgressBar = false;
                        Logger.Log("RegisterAsInviteeAsync: Session is not alive");
                        MessageBoxHelper.Show(Strings.Get("SessionIsNotAlive"), Strings.Get("SessionIsNotAliveTitle"), SettingsToastOn);
                        break;

                    case RoundUpServiceOperationResult.InvalidInviterShortDeviceId:
                        IsInvitee = false;  
                        ShowProgressBar = false;
                        Logger.Log("RegisterAsInviteeAsync: Invalid inviter short device id");
                        MessageBoxHelper.Show(Strings.Get("BadInviteCode"), Strings.Get("BadInviteCodeTitle"), SettingsToastOn);
                        break;

                    case RoundUpServiceOperationResult.TooManyInvitees:
                        IsInvitee = false;  
                        ShowProgressBar = false;
                        Logger.Log("RegisterAsInviteeAsync: Too many invitees");
                        MessageBoxHelper.Show(Strings.Get("TooManyInvitees"), Strings.Get("TooManyInviteesTitle"), SettingsToastOn);
                        break;

                    default:
                        IsInvitee = false;  
                        ShowProgressBar = false;
                        Logger.Log("RegisterAsInviteeAsync: Default error result");
                        MessageBoxHelper.Show(Strings.Get("CannotStartInviteeSession"), Strings.Get("CannotStartInviteeSessionTitle"), SettingsToastOn);
                        break;
                }
            }
            catch (Exception ex)
            {
                IsInvitee = false;
                ShowProgressBar = false;
                Logger.Log(ex, "RegisterAsInviteeAsync exception: ", new StackFrame(0, true));
                MessageBoxHelper.Show(Strings.Get("CannotStartInviteeSession"), Strings.Get("CannotStartInviteeSessionTitle"), false);
            }
            finally
            {
                ShowAcceptInviteUI = false;
                OperationHasCompleted = true;
            }
        }

        /// <summary>Used by an inviter to cancel a session. A notification is sent to all invitees that have accepted</summary>
        /// <param name="obj">Not used</param>
        private async void DoCancelSessionCommand(object obj)
        {
            try
            {
                // Are we waiting for the previous operation to complete?
                if(!OperationHasCompleted) return;
                OperationHasCompleted = false;  // Start a new operation

                if(IsInviter && SessionId == -1)
                {
                    // This can happen when we successfully created a new Session in the Azure table,
                    // but we didn't get an MPNS notification back containing the SessionId (rare)
                    var answer = MessageBoxHelper.Show(Strings.Get("CancelUnstartedSession"), Strings.Get("CancelSessionTitle"), MessageBoxButton.OKCancel);
                    if(answer == MessageBoxResult.Cancel)
                    {
                        OperationHasCompleted = true;
                        return;
                    } 
                    
                    ResetProperties();
                    return;
                }

                if( !IsInviter || 
                    !LocationServiceConnected ||
                    _mpnsChannelHelper.WaitingForMpnsChannelUri || 
                    SessionId == -1 || 
                    string.IsNullOrEmpty(InviterShortDeviceId))
                {
                    OperationHasCompleted = true;
                    return;
                }

                var result = MessageBoxHelper.Show(Strings.Get("CancelInviterSession"), Strings.Get("CancelSessionTitle"), MessageBoxButton.OKCancel);
                if(result == MessageBoxResult.Cancel)
                {
                    OperationHasCompleted = true;
                    return;
                }

                if(!await CheckNetworkAvailable())
                {
                    MessageBoxHelper.Show(Strings.Get("NoNetworkConnection"), string.Empty, false);
                    OperationHasCompleted = true;
                    return;
                }

                // If the MPNS channel isn't connected, wait until it is and then proceed
                if(!_mpnsService.Connected)
                {
                    Logger.Log("Start wait for MPNS ChannelOpen (CancelSession)");

                    ProgressBarText = Strings.Get("WaitingForPushNotificationService");
                    ShowProgressBar = true;

                    _mpnsChannelHelper.StartWaiting(DoCompleteCancelSession, MpnsWaitTimedout);
                }
                else await DoCompleteCancelSession();
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                OperationHasCompleted = true;
                ShowProgressBar = false;
                _mpnsChannelHelper.WaitingForMpnsChannelUri = false;
            } 
        }

        /// <summary>
        /// Completes processing of cancelling a session. Do not call this method directly - call 
        /// DoCancelSessionCommand, which will result in a call to this method
        /// </summary>
        private async Task DoCompleteCancelSession()
        {
            try
            {
                ProgressBarText = Strings.Get("ProgressBarTextCancelSession");  // "cancelling session"
                ShowProgressBar = true;

                var result = await _roundUpService.CancelSessionAsync(
                    SessionId,                              // Session id (forms part of the invite code)
                    InviterShortDeviceId,                   // The *inviter's* short device id (forms part of the invite code) 
                    SettingsAlias,                          // Inviter's name
                    _mpnsService.ChannelUri.ToString());    // Channel uri        

                Logger.Log("Session cancelled by inviter");

                switch(result)
                {
                    case RoundUpServiceOperationResult.OperationSuccess:
                        MessageBoxHelper.Show(Strings.Get("SessionHasBeenCancelled"), Strings.Get("SessionHasBeenCancelledTitle"), false);
                        break;

                    case RoundUpServiceOperationResult.MpnsNotificationLimitExceeded:
                        Logger.Log("MPNS notification limit exceeded", new StackFrame(0, true));
                        MessageBoxHelper.Show(Strings.Get("MpnsLimitExceeded"), Strings.Get("MpnsLimitExceededTitle"), false);
                        break;

                    default:

                        // There was an error cancelling the session. However, the user won't care about that at this point,
                        // so we just have to flag the current session as not re-startable
                        MessageBoxHelper.Show(Strings.Get("CannotCancelSession"), Strings.Get("CannotCancelSessionTitle"), false);
                        break;
                }

                ResetProperties();
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                MessageBoxHelper.Show(Strings.Get("CannotCancelSession"), Strings.Get("CannotCancelSessionTitle"), false);
            }
            finally
            {
                ShowProgressBar = false;
                OperationHasCompleted = true;
            }
        }

        /// <summary>Cancel a previously accepted invitation. A notification is sent to the inviter</summary>
        /// <param name="obj">Not used</param>
        private async void DoCancelAcceptedInvitationCommand(object obj)
        {
            try
            {
                // Are we waiting for the previous operation to complete?
                if(!OperationHasCompleted) return;
                OperationHasCompleted = false;  // Start a new operation

                if(IsInvitee && InviteeId == -1)
                {
                    // This can happen when we successfully insert a new Invitee in the Azure table,
                    // but we didn't get an MPNS notification (InviteeHasAccepted) back containing the InviteeId (rare)
                    var answer = MessageBoxHelper.Show(Strings.Get("CancelInviteeSession"), Strings.Get("CancelSessionTitle"), MessageBoxButton.OKCancel);
                    if(answer == MessageBoxResult.Cancel)
                    {
                        OperationHasCompleted = true;
                        return;
                    }

                    ResetProperties();  // Just reset the UI as we can't inform the Azure backend of any changes (we don't have the InviteeId)
                    return;
                }

                if( !IsInvitee || 
                    !LocationServiceConnected ||
                    _mpnsChannelHelper.WaitingForMpnsChannelUri || 
                    InviteeId == -1 || 
                    SessionId == -1 || 
                    string.IsNullOrEmpty(InviterShortDeviceId))
                {
                    OperationHasCompleted = true;
                    return;
                }

                var result = MessageBoxHelper.Show(Strings.Get("CancelInviteeSession"), Strings.Get("CancelSessionTitle"), MessageBoxButton.OKCancel);
                if(result == MessageBoxResult.Cancel)
                {
                    OperationHasCompleted = true;
                    return;
                }

                if(!await CheckNetworkAvailable())
                {
                    MessageBoxHelper.Show(Strings.Get("NoNetworkConnection"), string.Empty, false);
                    OperationHasCompleted = true;
                    return;
                }

                // If the MPNS channel isn't connected, wait until it is and then proceed
                if(!_mpnsService.Connected)
                {
                    Logger.Log("Start wait for MPNS ChannelOpen (CancelAcceptedInvitation)");

                    ProgressBarText = Strings.Get("WaitingForPushNotificationService");
                    ShowProgressBar = true;

                    _mpnsChannelHelper.StartWaiting(DoCompleteCancelAcceptedInvitation, MpnsWaitTimedout);
                }
                else await DoCompleteCancelAcceptedInvitation();
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                OperationHasCompleted = true;
                ShowProgressBar = false;
                _mpnsChannelHelper.WaitingForMpnsChannelUri = false;
            }          
        }

        /// <summary>
        /// Completes processing of cancelling an invitee's session. Do not call this method directly - call 
        /// DoCancelAcceptedInvitationCommand, which will result in a call to this method
        /// </summary>
        private async Task DoCompleteCancelAcceptedInvitation()
        {
            try
            {
                ProgressBarText = Strings.Get("ProgressBarTextCancelSession");  // "cancelling session"
                ShowProgressBar = true;

                var result = await _roundUpService.CancelInviteeSessionAsync(
                    InviteeId,                      // Invitee's id
                    CurrentInviteCode.SessionId,    // Session id (forms part of the invite code)
                    InviterShortDeviceId,           // The *inviter's* short device id (forms part of the invite code)
                    SettingsAlias);                 // Invitee's name

                Logger.Log("Session cancelled by invitee");

                switch(result)
                {
                    case RoundUpServiceOperationResult.OperationSuccess:
                        MessageBoxHelper.Show(Strings.Get("SessionHasBeenCancelled"), Strings.Get("SessionHasBeenCancelledTitle"), SettingsToastOn);
                        break;

                    case RoundUpServiceOperationResult.MpnsNotificationLimitExceeded:
                        Logger.Log("MPNS notification limit exceeded", new StackFrame(0, true));
                        MessageBoxHelper.Show(Strings.Get("MpnsLimitExceeded"), Strings.Get("MpnsLimitExceededTitle"), false);
                        break;

                    default:
                        // There was an error cancelling the invitee's session. However, the user won't care about that at this point,
                        // so we just have to flag the current session as not re-startable

                        MessageBoxHelper.Show(Strings.Get("CannotCancelSession"), Strings.Get("CannotCancelSessionTitle"), false);
                        break;
                }

                ResetProperties();
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                MessageBoxHelper.Show(Strings.Get("CannotCancelSession"), Strings.Get("CannotCancelSessionTitle"), false);
            }
            finally
            {
                ShowProgressBar = false;
                OperationHasCompleted = true;
            }
        }

        /// <summary>Inviter-only menu command. Send an invite by SMS</summary>
        /// <param name="obj">Not used</param>
        private void DoSendInviteSmsCommand(object obj)
        {
            try
            {
                if(string.IsNullOrEmpty(InviteCodeText))
                {
                    MessageBoxHelper.Show(Strings.Get("NoInviteCode"), Strings.Get("NoInviteCodeTitle"), false);
                    return;
                } 
                
                var sms = IocContainer.Get<ISmsComposeService>();
                sms.Show("", InviteCodeText);
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary>Inviter-only menu command. Send an invite by email</summary>
        /// <param name="obj">Not used</param>
        private void DoSendInviteEmailCommand(object obj)
        {
            try
            {
                if(string.IsNullOrEmpty(InviteCodeText))
                {
                    MessageBoxHelper.Show(Strings.Get("NoInviteCode"), Strings.Get("NoInviteCodeTitle"), false);
                    return;
                }

                var email = IocContainer.Get<IEmailComposeService>();
                email.Show(Strings.Get("InviteEmailSubject"), InviteCodeText);
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary>Menu item to allow the user to tap the map to select a new RoundUp point</summary>
        /// <param name="obj">Not used</param>
        private async void DoStartSetNewRoundUpPointCommand(object obj)
        {
            // *** Note: In version 1.0 we only shown the menu option to do this BEFORE the session starts/is active ***

            try
            {
                var result = MessageBoxHelper.Show(Strings.Get("SetNewRoundUpPointPrompt"), Strings.Get("SetNewRoundUpPointPromptTitle"), MessageBoxButton.OKCancel);
                if(result == MessageBoxResult.Cancel)
                {
                    // The current location of the inviter is the RoundUp point...
                    ShowRoundUpPointLocation = false;  
                    AllowRoundUpPointLocationChange = false;

                    // Do we need to update the Session and send out notifications that the RoundUp point has changed?
                    if( SessionStatus == SessionStatusValue.NotSet || RoundUpPointLocation.Equals(CurrentLocation)) 
                        return;  // Nothing's changed. No need to update/send notifications

                    // The RoundUp point is changing back to default current location of the inviter
                    await RequestChangeRoundUpPoint(CurrentLocation);

                    // For invitees this will get set in the OnMpnsPushNotification handler for the "RoundUpLocationChange" notification
                    RoundUpPointLocation = CurrentLocation;   
                    return;
                }

                AllowRoundUpPointLocationChange = true;  // Allow the user to tap the map to set the RoundUp point (see DoSetNewRoundUpPointCommand)
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }              
        }

        /// <summary>Inviter. Executed from the View's code-behind to set the actual new RoundUp point</summary>
        /// <param name="obj">The GeoCoordinate of the new RoundUp point</param>
        private async void DoSetNewRoundUpPointCommand(object obj)
        {
            if(!SettingsLocationServicesOn)
            {
                ShowKeyServicesOffMessage();
                return;
            }

            if(!AllowRoundUpPointLocationChange) return;  // Has the user asked to change location?

            try
            {
                AllowRoundUpPointLocationChange = false;

                var geoCoordinate = obj as GeoCoordinate;
                if(geoCoordinate == null) return;

                RoundUpPointLocation = geoCoordinate;
                ShowRoundUpPointLocation = true;
                
                // Do we need to update the Session and send out notifications that the RoundUp point has changed?
                // *** For version 1.0, SessionStatus == SessionStatusValue.NotSet will always be true because we ***
                // *** only show the meun item to allow changes when when the session's not started               ***
                if (SessionStatus == SessionStatusValue.NotSet || RoundUpPointLocation.Equals(CurrentLocation)) return; 

                await RequestChangeRoundUpPoint(RoundUpPointLocation);
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }            
        }

        /// <summary>
        /// Ad-hoc refresh of current device position. 
        /// Will update the CurrentLocation property and center the map on the current location
        /// </summary>
        /// <param name="obj">Not used</param>
        private async void DoRefreshCurrentLocationCommand(object obj)
        {
            try
            {
                if(!SettingsLocationServicesOn)
                {
                    ShowKeyServicesOffMessage();
                    return;
                }
                
                // Are we waiting for the previous operation to complete?
                if(!OperationHasCompleted) return;
                OperationHasCompleted = false; // Start a new operation

                var tmpNewLocation = await _locationService.GetCurrentPositionAsync();
                if(tmpNewLocation == null) return; // Don't overrwrite CurrentLocation with an invalid location

                HandleLocationChanged(new LocationUpdateEventArgs{ Location = tmpNewLocation, DeltaHasExceededThreshold = true });
            }
            catch(Exception ex) 
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
            finally
            {
                OperationHasCompleted = true;
            }
        }

        /// <summary>Inviter and Invitee. Turns on location tracking</summary>
        /// <param name="obj">Not used</param>
        private async void DoTurnLocationTrackingOnCommand(object obj)
        {
            if(_locationService == null || SettingsTrackCurrentLocation) return;

            try
            {
                SettingsTrackCurrentLocation = true;

                // Get an updated position fix
                CurrentLocation = await _locationService.GetCurrentPositionAsync();
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary>Inviter and Invitee. Turns off location tracking</summary>
        /// <param name="obj">Not used</param>
        private void DoTurnLocationTrackingOffCommand(object obj)
        {
            if (_locationService == null || !SettingsTrackCurrentLocation) return;
            SettingsTrackCurrentLocation = false;
        }

        private void DoPurchaseCommand(object obj)
        {
            // Has the user already purchased?
            if(_storeService.License == LicenseMode.Full) 
            {
                MessageBoxHelper.Show(Strings.Get("AlreadyPurchased"), Strings.Get("AlreadyPurchasedTitle"), false);
                return;
            }

            Logger.Log("User requested upgrade from trial - launching store");

            _storeService.Purchase();
        }

        private void DoRateAndReviewCommand(object obj)
        {
            Logger.Log("User requested rate and review - launching store");

            _storeService.RateAndReview();
        }

        private void DoShowMapControlPanelCommand(object obj)
        {
            if(!AllKeyServicesAreOn())
            {
                ShowKeyServicesOffMessage();
                return;
            } 
            
            ShowPanelOverlay(PanelOverlay.MapControls, !ShowMapControlPanel);
        }

        private void DoMapCartographicConverterCommand(object obj)
        {
            if(!AllKeyServicesAreOn())
            {
                ShowKeyServicesOffMessage();
                return;
            } 
            
            try 
            {
                var s = (string)obj;  // 0=Road, 1=Aerial, 2=Terrain, 3=Hybrid

                switch(s) 
                {
                    case "0":
                        MapMode = MapCartographicMode.Road;
                        Logger.Log("Setting MapCatographicMode to Road");
                        break;

                    case "1":
                        MapMode = MapCartographicMode.Aerial;
                        Logger.Log("Setting MapCatographicMode to Aerial");
                        break;

                    case "2":
                        MapMode = MapCartographicMode.Terrain;
                        Logger.Log("Setting MapCatographicMode to Terrain");
                        break;

                    case "3":
                        MapMode = MapCartographicMode.Hybrid;
                        Logger.Log("Setting MapCatographicMode to Hybrid");
                        break;

                    default:
                        MapMode = MapCartographicMode.Road;
                        Logger.Log("Setting MapCatographicMode to default (Road)");
                        break;
                }
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error converting integer value to map cartographic mode", new StackFrame(0, true));                
            }
        }

        /// <summary>Centers the map on an invitee's location</summary>
        /// <param name="obj">Invitee id</param>
        private void DoCenterMapOnInviteeCommand(object obj)
        {
            if(obj == null) return;

            var id = int.Parse(obj.ToString());
            var q = from i in Invitees where i.id == id select i;
            var inviteeToCenter = q.FirstOrDefault();
            if(inviteeToCenter == null) return;

            MapCenterLocation = inviteeToCenter.Location;

            // Raise an event so the view can show an animated sonar around the selected invitee
            OnInviteeSelected(new LocationUpdateEventArgs { DeltaHasExceededThreshold = true, Location = inviteeToCenter.Location});

            var msg = Strings.Get("InviteeIsCentered");  // {0} is centered on the map 
            msg = msg.Replace("{0}", inviteeToCenter.Name);
            if(SettingsToastOn) MessageBoxHelper.ShowToast(msg, Strings.Get("InviteeIsCenteredTitle"));
        }

        /// <summary>Centers the map on device's current location</summary>
        /// <param name="obj">Not used</param>
        private void DoCenterMapOnDeviceCommand(object obj)
        {
            if(!AllKeyServicesAreOn())
            {
                ShowKeyServicesOffMessage();
                return;
            } 
            
            if(CurrentLocation == null || (CurrentLocation.Latitude.CompareTo(0) == 0 && CurrentLocation.Longitude.CompareTo(0) == 0)) return;
            MapCenterLocation = CurrentLocation;
        }

        /// <summary>Checks for missed notifications</summary>
        /// <param name="obj">Not used</param>
        private async void DoSyncNotificationsCommand(object obj)
        {
            await ResendMissedNotificationsAsync();
        }

        /// <summary>Allows us to share the meetup location via a web browser</summary>
        /// <param name="obj"></param>
        private void DoShareLocationViaWebCommand(object obj)
        {
            // Don't try to share location if we don't have a firm location fix on the inviter
            if(!LocationServiceConnected || 
                RoundUpPointInfo == null || 
                (RoundUpPointInfo.Location.Latitude.CompareTo(0) == 0 && RoundUpPointInfo.Location.Longitude.CompareTo(0) == 0))
            {
                MessageBoxHelper.Show(Strings.Get("NoStartNewSessionNoLocation"), Strings.Get("NoStartNewSessionNoLocationTitle"), false);
                return;
            }

            var uri = Strings.Get("BingMapsUri");

            // The URI will be in the following format:
            // http://www.bing.com/maps/?v=2&cp={0}~{1}&lvl=18&sp=point.{0}_{1}_{2}_{3}
            // For example:
            // http://www.bing.com/maps/?v=2&cp=51.42912~-0.32924&lvl=18&sp=point.51.42912_-0.32924_37%20Cambridge%20Road_Meet%20here

            uri = uri.Replace("{0}", RoundUpPointInfo.Location.Latitude.ToString(CultureInfo.InvariantCulture));
            uri = uri.Replace("{1}", RoundUpPointInfo.Location.Longitude.ToString(CultureInfo.InvariantCulture));
            uri = uri.Replace("{2}", string.IsNullOrEmpty(RoundUpPointInfo.Address) ? Strings.Get("NoAddress") : Uri.EscapeDataString(RoundUpPointInfo.Address));
            uri = uri.Replace("{3}", Uri.EscapeDataString(Strings.Get("RoundUpLocationMarker")));

            // The final shareable text will be in the following format:
            //
            // {alias} is sharing a location with you via roundup and Bing maps:
            //
            // {bing maps uri}
            // 
            // For an even better experience, get roundup from the Windows Phone Store:
            // 
            // {app uri}

            var preamble = Strings.Get("BingMapsPreamble").Replace("{alias}", SettingsAlias);
            var shareText =  preamble + uri + Strings.Get("BingMapsPostamble") + Strings.Get("_AppLinkInStore");

            switch(ShareLocationBy)
            {
                case ShareLocationOption.BingMapViaClipboard:

                    Clipboard.SetText(shareText);
                    MessageBoxHelper.Show(Strings.Get("BingMapsViaClipboard"), Strings.Get("BingMapsViaXTitle"), false);
                    Logger.Log("The following map link has been added to the clipboard:\n\n" + shareText + "\n");
                    break;

                case ShareLocationOption.BingMapViaSms:

                    MessageBoxHelper.Show(Strings.Get("BingMapsViaSms"), Strings.Get("BingMapsViaXTitle"), false);
                    Logger.Log("The following map link has been generated:\n\n" + shareText + "\n");
                    var sms = IocContainer.Get<ISmsComposeService>();
                    sms.Show("", shareText);                
                    break;

                case ShareLocationOption.BingMapViaEmail:

                    MessageBoxHelper.Show(Strings.Get("BingMapsViaEmail"), Strings.Get("BingMapsViaXTitle"), false);
                    Logger.Log("The following map link has been generated:\n\n" + shareText + "\n");
                    var email = IocContainer.Get<IEmailComposeService>();
                    email.Show(Strings.Get("InviteEmailSubject"), shareText);                
                    break;
            }
        }

        private void ShareLocationViaInviteCode(ShareLocationOption shareLocationBy)
        {
            if(!IsLiveSession) return;

            switch(shareLocationBy)
            {
                case ShareLocationOption.BingMapViaClipboard:
                    Clipboard.SetText(InviteCodeText);
                    MessageBoxHelper.Show(Strings.Get("ShareViaClipboard"), Strings.Get("ShareViaClipboardTitle"), false);
                    Logger.Log("The following invite code has been added to the clipboard:\n\n" + InviteCodeText + "\n");
                    break;

                case ShareLocationOption.InviteCodeViaSms:
                    Logger.Log("The following invite code has been generated:\n\n" + InviteCodeText + "\n");
                    SendInviteSmsCommand.Execute(null);
                    break;

                case ShareLocationOption.BingMapViaEmail:
                    Logger.Log("The following invite code has been generated:\n\n" + InviteCodeText + "\n");
                    SendInviteEmailCommand.Execute(null); 
                    break;
            }
        }

        /// <summary>Shows the About view</summary>
        /// <param name="obj">Not used</param>
        private void DoShowAboutViewCommand(object obj)
        {
            var nav = new NavigationService();
            try
            {
                nav.NavigateTo(new Uri("/View/AboutView.xaml", UriKind.Relative));
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Unable to navigate to AboutView.xaml", new StackFrame(0, true));                
            }
        }

        /// <summary>Shows the Settings view</summary>
        /// <param name="obj">Not used</param>
        private void DoShowSettingsViewCommand(object obj)
        {
            var nav = new NavigationService();
            try
            {
                nav.NavigateTo(new Uri("/View/SettingsView.xaml", UriKind.Relative));
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Unable to navigate to SettingsView.xaml", new StackFrame(0, true));                
            }
        }

        /// <summary>Shows the Help view</summary>
        /// <param name="obj">Not used</param>
        private void DoShowHelpCommand(object obj)
        {
            var nav = new NavigationService();
            try
            {
                nav.NavigateTo(new Uri("/View/HelpView.xaml", UriKind.Relative));
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Unable to navigate to HelpView.xaml", new StackFrame(0, true));                
            }            
        }

        /// <summary>Show/hide the share panel</summary>
        /// <param name="obj">Either null or true/false</param>
        private void DoShowSharePanelCommand(object obj)
        {
            try
            {
                ShowPanelOverlay(PanelOverlay.Share, obj == null ? !ShowShareUI : bool.Parse(obj.ToString()));
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error handling DoShowSharePanelCommand", new StackFrame(0, true));                
            }
        }

        /// <summary>Show/hide the invitees panel</summary>
        /// <param name="obj">Either null or true/false</param>        
        private void DoShowInviteesPanelCommand(object obj)
        {
            try
            {
                ShowPanelOverlay(PanelOverlay.Invitees, obj == null ? !ShowInviteesUI : bool.Parse(obj.ToString()));
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error handling DoShowInviteesPanelCommand", new StackFrame(0, true));                
            }
        }

        /// <summary>Show/hide the directions panel</summary>
        /// <param name="obj">Either null or true/false</param>
        private void DoShowDirectionsPanelCommand(object obj)
        {
            try
            {
                ShowPanelOverlay(PanelOverlay.Directions, obj == null ? !ShowDirectionsUI : bool.Parse(obj.ToString()));
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error handling DoShowDirectionsPanelCommand", new StackFrame(0, true));                
            }
        }

        /// <summary>xxx</summary>
        /// <param name="obj"></param>
        private void DoMenuStateChangedCommand(object obj)
        {
            if(obj == null)
            {
                ShowMapLogo = false; 
                return;
            }
            
            var e = obj as Microsoft.Phone.Shell.ApplicationBarStateChangedEventArgs;
            if(e == null) 
            {
                ShowMapLogo = false; 
                return;
            }

            ShowMapLogo = e.IsMenuVisible;
        }

        /// <summary>Handles the selection of a route waypoint from the directions list</summary>
        /// <param name="obj">Selected index</param>
        private void DoRouteWaypointSelectedCommand(object obj)
        {
            try
            {
                if( obj == null || 
                    _locationService == null || 
                    _locationService.RouteWaypoints == null || 
                    _locationService.RouteWaypoints.Count == 0) 
                    return;

                var index = int.Parse(obj.ToString());
                if(index == -1 || index > _locationService.RouteWaypoints.Count - 1) return;
                
                var waypoint = _locationService.RouteWaypoints[index];
                if(waypoint == null) return;

                MapCenterLocation = waypoint;
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error handling DoRouteWaypointSelectedCommand", new StackFrame(0, true));                                
            }
        }

        /// <summary>
        /// Provides the production ApplicationId and AuthenticationToken values necessary for
        /// production use of the Nokia map control
        /// </summary>
        /// <param name="obj">Not used</param>
        /// <remarks>
        /// When deploying to the store you need to get an ApplicationId and AuthenticationToken
        /// which provides the necessary authentication to use the Nokia maps control in production
        /// 
        /// *********************************************************************************
        /// * These values are not required at development time. Providing invalid or empty *
        /// * ApplicationID and AuthenticationToken at dev-time does not affect or disable  *
        /// * the map control                                                               *
        /// *********************************************************************************
        /// 
        /// MSDN Ref:http://msdn.microsoft.com/en-us/library/windowsphone/develop/jj207033(v=vs.105).aspx
        /// 
        /// Adding the ApplicationID and AuthenticationToken
        /// Before you can publish an app that uses the Map control, you have to get an ApplicationId and 
        /// AuthenticationToken from the Windows Phone Dev Center and add the values to your code. The values 
        /// that you get are specific to the individual app for which you request them.
        /// 
        /// To get an ApplicationID and AuthenticationToken from the Dev Center 
        /// 
        /// 1. After you have finished your app, begin the app submission process.
        /// 2. On the Submit app page, click Map services. The Map services page opens.
        /// 3. On the page, click Get token.  The new ApplicationID and AuthenticationToken are displayed on the same page.
        /// 4. Copy the values and paste them into your code as described in the following procedure.
        /// 5. Rebuild your app with the new code and upload and updated copy to the Store.
        ///  
        /// You have to set the values of both the ApplicationId and AuthenticationToken properties after 
        /// the first Map control has been loaded, not just instantiated. If you destroy all instances 
        /// of the Map control in your app and then create a new instance, you have to set these 
        /// properties again.
        /// </remarks>
        private void DoMapLoadedCommand(object obj)
        {
            try
            {
                var appId = Strings.GetStringResource("_MapApplicationId", "0c4920e4-ffa6-4e77-bb93-5d490e832204");
                var authToken = Strings.GetStringResource("_MapAuthenticationToken", "GgRLyFHFs1z2h6zeeWjE_Q");

                if(string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(authToken))
                    Logger.Log("MapLoaded: Authorization tokens are null or empty (this is OK in dev)");

                Microsoft.Phone.Maps.MapsSettings.ApplicationContext.ApplicationId = appId;
                Microsoft.Phone.Maps.MapsSettings.ApplicationContext.AuthenticationToken = authToken;

                // We restore saved map control values because, although the following properties are bound to 
                // map properties, when the map loads it resets the values to defaults, and then the bindings
                // reset properties to their default values (note that we set the zoom values at other points)
                MapPitch = GetStateItem<double>("MapPitch", 0);
                MapHeading = GetStateItem<double>("MapHeading", 0);
                MapLandmarksOn = GetStateItem("MapLandmarksOn", false);
                MapPedestrianFeaturesOn = GetStateItem("MapPedestrianFeaturesOn", false);
                MapDayNightColorMode = GetStateItem("MapDayNightColorMode", MapColorMode.Light);
                MapMode = GetStateItem("MapMode", MapCartographicMode.Road);
            }
            catch(Exception ex)
            {
                MessageBoxHelper.Show(Strings.Get("MissingMapAuthenticationCode"), Strings.Get("MissingMapAuthenticationCodeTitle"), false);
                Logger.Log(ex, "Unable to get map authentication token and id", new StackFrame(0, true));
            }
        }

        /// <summary>Inviter and Invitee. Gets the route from a start GeoCoordinate to an end point (e.g. to the RoundUp point)</summary>
        /// <param name="obj">Not used</param>
        private async void DoGetRouteCommand(object obj)
        {
            try
            {
                var route = await _locationService.GetRouteAsync(
                    CurrentLocation, 
                    RoundUpPointLocation, 
                    IsWalking ? TravelMode.Walking : TravelMode.Driving);

                if(route == null || route.Legs == null || route.Legs.Count == 0)
                {
                    Logger.Log("Unable to get route to RoundUp location");

                    // Can't get the route for some reason. Display a helpful message
                    // to the user:
                    //
                    // Sorry, unable to get the route to the roundup point.
                    //
                    // However, {invitee name} is located at {roundup address}. 
                    //
                    // This location is marked on the map and is {distance} away from your current location

                    var msg = Strings.Get("CannotGetRoute");
                    msg = msg.Replace("{0}", CurrentInviteCode.InviterAlias);
                    msg = msg.Replace("{1}", RoundUpPointInfo.Address);
                    msg = msg.Replace("{2}", RoundUpPointInfo.DistanceText);
                    MessageBoxHelper.Show(msg, Strings.Get("CannotGetRouteTitle"), false);

                    MapCenterLocation = RoundUpPointLocation;  // Center the map on the roundup point
                    return;
                }

                var mapRoute = new MapRoute(route);

                // Fire the MapRouteChanged event to allow the View to add the route to the map control
                OnMapRouteChanged(new MapRouteEventArgs { NewRoute = mapRoute });

                if(RouteInstructions == null) RouteInstructions = new ObservableCollection<RouteInstruction>();
                else RouteInstructions.Clear();

                foreach(var instruction in _locationService.RouteInstructions) 
                    RouteInstructions.Add(new RouteInstruction {Instruction = instruction});

                RouteInstructions.Add(
                    new RouteInstruction
                    {
                        Instruction = string.Format(Strings.Get("RouteInstructionEta"), _locationService.GetEta(route).ToShortTimeString())
                    });
            }
            catch (Exception ex)
            {
                Logger.Log(ex, "Unable to get route to RoundUp location", new StackFrame(0, true));

                var msg = Strings.Get("CannotGetRoute");
                msg = msg.Replace("{0}", CurrentInviteCode.InviterAlias);
                msg = msg.Replace("{1}", RoundUpPointInfo.Address);
                msg = msg.Replace("{2}", RoundUpPointInfo.DistanceText);
                MessageBoxHelper.Show(msg, Strings.Get("CannotGetRouteTitle"), false);

                MapCenterLocation = RoundUpPointLocation;  // Center the map on the roundup point
            }
        }

        /// <summary>
        /// Attempts to restore a previous session. Can be requested by the user when auto-restore of a previous session
        /// has been abandoned because it exceeds the session timeout value
        /// </summary>
        /// <param name="obj">Not used</param>
        private async void DoRestorePreviousSessionCommand(object obj)
        {
            if(!AllKeyServicesAreOn())
            {
                ShowKeyServicesOffMessage();
                return;
            }

            try
            {
                ProgressBarText = Strings.Get("WillAttemptSessionRestore");
                ShowProgressBar = true;

                await RestorePreviousSession();
                if(App.CanRestorePreviousSession) await ResendMissedNotificationsAsync();
                else MessageBoxHelper.Show(Strings.Get("UnableToRestorePreviousSession"), Strings.Get("UnableToRestorePreviousSessionTitle"), false);
            }
            catch(Exception ex) 
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
            finally
            {
                ProgressBarText = string.Empty;
                ShowProgressBar = false;
            }
        }

        /// <summary>Dump this object's state, along with state from core services. Does nothing when compiled for Release</summary>
        /// <param name="obj">Not used</param>
        private void DoDumpCommand(object obj)
        {
            try
            {
                Dump();
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary>Raises a series on PropertyChanged events when the connection status of a service changes</summary>
        private void RaiseServicePropertyChangedEvents()
        {
            OnPropertyChanged("WaitingForLocationServiceToConnect");
            OnPropertyChanged("AllServicesConnected");
            OnPropertyChanged("IsSessionCancelable");
            OnPropertyChanged("IsInviteCancelable");
            OnPropertyChanged("AppBarIndexSelector");

            try
            {
                if(HasStartUpParams && LocationServiceConnected)
                {
                    Logger.Log("Will attempt to auto-accept invite (SessionId = " + InviteCodeHelper.LaunchInviteCode.SessionId + ")");
                    DoStartAcceptInviteCommand(null);
                }
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));                
            }
        }

        /// <summary>Changes the roundup (meeting) point. Sends notifications to all invitees if necessary</summary>
        /// <param name="newLocation">The GeoCoordinate for the new roundup point</param>
        /// <returns>Returns a RoundUpServiceOperationResult indicating success or failure</returns>
        private async Task RequestChangeRoundUpPoint(GeoCoordinate newLocation)
        {
            try
            {
                if(!LocationServiceConnected || _mpnsChannelHelper.WaitingForMpnsChannelUri || newLocation == null) return;

                // Save the location of the new roundup location
                RequestedNewRoundUpPoint = newLocation;

                if(!await CheckNetworkAvailable())
                {
                    MessageBoxHelper.Show(Strings.Get("NoNetworkConnection"), string.Empty, false);
                    return;
                }

                // If the MPNS channel isn't connected, wait until it is (the call to DoCompleteRequestChangeRoundUpPoint is in the handler)
                if (!_mpnsService.Connected)
                {
                    Logger.Log("Start wait for MPNS ChannelOpen (RequestChangeRoundUpPoint)");

                    ProgressBarText = Strings.Get("WaitingForPushNotificationService");
                    ShowProgressBar = true;

                    _mpnsChannelHelper.StartWaiting(DoCompleteRequestChangeRoundUpPoint, MpnsWaitTimedout);
                }
                else await DoCompleteRequestChangeRoundUpPoint();
            }
            catch (Exception ex)
            {
                _mpnsChannelHelper.WaitingForMpnsChannelUri = false;
                ShowProgressBar = false;
                Logger.Log(ex, new StackFrame(0, true));
                MessageBoxHelper.Show(Strings.Get("CannotChangeRoundUpPoint"), Strings.Get("CannotChangeRoundUpPointTitle"), false);
            }
        }

        /// <summary>
        /// Completes the change to the roundup (meeting) point. Sends notifications to all invitees if necessary.
        /// Do not call this method directly - call RequestChangeRoundUpPoint
        /// </summary>
        /// <returns>Returns a RoundUpServiceOperationResult indicating success or failure</returns>
        private async Task DoCompleteRequestChangeRoundUpPoint()
        {
            try
            {
                ProgressBarText = Strings.Get("ProgressBarTextChangeRoundUpPoint");  // "changing roundup location"
                ShowProgressBar = true;

                var result = await _roundUpService.UpdateRoundUpPointLocationAsync(
                    SessionId, 
                    _mpnsService.ChannelUri.ToString(), 
                    InviterShortDeviceId, 
                    RequestedNewRoundUpPoint.Latitude, 
                    RequestedNewRoundUpPoint.Longitude,
                    SettingsAlias,
                    string.Empty);

                switch(result)
                {
                    case RoundUpServiceOperationResult.OperationSuccess:
                        MessageBoxHelper.Show(Strings.Get("ChangeRoundUpPointSuccess"), Strings.Get("ChangeRoundUpPointSuccessTitle"), false);
                        break;

                    case RoundUpServiceOperationResult.MpnsNotificationLimitExceeded:
                        Logger.Log("MPNS notification limit exceeded", new StackFrame(0, true));
                        MessageBoxHelper.Show(Strings.Get("MpnsLimitExceeded"), Strings.Get("MpnsLimitExceededTitle"), false);
                        break;

                    default:
                        Logger.Log("RoundUpService.UpdateRoundUpPointLocationAsync() returned an error", new StackFrame(0, true));
                        MessageBoxHelper.Show(Strings.Get("CannotChangeRoundUpPoint"), Strings.Get("CannotChangeRoundUpPointTitle"), false);
                        break;
                }
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                MessageBoxHelper.Show(Strings.Get("CannotChangeRoundUpPoint"), Strings.Get("CannotChangeRoundUpPointTitle"), false);
            }
            finally
            {
                ShowProgressBar = false;
            }
        }

        /// <summary>
        /// If we've not previously asked the user (and got a positive responce), ask for permission to use location services.
        /// Sets the values of the SettingsLocationServicesOn and SettingsUserPermissionLocationServices properties as reqired
        /// </summary>
        /// <returns>Returns true if we have permission, false otherwise</returns>
        private bool WeHavePermissionToUseLocationServices()
        {
            try
            {
                if(SettingsUserPermissionLocationServices || GetStateItem("SettingsUserPermissionLocationServices", false))
                    return true; // User has previously given permission

                var result = MessageBoxHelper.Show(Strings.Get("RequestPermissionLocationServices"), Strings.Get("RequestPermissionLocationServicesTitle"), MessageBoxButton.OKCancel);
                if(result == MessageBoxResult.Cancel)
                {
                    Logger.Log("The user refused permission to use location services");
                    SettingsLocationServicesOn = false;
                    SettingsTrackCurrentLocation = false;
                    SetStateItem("SettingsLocationServicesOn", false);
                    SetStateItem("SettingsUserPermissionLocationServices", false); // Ask again next time we start                         
                    return false;
                }

                // All OK to use location services
                Logger.Log("The user gave permission to use location services");
                SettingsUserPermissionLocationServices = true;
                SettingsLocationServicesOn = true;
                SetStateItem("SettingsLocationServicesOn", true);
                SetStateItem("SettingsUserPermissionLocationServices", true); // Don't ask again                           
                return true;
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return false;
            }
        }

        /// <summary>
        /// If we've not previously asked the user (and got a positive responce), ask for permission to use MPNS.
        /// Sets the values of the SettingsMpnsOn and SettingsUserPermissionMpns properties as reqired
        /// </summary>
        /// <returns>Returns true if we have permission, false otherwise</returns>
        private bool WeHavePermissionToUseMpns()
        {
            try
            {
                if(SettingsUserPermissionMpns || GetStateItem("SettingsUserPermissionMpns", false))
                    return true; // User has previously given permission

                var result = MessageBoxHelper.Show(Strings.Get("RequestPermissionMpns"), Strings.Get("RequestPermissionMpnsTitle"), MessageBoxButton.OKCancel);
                if(result == MessageBoxResult.Cancel)
                {
                    SettingsMpnsOn = false;
                    SetStateItem("SettingsMpnsOn", false);
                    SetStateItem("SettingsUserPermissionMpns", false); // Ask again next time we start                         
                    return false;
                }

                // All OK to use location services
                SettingsUserPermissionMpns = true;
                SettingsMpnsOn = true;
                SetStateItem("SettingsMpnsOn", true);
                SetStateItem("SettingsUserPermissionMpns", true); // Don't ask again                           
                return true;
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return false;
            }
        }

        /// <summary>Checks to see if we have any type of network connectivity</summary>
        /// <returns>Returns true if we have any type of network connectivity, false otherwise</returns>
        private async Task<bool> CheckNetworkAvailable(bool showProgressBar = true)
        {
            try
            {
                if(showProgressBar)
                {
                    ProgressBarText = Strings.Get("CheckingForNetworkConnection");
                    ShowProgressBar = true;
                }

                var connected = await _networkService.GetConnectionStatusAsync();
                return connected;
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return false;
            }
            finally
            {
                if(showProgressBar)
                {
                    ProgressBarText = string.Empty;
                    ShowProgressBar = false;
                }
            }
        }
      
        /// <summary>We timed-out waiting for MPNS - can't complete the invite command</summary>
        private void MpnsWaitTimedout()
        {
            MessageBoxHelper.Show(Strings.Get("MpnsTimedOut"), Strings.Get("MpnsTimedOutTitle"), false);

            ShowProgressBar = false;
            ShowAcceptInviteUI = false;
            OperationHasCompleted = true;
        }

        /// <summary>We timed-out waiting for MPNS - can't complete the invite command (but don't display an error)</summary>
        private void MpnsWaitSilentTimedout()
        {
            ShowProgressBar = false;
            ShowAcceptInviteUI = false;
            OperationHasCompleted = true;
        }

        /// <summary>Updates our tile</summary>
        private void RefreshFlipTile()
        {
            if(!SessionTypeHasBeenSet)
            {
                FlipTileHelper.Update(Strings.Get("TileNoSession"), Strings.Get("TileNoSessionMsg"), Strings.Get("TileNoSessionWideMsg"));
                return;
            }

            // Inviter processing
            if(IsInviter)
            {
                var count = Invitees == null ? 0 : Invitees.Count;
                switch(count)
                {
                    case 0:
                        FlipTileHelper.Update(Strings.Get("TileSessionInProgress"), Strings.Get("TileSessionInProgressNoInvitees"));
                        break;

                    case 1:
                        FlipTileHelper.Update(Strings.Get("TileSessionInProgress"), Strings.Get("TileSessionInProgressWithOneInvitee"));
                        break;

                    default:
                        FlipTileHelper.Update(Strings.Get("TileSessionInProgress"), count + Strings.Get("TileSessionInProgressWithInvitees"));
                        break;
                }
            }
            else
            {
                // Invitee processing
                FlipTileHelper.Update(Strings.Get("TileSessionInProgress"), Strings.Get("TileSessionInProgressInvitee") + CurrentInviteCode.InviterAlias);
            }
        }

        /// <summary>Tests to see if both location and mpns services are on</summary>
        /// <returns>Returns true if both location and mpns services are on, false otherwise</returns>
        private bool AllKeyServicesAreOn()
        {
            return SettingsLocationServicesOn && SettingsMpnsOn;
        }

        private void ShowKeyServicesOffMessage()
        {
            var msg = Strings.Get("KeyServicesAreOff");
            if(!SettingsLocationServicesOn) msg += "Location\n\r";
            if(!SettingsMpnsOn) msg += "Push notifications";

            MessageBoxHelper.Show(msg, Strings.Get("KeyServicesAreOffTitle"), false);
        }

        private void ShowPanelOverlay(PanelOverlay panel, bool show)
        {
            ShowAcceptInviteUI = false;
            ShowDirectionsUI = false; 
            ShowInviteesUI = false;
            ShowMapControlPanel = false;
            ShowShareUI = false;

            switch(panel)
            {
                case PanelOverlay.AcceptInvite:
                    ShowAcceptInviteUI = show;
                    break;

                case PanelOverlay.Directions:
                    if(RouteInstructions == null || RouteInstructions.Count == 0)
                    {
                        MessageBoxHelper.Show(Strings.Get("NoDirections"), Strings.Get("NoDirectionsTitle"), false);
                        return;
                    }
                    
                    ShowDirectionsUI = show;
                    break;

                case PanelOverlay.Invitees:
                    if(Invitees == null || Invitees.Count == 0)
                    {
                        MessageBoxHelper.Show(Strings.Get("NoInvitees"), Strings.Get("NoInviteesTitle"), false);
                        return;
                    }

                    ShowInviteesUI = show;
                    break;

                case PanelOverlay.MapControls:
                    ShowMapControlPanel = show;
                    break;

                case PanelOverlay.Share:
                    ShowShareUI = show;
                    break;
            }
        }

    }
}
