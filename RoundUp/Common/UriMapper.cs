using System;
using System.Diagnostics;
using System.Net;
using System.Windows.Navigation;
using RArcher.Phone.Toolkit.Logging;

namespace RoundUp.Common
{
    /// <summary>This class handles the URI the app was launched via. Any parameters passed are parsed and stored for later use</summary>
    class UriMapper : UriMapperBase
    {
        /// <summary>
        /// If the app was launched using our custom uri association - pick off the
        /// session id, device id and inviter's name parameters and pass them in the 
        /// InviteCodeHelper.LaunchInviteCode field to the view model
        /// 
        /// If using the uri association, the incoming uri will the following format:
        ///
        /// ReSharper disable CSharpWarnings::CS1570
        ///
        ///      /Protocol?encodedLaunchUri=rndup://sid/?did=yyyy&nme=nnnn
        /// 
        /// ReSharper restore CSharpWarnings::CS1570
        ///
        /// where sid = session id, did = short device id, nme = inviter name
        /// </summary>
        /// <param name="uri">The uri passed to the app</param>
        /// <returns>Returns a URI to be used to launch the app's start-up page</returns>
        public override Uri MapUri(Uri uri)
        {
            try
            {
                // Handle Uri mapping...
                var uriText = HttpUtility.UrlDecode(uri.ToString());

                // Parse the invite code text. Result may be null, which is valid (= no startup params)
                var tmpLaunchCode = InviteCodeHelper.ParseUriAssociation(uriText);

                // Have we already found this invite code (for some reason MapUri sometimes gets called twice)?
                if(!InviteCode.AreIdentical(tmpLaunchCode, InviteCodeHelper.LaunchInviteCode))
                {
                    InviteCodeHelper.LaunchInviteCode = tmpLaunchCode;

                    if(Debugger.IsAttached && InviteCodeHelper.LaunchInviteCode != null)
                    {
                        var tmp = string.Format("Detected launch invite code: SessionId = {0}, InviterShortDeviceId = {1}, InviterAlias = {2}", InviteCodeHelper.LaunchInviteCode.SessionId, InviteCodeHelper.LaunchInviteCode.InviterShortDeviceId, InviteCodeHelper.LaunchInviteCode.InviterAlias);

                        Logger.Log(tmp);
                    }
                }

                if(tmpLaunchCode == null) return uri;  // Just use the default navigation uri (there were no params)

                // Re-construct the view uri, having picked off the start-up params
                var launchUri = string.Format("/View\\MainView.xaml");
                return new Uri(launchUri, UriKind.Relative);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                var launchUri = string.Format("/View\\MainView.xaml");
                return new Uri(launchUri, UriKind.Relative);            
            }
        }
    }
}