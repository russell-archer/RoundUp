using System.ComponentModel;
using RArcher.Phone.Toolkit;

namespace RoundUp.ViewModel
{
    /// <summary>ViewModel for MainView (constants)</summary>
    public partial class MainViewModel : ModelBase, IMainViewModel, INotifyPropertyChanged
    {
        /// <summary>The initial zoom level when we first connect to location services</summary>
        private const int InitialMapZoomLevel = 17;

        /// <summary>
        /// For long journeys that might produce a large number of notifications we use this const value
        /// to determine the max number of notifications we can request via the throttled (500 notification limit)
        /// MPNS service
        /// </summary>
        private const double MaxSafeMpnsNotificationLimit = 400;
    }
}
