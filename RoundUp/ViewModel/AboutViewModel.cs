using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using RArcher.Phone.Toolkit;
using RArcher.Phone.Toolkit.Common;
using RArcher.Phone.Toolkit.Logging;
using RArcher.Phone.Toolkit.Store;
using RArcher.Phone.Toolkit.Store.Enum;

namespace RoundUp.ViewModel
{
    public class AboutViewModel : ModelBase, IAboutViewModel, INotifyPropertyChanged
    {
        // Properties ---------------------------------------------------------
        
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

        /// <summary>Holds the license mode for the app (Trial, Full, etc.)</summary>
        [Dump]
        [DoNotSaveState]
        public LicenseMode License
        {
            get { return _storeService.License; }
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

        // Commands -----------------------------------------------------------
        
        /// <summary>Allow the user to purchase the app (upgrade from trial)</summary>
        public RelayCommand PurchaseCommand { get; set; }

        /// <summary>Allow the user to rate and review the app in the store</summary>
        public RelayCommand RateAndReviewCommand { get; set; }

        // Privates -----------------------------------------------------------
        
        private readonly IStoreService _storeService;

        // Methods ------------------------------------------------------------
        
        public AboutViewModel() : base("MainViewModel")
        {
            // Note that we're using the save/restore state id of "MainViewModel".
            // This allows us to share settings with the main view model

            _storeService = IocContainer.Get<IStoreService>();

            PurchaseCommand = new RelayCommand(DoPurchaseCommand);
            RateAndReviewCommand = new RelayCommand(DoRateAndReviewCommand);

            InitStoreService();     // Checks our license
        }

        /// <summary>Save state to persistent (isolated) storage</summary>
        public override void SaveState()
        {
            Logger.Log("AboutViewModel.SaveState");

            //SaveAutoSetting();  // Save all settings marked with the [AutoSetting] attribute
            //SaveAutoState();  // Save all properties marked with the [AutoState] attribute

            _storeService.SaveState();
        }

        /// <summary>Restore state from to persistent (isolated) storage</summary>
        public override void RestoreState()
        {
            Logger.Log("AboutViewModel.RestoreState");

            try 
            { 
                //RestoreAutoSetting();  // Restore all SETTINGS marked with the [AutoSetting] attribute
                //RestoreAutoState();

                // Make sure our license is valid, otherwise prompt the user to purchase through the store
                if(!CheckLicense()) _storeService.Purchase();
            }
            catch (Exception ex)
            {
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
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }            
        }

        /// <summary>Checks the app's license</summary>
        /// <returns>Returns true if we have a valid full or trial license, therwise returns false</returns>
        private bool CheckLicense()
        {
            // Always restore store settings (which refreshes license status and increments usage counters, etc.)
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
    }
}