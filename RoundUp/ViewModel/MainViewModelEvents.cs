using System;
using System.ComponentModel;
using System.Diagnostics;
using RArcher.Phone.Toolkit;
using RArcher.Phone.Toolkit.Location.Common;
using RArcher.Phone.Toolkit.Logging;
using RoundUp.Model;

namespace RoundUp.ViewModel
{
    /// <summary>ViewModel for MainView (events)</summary>
    public partial class MainViewModel : ModelBase, IMainViewModel, INotifyPropertyChanged
    {
        /// <summary>The MapRouteChanged event is raised when a new Route is available to the RoundUp location</summary>
        public event EventHandler<MapRouteEventArgs> MapRouteChanged;

        /// <summary>
        /// The StateRestored event is raised when all state for the view model and key services has been 
        /// restored (or it isn't required to do so).
        /// </summary>
        public event EventHandler<EventArgs> StateRestored;

        /// <summary>
        /// The ShareUiVisibilityChanged event is raised when the visibility of the share panel changes.
        /// The visibility of the share panel is determined through the ShowShareUI view model property
        /// </summary>
        public event EventHandler<EventArgs> ShareUiVisibilityChanged;


        /// <summary>The CurrentLocationChanged event is raised when the device's location changes</summary>
        public event EventHandler<LocationUpdateEventArgs> CurrentLocationChanged;

        /// <summary>The InviteeSelected event is raised when an invitee is selected on the invitee list panel</summary>
        public event EventHandler<LocationUpdateEventArgs> InviteeSelected;

        /// <summary>Raises the MapRouteChanged event when a new route to the RoundUp location is available</summary>
        /// <param name="args">Contains information on the route to the RoundUp point. Can be added to a Map control using Map.AddRoute</param>
        public void OnMapRouteChanged(MapRouteEventArgs args)
        {
            try
            {
                var handler = MapRouteChanged;
                if(handler != null) handler(this, args);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));                
            }
        }

        /// <summary>Raises the StateRestored event</summary>
        /// <param name="args">Not used</param>
        public void OnStateRestored(EventArgs args)
        {
            try
            {
                var handler = StateRestored;
                if(handler != null) handler(this, args);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }            
        }

        /// <summary>Raises the ShareUiVisibilityChanged event</summary>
        /// <param name="args">Not used</param>
        public void OnShareUiVisibilityChanged(EventArgs args)
        {
            try
            {
                var handler = ShareUiVisibilityChanged;
                if(handler != null) handler(this, args);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }            
        }

        /// <summary>Fires the CurrentLocationChanged event whenever the device's location changes</summary>        
        public void OnCurrentLocationChanged(LocationUpdateEventArgs args)
        {
            var handler = CurrentLocationChanged;
            if (handler != null) handler(this, args);
        }

        /// <summary>Fires the InviteeSelected event when an invitee is selected on the invitee list panel</summary>        
        public void OnInviteeSelected(LocationUpdateEventArgs args)
        {
            var handler = InviteeSelected;
            if (handler != null) handler(this, args);
        }
    }
}
