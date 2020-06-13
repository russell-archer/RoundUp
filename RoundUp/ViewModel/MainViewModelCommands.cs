using System.ComponentModel;
using RArcher.Phone.Toolkit;
using RArcher.Phone.Toolkit.Common;

namespace RoundUp.ViewModel
{
    /// <summary>ViewModel for MainView (ICommands)</summary>
    public partial class MainViewModel : ModelBase, IMainViewModel, INotifyPropertyChanged
    {
        /// <summary>Starts a new session</summary>
        public RelayCommand InviteCommand { get; set; }

        /// <summary>Used by an inviter to cancel a session. A notification is sent to all invitees that have accepted</summary>
        public RelayCommand CancelSessionCommand { get; set; }

        /// <summary>Menu item to allow the user to tap the map to select a new RoundUp point</summary>
        public RelayCommand StartSetNewRoundUpPointCommand { get; set; }

        /// <summary>Inviter. Executed from the View's code-behind. Passes the GeoCoordinate of the new RoundUp point</summary>
        public RelayCommand SetNewRoundUpPointCommand { get; set; }

        /// <summary>Starts the process of accepting an invite. Shows a textbox that can be used to enter the invite code</summary>
        public RelayCommand StartAcceptInviteCommand { get; set; }

        /// <summary>Completes the process of accepting an invite. Inserts a row in the Invitee Azure table and triggers MPNS notifications</summary>
        public RelayCommand CompleteAcceptInviteCommand { get; set; }

        /// <summary>Allows the user to cancel the start of accepting an invite</summary>
        public RelayCommand CancelAcceptInviteCommand { get; set; }

        /// <summary>Decline an invitation. A notification is sent to the inviter</summary>
        public RelayCommand DeclineInviteCommand { get; set; }

        /// <summary>Cancel a previously accepted invitation. A notification is sent to the inviter</summary>
        public RelayCommand CancelAcceptedInvitationCommand { get; set; }

        /// <summary>Inviter and Invitee. Ad-hoc refresh of current device position. Will update the CurrentLocation property</summary>
        public RelayCommand RefreshCurrentLocationCommand { get; set; }

        /// <summary>Inviter and Invitee. Turns on location tracking</summary>
        public RelayCommand TurnLocationTrackingOnCommand { get; set; }

        /// <summary>Inviter and Invitee. Turns off location tracking</summary>
        public RelayCommand TurnLocationTrackingOffCommand { get; set; }

        /// <summary>Inviter and Invitee. Gets the route from a start GeoCoordinate to an end point (e.g. to the RoundUp point)</summary>
        public RelayCommand GetRouteCommand { get; set; }

        /// <summary>Attempts to restore a previous session, if it's still alive</summary>
        public RelayCommand RestorePreviousSessionCommand { get; set; }

        /// <summary>Allow the user to purchase the app (upgrade from trial)</summary>
        public RelayCommand PurchaseCommand { get; set; }

        /// <summary>Allow the user to rate and review the app in the store</summary>
        public RelayCommand RateAndReviewCommand { get; set; }

        /// <summary>Show the map control panel</summary>
        public RelayCommand ShowMapControlPanelCommand { get; set; }

        /// <summary>Show the share panel</summary>
        public RelayCommand ShowSharePanelCommand { get; set; }

        /// <summary>Show the directions panel</summary>
        public RelayCommand ShowDirectionsPanelCommand { get; set; }

        /// <summary>Show the invitees panel</summary>
        public RelayCommand ShowInviteesPanelCommand { get; set; }

        /// <summary>Sets the MapCatographicMode property based on a command property value</summary>
        public RelayCommand MapCartographicConverterCommand { get; set; }

        /// <summary>Show the help panorama</summary>
        public RelayCommand ShowHelpCommand { get; set; }

        /// <summary>Close the first-run help screen</summary>
        public RelayCommand CloseFirstRunHelpCommand { get; set; }

        /// <summary>Handler for the map loaded event. Sets up the necessary production api keys (MapLoadedEventToCommand)</summary>
        public RelayCommand MapLoadedCommand { get; set; }

        /// <summary>Menu item used by the Inviter to send an SMS invite</summary>
        public RelayCommand SendInviteSmsCommand { get; set; }

        /// <summary>Menu item used by the Inviter to send an email invite</summary>
        public RelayCommand SendInviteEmailCommand { get; set; }

        /// <summary>Centers the map on a particular invitee</summary>
        public RelayCommand CenterMapOnInviteeCommand { get; set; }

        /// <summary>Centers the map on the device's current location</summary>
        public RelayCommand CenterMapOnDeviceCommand { get; set; }

        /// <summary>Checks for missed notifications</summary>
        public RelayCommand SyncNotificationsCommand { get; set; }

        /// <summary>Allows the user's location to be shared via Bing maps</summary>
        public RelayCommand ShareLocationViaWebCommand { get; set; }

        /// <summary>Displays the About view</summary>
        public RelayCommand ShowAboutViewCommand { get; set; }

        /// <summary>Displays the Settings view</summary>
        public RelayCommand ShowSettingsViewCommand { get; set; }

        /// <summary>Handles the selection of a route waypoint (route directions list)</summary>
        public RelayCommand RouteWaypointSelectedCommand { get; set; }

        /// <summary>Executed when the menu is shown/hidden</summary>
        public RelayCommand MenuStateChangedCommand { get; set; }

        /// <summary>Dump this object's state, along with state from core services</summary>
        public RelayCommand DumpCommand { get; set; }
    }
}
