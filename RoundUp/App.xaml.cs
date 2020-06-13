using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Navigation;
using Cimbalino.Phone.Toolkit.Services;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using RArcher.Phone.Toolkit.Common;
using RArcher.Phone.Toolkit.Logging;
using RArcher.Phone.Toolkit.Mpns;
using RArcher.Phone.Toolkit.Network;
using RArcher.Phone.Toolkit.Store;
using RoundUp.Model;
using RoundUp.Resources;
using RoundUp.ViewModel;
using ILocationService = RArcher.Phone.Toolkit.Location.ILocationService;
using LocationService = RArcher.Phone.Toolkit.Location.LocationService;

namespace RoundUp
{
    /// <summary>
    /// roundup is a CBE (Continuoue Background Execution) app. 
    ///
    /// *** Important ********************************************************************************************
    /// 
    /// Remember, no async app event handlers (Application_Launching, Application_Closing, 
    /// Application_Activated, Application_Deactivated and Application_RunningInBackground).
    /// See: http://mark.mymonster.nl/2013/07/10/donrsquot-make-your-application-lifetime-events-async-void
    /// 
    /// **********************************************************************************************************            
    /// 
    /// We subscribe to the RunningInBackground event. We also configure ourselves
    /// for CBE in WMAppManifest.xml by setting BackgroundExecution to "LocationTracking".
    /// 
    /// CBE apps are listed in the background task control panel on the phone, in the list of "apps 
    /// that might run in the background." Because background execution potentially consumes more 
    /// battery power, the user always has the option to block any or all of these apps from running.
    /// 
    /// To qualify as as CBE app you must:
    /// 
    /// 1. Set BackgroundExecution to LocationTracking in the manifest
    /// 2. Handle the RunningInBackground and set a flag. This flag should be used to carry out
    ///    only the minumum of operations (i.e don't try and update the UI) 
    /// 3. Handle being restarted (i.e. via a toast)
    /// 4. Actively be tracking location
    /// 
    /// If the user turns off CBE, we disable location tracking when the app is deactivated, this 
    /// makes sure the OS turns off CBE (because we're not tracking location).
    /// 
    /// Because continuously using the GPS sensors can consume a lot of battery power, we force
    /// CBE off (by turning off location tracking) if there's not an active roundup session
    /// </summary>
    public partial class App : Application
    {
        /// <summary>Provides easy access to the root frame of the Phone Application</summary>
        public static PhoneApplicationFrame RootFrame { get; private set; }

        /// <summary>
        /// True if the app was deactivated (no need to recreate all objects), 
        /// false if it was tombstoned (all objects will need to be re-newed)
        /// </summary>
        public static bool IsApplicationInstancePreserved { get; set; }

        /// <summary>True if the app is running in the background, false otherwise</summary>
        public static bool IsRunningInBackground { get; private set; }

        /// <summary>True if we can (and will attempt) restore a previous session (e.g. when re-launching closed app)</summary>
        public static bool CanRestorePreviousSession { get; set; }

        /// <summary>When a view is navigated from, it can set this value if required</summary>
        public static string MostRecentView { get; set; }

        // Avoid double-initialization
        private bool _phoneApplicationInitialized = false;

        /// <summary>Constructor for the Application object</summary>
        public App()
        {
            // Global handler for uncaught exceptions.
            UnhandledException += Application_UnhandledException;

            // Standard XAML initialization
            InitializeComponent();

            // Phone-specific initialization
            InitializePhoneApplication();

            // Language display initialization
            InitializeLanguage();

            // Show graphics profiling information while debugging.
            if (Debugger.IsAttached)
            {
                // Display the current frame rate counters.
                //Application.Current.Host.Settings.EnableFrameRateCounter = true;

                // Show the areas of the app that are being redrawn in each frame.
                //Application.Current.Host.Settings.EnableRedrawRegions = true;

                // Enable non-production analysis visualization mode,
                // which shows areas of a page that are handed off to GPU with a colored overlay.
                //Application.Current.Host.Settings.EnableCacheVisualization = true;

                // Prevent the screen from turning off while under the debugger by disabling
                // the application's idle detection.
                // Caution:- Use this under debug mode only. Application that disables user idle detection will continue to run
                // and consume battery power when the user is not using the phone.
                PhoneApplicationService.Current.UserIdleDetectionMode = IdleDetectionMode.Disabled;
            }
        }

        
        /// <summary>
        /// Code to execute when the application is launching (eg, from Start)
        /// This code will not execute when the application is reactivated
        /// </summary>
        private void Application_Launching(object sender, LaunchingEventArgs e)
        {
            Logger.Log("Application Launching");
            
            IsRunningInBackground = false;
            IsApplicationInstancePreserved = false;

            // Initialize the IoC container
            InitializeIocBindings();

            // Initialize the strings resource helper
            Strings.ResourceBaseName = "RoundUp.Resources.AppResources";
        }

