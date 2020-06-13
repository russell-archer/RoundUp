using RArcher.Phone.Toolkit.Common;

namespace RoundUp.ViewModel
{
    /// <summary>Implements the ViewModel Locator pattern</summary>
    public class ViewModelLocator
    {
        /// <summary>Returns the ViewModel (which is a singleton) for the MainView using IocContainer bindings</summary>
        public IMainViewModel MainViewModel { get { return IocContainer.Get<IMainViewModel>(); }}

        /// <summary>Returns the ViewModel (which is a singleton) for the AboutView using IocContainer bindings</summary>
        public IAboutViewModel AboutViewModel { get { return IocContainer.Get<IAboutViewModel>(); }}

        /// <summary>Returns the ViewModel (which is a singleton) for the SettingsView using IocContainer bindings</summary>
        public ISettingsViewModel SettingsViewModel { get { return IocContainer.Get<ISettingsViewModel>(); }}    }
}