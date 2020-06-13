using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using RArcher.Phone.Toolkit.Logging;
using RoundUp.ViewModel;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Maps.Controls;
using GestureEventArgs = System.Windows.Input.GestureEventArgs;

namespace RoundUp.View
{
    /// <summary>MainView class</summary>
    public partial class MainView : PhoneApplicationPage
    {
        /// <summary>The View's ViewModel</summary>
        public IMainViewModel ViewModel { get; set; }

        private MapRoute _mapRouteToRoundUpLocation;  // Holds info on the route to the RoundUp point
        private readonly Timer _sonarTimer;
        private bool _subscribedToStoryboardEvents;
        private readonly EventHandler<CancelEventArgs> _backButtonHandler;  // Hold onto the delegate so we can unsubscribe during navigation away from the page

        /// <summary>Constructor, retrieves a refernce to our ViewModel which has been set via the ViewModelLocator in XAML</summary>
        public MainView()
        {
            try
            {
                InitializeComponent();

                // Get a reference to our view model, which has been set in XAML
                ViewModel = this.DataContext as IMainViewModel;
                if(ViewModel == null) throw new NullReferenceException();

                // Hook into the ViewModel's MapRouteChanged event. This allows us to display the
                // route (if any) to the RoundUp location point
                ViewModel.MapRouteChanged += (sender, args) =>
                {
                    try
                    {
                        // Remove any previous route
                        if(_mapRouteToRoundUpLocation != null) LocationMap.RemoveRoute(_mapRouteToRoundUpLocation);

                        // Save the route...
                        _mapRouteToRoundUpLocation = args.NewRoute;

                        // The ViewModel can remove the existing route by raising the MapRouteChanged event and supplying a null NewRoute
                        if(args.NewRoute == null) return;

                        // ... and add it to the map
                        LocationMap.AddRoute(_mapRouteToRoundUpLocation);
                    }
                    catch(Exception ex)
                    {
                        Logger.Log(ex, "Error modifying route. ", new StackFrame(0, true));                        
                    }
                };

                // Hook into the ViewModel's ShareUiVisibilityChanged event. This allows us to start/stop the "sonar" annimation
                _sonarTimer = new Timer(SonarWaitOnCompleted, null, -1, -1);
                ViewModel.ShareUiVisibilityChanged += (sender, args) =>
                {
                    if(!ViewModel.ShowShareUI)
                    {
                        // Close the share panel and stop the sonar animation
                        _sonarTimer.Change(-1, -1);  // Stop the timer
                        if(_subscribedToStoryboardEvents) StoryboardSonar.Completed -= StoryboardSonarOnCompleted;
                        StoryboardSonar.Stop();  // Stop the sonar animation
                        ShareUiPanelCloseStoryboard.Begin();  // Close the share panel
                        return;
                    }

                    // Open the share panel and start the sonar animation
                    StoryboardSonar.Completed += StoryboardSonarOnCompleted;
                    _subscribedToStoryboardEvents = true;
                    StoryboardSonar.Begin();  // Start the sonar animation
                    ShareUiPanelOpenStoryboard.Begin();  // Open the share panel
                };

                // Hook into the CurrentLocationChanged event so we can update the position of the current location "sonar" animation
                ViewModel.CurrentLocationChanged += (sender, args) =>
                {
                    var currentLoc = LocationMap.ConvertGeoCoordinateToViewportPoint(args.Location);
                    SonarCurrentLocation.Margin = new Thickness(
                        currentLoc.X - SonarCurrentLocation.Width/2, 
                        currentLoc.Y - SonarCurrentLocation.Height/2, 
                        0, 
                        0);

                    SonarCurrentLocation.Visibility = Visibility.Visible;
                    Deployment.Current.Dispatcher.BeginInvoke(() => StoryboardCurrentLocation.Begin());
                };
                StoryboardCurrentLocation.Completed += (sender, args) => { SonarCurrentLocation.Visibility = Visibility.Collapsed; };

                // Hook into the InviteeSelected event so we can place a "sonar" animation around the currently selected invitee
                ViewModel.InviteeSelected += (sender, args) =>
                {
                    var inviteeLoc = LocationMap.ConvertGeoCoordinateToViewportPoint(args.Location);
                    SonarSelectedInvitee.Margin = new Thickness(
                        inviteeLoc.X - SonarSelectedInvitee.Width/2, 
                        inviteeLoc.Y - SonarSelectedInvitee.Height/2, 
                        0, 
                        0);

                    SonarSelectedInvitee.Visibility = Visibility.Visible;
                    Deployment.Current.Dispatcher.BeginInvoke(() => StoryboardSelectedInvitee.Begin());                    
                };
                StoryboardSelectedInvitee.Completed += (sender, args) => { SonarSelectedInvitee.Visibility = Visibility.Collapsed; };

                // Pass on the backKeyPress event to the view model
                // 9/4/14 changed from using anonymous delegate so we can unsubscribe from the event during OnNavigatedFrom as recommended by MS
                //this.BackKeyPress += (sender, args) => ViewModel.BackKeyPress(args);
                _backButtonHandler = (sender, args) => ViewModel.BackKeyPress(args);
                this.BackKeyPress += _backButtonHandler;
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Fatal exception in MainView ctor. ", new StackFrame(0, true));                
            }
        }