        /// <summary>
        /// Code to execute when the application is activated (brought to foreground)
        /// This code will not execute when the application is first launched        
        /// </summary>
        private void Application_Activated(object sender, ActivatedEventArgs e)
        {
            Logger.Log(e.IsApplicationInstancePreserved
                ? "Application Activated (was deactivated; state still in-memory, no need to restore)"
                : "Application Activated (was tombstoned; all state needs to be restored)"); 
            
            IsRunningInBackground = false;
            IsApplicationInstancePreserved = e.IsApplicationInstancePreserved;

            if(!e.IsApplicationInstancePreserved)
            {
                InitializeIocBindings();
                Strings.ResourceBaseName = "RoundUp.Resources.AppResources";
            }
        }

        /// <summary>
        /// Code to execute when the application is deactivated (sent to background)
        /// This code will not execute when the application is closing.
        /// 
        /// Because this app's registered to run in the background (CBE) this event 
        /// is NOT raised, instead the RunningInBackground event is raised
        /// </summary>
        private void Application_Deactivated(object sender, DeactivatedEventArgs e)
        {
            IsRunningInBackground = false;

            switch(e.Reason)
            {
                case DeactivationReason.ApplicationAction:
                    Logger.Log("Application Deactivated: ApplicationAction"); 
                    break; 
                
                case DeactivationReason.PowerSavingModeOn:
                    Logger.Log("Application Deactivated: PowerSavingModeOn"); 
                    break; 
                
                case DeactivationReason.ResourcesUnavailable:
                    Logger.Log("Application Deactivated: ResourcesUnavailable"); 
                    break; 
                
                case DeactivationReason.UserAction:
                    Logger.Log("Application Deactivated: UserAction"); 
                    break;
            }
        }

        /// <summary>
        // Code to execute when the application is closing (eg, user hit Back)
        // This code will not execute when the application is deactivated
        /// </summary>
        private void Application_Closing(object sender, ClosingEventArgs e)
        {
            IsRunningInBackground = false;
            Logger.Log("Application Closing");
        }

        /// <summary>
        /// Handles the RunningInBackground event. Because this app's registered to run in the background (CBE)
        /// this event is raised instead of the normal Deactivated event
        /// </summary>
        private void Application_RunningInBackground(object sender, RunningInBackgroundEventArgs e)
        {
            IsRunningInBackground = true;
            Logger.Log("Application is now running in the background (CBE)");
        }

        /// <summary>Code to execute if a navigation fails</summary>
        private void RootFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            if (Debugger.IsAttached)
            {
                // A navigation has failed; break into the debugger
                Debugger.Break();
            }
        }

        /// <summary>
        /// Code to execute on Unhandled Exceptions 
        /// </summary>
        private void Application_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            Logger.Log("An unhandled exception was just caught in App.Application_UnhandledException()");

