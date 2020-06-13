using System;
using System.Diagnostics;
using RArcher.Phone.Toolkit.Common;
using RArcher.Phone.Toolkit.Logging;

namespace RoundUp.Common
{
    /// <summary>Helper class related to the invite code and invite message sent to invitees</summary>
    public static class InviteCodeHelper
    {
        /// <summary>
        /// When the app's launched using our custom rndup: uri association, the UriMapper class will
        /// collect the launch parameters and create an InviteCode in the LaunchInviteCode field. 
        /// These values can then be used in the view model during initialization. If the app launches
        /// in the normal way (without using the URI association), the properties in LaunchInviteCode
        /// will be SessionId = -1, InviterShortDeviceId = string.empty, InviterAlias = string.empty
        /// </summary>
        public static InviteCode LaunchInviteCode;

        /// <summary>Returns an InviteCode object from a free-form string invite message</summary>
        /// <param name="text">The invite message</param>
        /// <returns>Returns an InviteCode object from a free-form string invite message</returns>
        public static InviteCode Parse(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("rndup://", StringComparison.Ordinal) == -1)
            {
                MessageBoxHelper.Show(Strings.GetStringResource("MissingInviteCode"), string.Empty, false);
                return null;
            }
        
            // Parse the invite, inviter's short device id code and inviter's name from the invite text.
            // The invite code will look like this: "rndup://sessionId?did=shortDeviceId&nme=inviter"
            //
            //      sessionId   = the Azure Session table row id of the session
            //      did         = the inviter's short device id (8 chars). Used as a basic security "pin code"
            //      nme         = the inviter's name/alias (50 chars max, variable length, http encoded)

            var inviteCode = new InviteCode();
        
            try
            {
                var inviteCodeText = text.Substring(text.IndexOf("rndup://", StringComparison.Ordinal)).Trim();
                // We now have: "rndup://xxx?did=yyyyyyyy&nme=nnnn {anything}", 
                // where y is an 8-char alphanumeric (a..z, A..Z or 0..9), xxx is an int, nnnn is a var number of chars,

                var tmp = inviteCodeText.Substring("rndup://".Length);  // --> "xxx?did=yyyyyyyy&nme=nnnn {anything}"

                var queryIndex = tmp.IndexOf("?did=", StringComparison.Ordinal);
                if (queryIndex == -1) throw new Exception();

                // Pick-off the session id
                var tmpSessionId = tmp.Remove(queryIndex);
                inviteCode.SessionId = int.Parse(tmpSessionId);

                // Now get the 8-character (it's always 8-chars, guaranteed) short device id
                tmp = tmp.Substring(queryIndex + "?did=".Length);  // --> "yyyyyyyy&nme=nnnn {anything}"
                inviteCode.InviterShortDeviceId = tmp.Substring(0, 8);

                // Now get the variable-length inviter name
                tmp = tmp.Substring("yyyyyyyy&nme=".Length);  // --> "nnnn {anything}"

                // Find the end of the custom uri - this will be a space, or the end of the string
                queryIndex = tmp.IndexOf(" ", StringComparison.Ordinal);
                inviteCode.InviterAlias = queryIndex == -1 ? tmp : tmp.Substring(0, queryIndex);
                inviteCode.InviterAlias = Uri.UnescapeDataString(inviteCode.InviterAlias);

                return inviteCode;
            }
            catch(Exception ex)
            {
                MessageBoxHelper.Show(Strings.GetStringResource("BadInviteCode"), string.Empty, false);
                Logger.Log(ex, new StackFrame(0, true));
                return null;
            }
        }

        /// <summary>Returns an InviteCode object from a custom uri association which may have been used to launch the app</summary>
        /// <param name="text">The invite message</param>
        /// <returns>Returns an InviteCode object from a custom uri association (see UriMapper)</returns>
        public static InviteCode ParseUriAssociation(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("rndup://", StringComparison.Ordinal) == -1) return null;

