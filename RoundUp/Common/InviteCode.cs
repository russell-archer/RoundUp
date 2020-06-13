using System;
using System.Diagnostics;
using RArcher.Phone.Toolkit.Logging;

namespace RoundUp.Common
{
    /// <summary>A RoundUp session invite, containing the session id and the inviter's 8-char device id</summary>
    public class InviteCode
    {
        /// <summary>Session (row) Id of the session</summary>
        public int SessionId { get; set; }

        /// <summary>The inviter's short device id</summary>
        public string InviterShortDeviceId { get; set; }

        /// <summary>The inviter's name/alias</summary>
        public string InviterAlias { get; set; }

        /// <summary>Compares two InviteCode objects</summary>
        /// <param name="a">InviteCode a</param>
        /// <param name="b">InviteCode b</param>
        /// <returns>Returns true if the two InviteCode objects are identical, false otherwise</returns>
        public static bool AreIdentical(InviteCode a, InviteCode b)
        {
            try
            {
                if(a == null && b == null) return true;
                if(a == null || b == null) return false;

                if( a.SessionId == b.SessionId && 
                    string.CompareOrdinal(a.InviterAlias, b.InviterAlias) == 0 &&
                    string.CompareOrdinal(a.InviterShortDeviceId, b.InviterShortDeviceId) == 0)
                    return true;
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }

            return false;
        }
    }
}