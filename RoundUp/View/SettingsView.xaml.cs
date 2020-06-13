using System;
using Microsoft.Phone.Controls;
using RoundUp.ViewModel;

namespace RoundUp.View
{
    public partial class SettingsView : PhoneApplicationPage
    {
        /// <summary>The View's ViewModel</summary>
        public ISettingsViewModel ViewModel { get; set; }

        public SettingsView()
        {
            InitializeComponent();

            // Get a reference to our view model, which has been set in XAML
            ViewModel = this.DataContext as ISettingsViewModel;
            if(ViewModel == null) throw new NullReferenceException();
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

            // If necessary, flag that we're returning to the main view from within the app, and that the app's
            // objects and state are preserved. This flag is normally set in App.xaml.cs when the app is launched
            // or activated, but when navigating between view the Application_Launching and Application_Activated
            // handlers are not called
            if(e.Uri != null && e.Uri.ToString().Contains(typeof(MainView).Name)) App.IsApplicationInstancePreserved = true;

            // This value can be used by the destination view/view model to decide if special processing is required.
            // For example, if the destination is the main view and the previous view may have modified some shared
            // settings values, the main view model will want to restore those settings
            App.MostRecentView = this.GetType().Name;
        }
    }
}