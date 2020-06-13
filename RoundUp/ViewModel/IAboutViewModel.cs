using RArcher.Phone.Toolkit.Common;
using RArcher.Phone.Toolkit.Store.Enum;

namespace RoundUp.ViewModel
{
    public interface IAboutViewModel
    {
        /// <summary>Returns the app's version as a string, suitable for display in the about screen</summary>
        string VersionText { get; }
        
        /// <summary>Text that can be used to denote the license mode (e.g. "Trial ({0} days left)", "Full", "Trial Expired")</summary>
        string LicenseModeText { get; } 

        /// <summary>Holds the license mode for the app (Trial, Full, etc.)</summary>
        LicenseMode License { get; }

        /// <summary>Allow the user to purchase the app (upgrade from trial)</summary>
        RelayCommand PurchaseCommand { get; set; }

        /// <summary>Allow the user to rate and review the app in the store</summary>
        RelayCommand RateAndReviewCommand { get; set; }

        /// <summary>Save state to persistent (isolated) storage</summary>
        void SaveState();

        /// <summary>Restore state from to persistent (isolated) storage</summary>
        void RestoreState();
    }
}