            // Parse the invite, inviter's short device id code and inviter's name from the uri association text...
            // The uri text will look like this: "/Protocol?encodedLaunchUri=rndup://sid/?did=yyyyyyyy&nme=nnnnn"
            // or this:                          "/Protocol?encodedLaunchUri=rndup://sid?did=yyyyyyyy&nme=nnnnn"

            var inviteCode = new InviteCode();

            try
            {
                var inviteCodeText = text.Substring(text.IndexOf("rndup://", StringComparison.Ordinal)).Trim();
                // We now have: "rndup://sid/?did=yyyyyyyy&nme=nnnn {anything}", or "sid?did=yyyy&nme=nnnn {anything}"
                // where y is an 8-char alphanumeric (a..z, A..Z or 0..9), xxx is an int, nnnn is a var number of chars,

                var tmp = inviteCodeText.Substring("rndup://".Length);  // --> "sid/?did=yyyyyyyy&nme=nnnn {anything}" or "sid?did=yyyyyyyy&nme=nnnn {anything}"

                // Decide if the leading "/" is present 
                int didLength;
                var queryIndex = tmp.IndexOf("/?did=", StringComparison.Ordinal);
                if(queryIndex == -1)
                {
                    queryIndex = tmp.IndexOf("?did=", StringComparison.Ordinal);
                    if(queryIndex == -1) throw new Exception();
                    didLength = "?did=".Length;
                }
                else didLength = "/?did=".Length;

                // Pick-off the session id
                var tmpSessionId = tmp.Remove(queryIndex);
                inviteCode.SessionId = int.Parse(tmpSessionId);

                // Now get the 8-character (it's always 8-chars, guaranteed) short device id
                tmp = tmp.Substring(queryIndex + didLength);  // --> "yyyyyyyy&nme=nnnn {anything}"
                inviteCode.InviterShortDeviceId = tmp.Substring(0, 8);

                // Now get the variable-length inviter name
                tmp = tmp.Substring("yyyyyyyy&nme=".Length);  // --> "nnnn {anything}"

                // Find the end of the custom uri - this will be a space, or the end of the string
                queryIndex = tmp.IndexOf(" ", StringComparison.Ordinal);
                inviteCode.InviterAlias = queryIndex == -1 ? tmp : tmp.Substring(0, queryIndex);
                inviteCode.InviterAlias = Uri.UnescapeDataString(inviteCode.InviterAlias);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return null;
            }

            return inviteCode;
        }

        /// <summary>Returns a nicely formatted message that can be send via sms/email to invitees</summary>
        /// <param name="sessionId">Session (row) Id of the session</param>
        /// <param name="shortDeviceId">The inviter's short device id</param>
        /// <param name="inviterName">The name/alias of the inviter</param>
        /// <param name="codeOnly">If true, does not add the friendly invite text to the code (just the inviter's alias)</param>
        /// <returns>Returns a nicely formatted message that can be send via sms/email to invitees</returns>
        public static string CreateInviteCodeText(int sessionId, string shortDeviceId, string inviterName, bool codeOnly = false)
        {
            try
            {
                // Create text that the inviter can send (i.e. via SMS, Email, etc.) to invite 
                // others to meet at the RoundUp point
                if(string.IsNullOrEmpty(inviterName)) inviterName = DeviceHelper.DeviceName();
                var inviterNameEncoded = Uri.EscapeDataString(inviterName);
                var inviteCodeText = string.Format("{0}\nrndup://{1}?did={2}&nme={3}", codeOnly ? Strings.Get("InviteMessageShort") : Strings.Get("InviteMessage"), sessionId, shortDeviceId, inviterNameEncoded);

                // Replace "{alias}" in InviteCodeText with the user's alias/name
                return inviteCodeText.Replace("{alias}", inviterName);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return string.Empty;
            }
        }
    }
}