        /// <summary>Called when the sonar animation completes</summary>
        private void StoryboardSonarOnCompleted(object sender, EventArgs eventArgs)
        {
            _sonarTimer.Change(1500, -1); // Start the timer (the "sonar" pulse animation is shown every 1.5 secs)
        }

        private void SonarWaitOnCompleted(object state)
        {
            _sonarTimer.Change(-1, -1);  // Stop the timer
            Deployment.Current.Dispatcher.BeginInvoke(() => StoryboardSonar.Begin());  // Do the sonar animation again
        }

        /// <summary>Handles the OnNavigatedTo event. Calls the ViewModel to restore state</summary>
        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ViewModel.RestoreState();
        }

        /// <summary>Handles the OnNavigatedFrom event. Calls the ViewModel to save state</summary>
        protected override void OnNavigatedFrom(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            ViewModel.SaveState();

            // This value can be used by the destination view/view model to decide if special processing is required.
            // For example, if the destination is the main view and the previous view may have modified some shared
            // settings values, the main view model will want to restore those settings
            App.MostRecentView = this.GetType().Name;

            // 9/4/14 changed to unsubscribe from the BackKeyPress event (recommended by Microsoft)
            this.BackKeyPress -= _backButtonHandler;
        }

        /// <summary>
        /// Handles the map tap event. This event is not routed to the ViewModel because we need to 
        /// directly use the map control's ability to convert a tap-point to an actual GeoCoordinate.
        /// Once we have the necessary GeoCoordinate data we route it to a RelayCommand in the ViewModel
        /// to handle all futher process required
        /// </summary>
        private void LocationMapOnTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            try
            {
                // Will the ViewModel allow us to change the RoundUp point, or does the user want to close
                // the map controls overlay?
                if(ViewModel.AllowRoundUpPointLocationChange)
                {
                    // Get tap point relative to the map control
                    var p = e.GetPosition(LocationMap);

                    // Ask the map to convert this to a GeoCoordinate
                    var roundUpPointGeoCoordinate = LocationMap.ConvertViewportPointToGeoCoordinate(p);

                    // Now ask the ViewModel to handle the details for creating the new RoundUp point.
                    // It will only do this if the user has previously requested setting a new RoundUp point
                    // using the StartSetNewRoundUpPointCommand menu option. This prevents map taps in normal usage 
                    // from accidentally setting a new RoundUp point
                    ViewModel.SetNewRoundUpPointCommand.Execute(roundUpPointGeoCoordinate);
                }
                else if(ViewModel.ShowMapControlPanel) ViewModel.ShowMapControlPanelCommand.Execute(null);  // Hide the map control panel
                else if(ViewModel.ShowShareUI) ViewModel.ShowSharePanelCommand.Execute(null);  // Hide the share panel
                else if(ViewModel.ShowAcceptInviteUI) ViewModel.CancelAcceptInviteCommand.Execute(null);  // Hide the share panel
                else if(ViewModel.ShowInviteesUI) ViewModel.ShowInviteesPanelCommand.Execute(null);  // Hide the invitees panel
                else if(ViewModel.ShowDirectionsUI) ViewModel.ShowDirectionsPanelCommand.Execute(null);  // Hide the directions panel
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));                
            }
        }

        /// <summary>Show the share panel</summary>
        private void LocationMapOnHold(object sender, GestureEventArgs e)
        {
            try
            {
                ViewModel.ShowSharePanelCommand.Execute(!ViewModel.ShowShareUI);  // Show/Hide the share panel
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));                                
            }
        }
    }
}