            if (Debugger.IsAttached)
            {
                // An unhandled exception has occurred; break into the debugger
                Debugger.Break();
            }
        }

        private void InitializePhoneApplication()
        {
            if (_phoneApplicationInitialized) return;

            // Create the frame but don't set it as RootVisual yet; this allows the splash
            // screen to remain active until the application is ready to render.
            //RootFrame = new PhoneApplicationFrame();
            RootFrame = new TransitionFrame();  // Changed to use transition annimation frame

            RootFrame.Navigated += CompleteInitializePhoneApplication;

            // Handle navigation failures
            RootFrame.NavigationFailed += RootFrame_NavigationFailed;

            // Assign our URI-mapper class to the application frame
            RootFrame.UriMapper = new Common.UriMapper();

            // Handle reset requests for clearing the backstack
            RootFrame.Navigated += CheckForResetNavigation;

            // Ensure we don't initialize again
            _phoneApplicationInitialized = true;
        }

        private void CompleteInitializePhoneApplication(object sender, NavigationEventArgs e)
        {
            // Set the root visual to allow the application to render
            if (RootVisual != RootFrame) RootVisual = RootFrame;

            // Remove this handler since it is no longer needed
            RootFrame.Navigated -= CompleteInitializePhoneApplication;
        }

        private void CheckForResetNavigation(object sender, NavigationEventArgs e)
        {
            // If the app has received a 'reset' navigation, then we need to check
            // on the next navigation to see if the page stack should be reset
            if (e.NavigationMode == NavigationMode.Reset) RootFrame.Navigated += ClearBackStackAfterReset;
        }

        private void ClearBackStackAfterReset(object sender, NavigationEventArgs e)
        {
            // Unregister the event so it doesn't get called again
            RootFrame.Navigated -= ClearBackStackAfterReset;

            // Only clear the stack for 'new' (forward) and 'refresh' navigations
            if (e.NavigationMode != NavigationMode.New && e.NavigationMode != NavigationMode.Refresh) return;

            // For UI consistency, clear the entire page stack
            while (RootFrame.RemoveBackEntry() != null) 
            {
                ; // do nothing
            }
        }

        /// <summary>
        /// Initialize the app's font and flow direction as defined in its localized resource strings.
        ///
        /// To ensure that the font of your application is aligned with its supported languages and that the
        /// FlowDirection for each of those languages follows its traditional direction, ResourceLanguage
        /// and ResourceFlowDirection should be initialized in each resx file to match these values with that
        /// file's culture. For example:
        ///
        /// AppResources.es-ES.resx
        ///    ResourceLanguage's value should be "es-ES"
        ///    ResourceFlowDirection's value should be "LeftToRight"
        ///
        /// AppResources.ar-SA.resx
        ///     ResourceLanguage's value should be "ar-SA"
        ///     ResourceFlowDirection's value should be "RightToLeft"
        ///
        /// For more info on localizing Windows Phone apps see http://go.microsoft.com/fwlink/?LinkId=262072.        
        /// </summary>
        private void InitializeLanguage()
        {
            try
            {
                // Set the font to match the display language defined by the
                // ResourceLanguage resource string for each supported language.
                //
                // Fall back to the font of the neutral language if the Display
                // language of the phone is not supported.
                //
                // If a compiler error is hit then ResourceLanguage is missing from
                // the resource file.
                RootFrame.Language = XmlLanguage.GetLanguage(AppResources.ResourceLanguage);

                // Set the FlowDirection of all elements under the root frame based
                // on the ResourceFlowDirection resource string for each
                // supported language.
                //
                // If a compiler error is hit then ResourceFlowDirection is missing from
                // the resource file.
                var flow = (FlowDirection)System.Enum.Parse(typeof(FlowDirection), AppResources.ResourceFlowDirection);
                RootFrame.FlowDirection = flow;
            }
            catch
            {
                // If an exception is caught here it is most likely due to either
                // ResourceLangauge not being correctly set to a supported language
                // code or ResourceFlowDirection is set to a value other than LeftToRight
                // or RightToLeft.

                if (Debugger.IsAttached) Debugger.Break();
                throw;
            }
        }

        private void InitializeIocBindings()
        {
            // Core object and service singeltons
            IocContainer.Kernel.Bind<IStateHelper>().To<PersistentStateHelper>().InSingletonScope();
            IocContainer.Kernel.Bind<IMpnsService>().To<MpnsService>().InSingletonScope();
            IocContainer.Kernel.Bind<IRoundUpService>().To<RoundUpService>().InSingletonScope();
            IocContainer.Kernel.Bind<ILocationService>().To<LocationService>().InSingletonScope();
            IocContainer.Kernel.Bind<INetworkService>().To<NetworkService>().InSingletonScope();
            IocContainer.Kernel.Bind<IStoreService>().To<StoreService>().InSingletonScope();
            IocContainer.Kernel.Bind<IMessageBoxService>().To<MessageBoxService>().InSingletonScope();
            IocContainer.Kernel.Bind<IToastHelper>().To<ToastHelper>().InSingletonScope();

            // View models
            IocContainer.Kernel.Bind<IMainViewModel>().To<MainViewModel>().InSingletonScope();
            IocContainer.Kernel.Bind<IAboutViewModel>().To<AboutViewModel>().InSingletonScope();
            IocContainer.Kernel.Bind<ISettingsViewModel>().To<SettingsViewModel>().InSingletonScope();
            
            // Other objects and services
            IocContainer.Kernel.Bind<ISmsComposeService>().To<SmsComposeService>();
            IocContainer.Kernel.Bind<IEmailComposeService>().To<EmailComposeService>();
            IocContainer.Kernel.Bind<ISession>().To<Session>();
            IocContainer.Kernel.Bind<IInvitee>().To<Invitee>();
            IocContainer.Kernel.Bind<IInviteeLocationMarker>().To<InviteeLocationMarker>();
        }
    }
}