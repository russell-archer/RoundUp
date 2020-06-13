namespace RoundUp.Enum
{
    /// <summary>
    /// Enum for notifications sent by the MPNS as requested by the RoundUp Azure service.
    /// [REPLAYABLE]     => the app will resend (to iself) messages of this type if they were missed while the app was deactivated, etc.
    /// [NOT REPLAYABLE] => the Azure cloud service does NOT save these messages for possible replay later
    /// </summary>
    public enum RoundUpNotificationMessage
    {
        InvalidMessage,                 // 0  - [NOT REPLAYABLE] Illegal message
        SessionStarted,                 // 1  - [REPLAYABLE]     Session has started (new Session row, no invitees have accepted yet) 
        SessionCancelledByInviter,      // 2  - [REPLAYABLE]     Session has been cancelled by the inviter 
        SessionCancelledByInvitees,     // 3  - [NOT USED]       Session has been cancelled because all the invitees have cancelled 
        SessionHasEnded,                // 4  - [REPLAYABLE]     Session has ended (all invitees arrived)
        SessionAborted,                 // 5  - [REPLAYABLE]     Session has been terminated by the system (e.g. timed-out, etc.)
        InviteeHasAccepted,             // 6  - [REPLAYABLE]     An invitee has accepted
        InviteeHasDeclined,             // 7  - [NOT USED]       An invitee has declined an invitation to join the session
        InviteeHasCancelled,            // 8  - [REPLAYABLE]     An invitee has cancelled participation in the session
        InviteeHasArrived,              // 9  - [REPLAYABLE]     An invitee has arrived at the inviter's location  
        InviteeLocationUpdate,          // 10 - [NOT REPLAYABLE] An invitee has changed location (sent to the inviter)
        RoundUpLocationChange,          // 11 - [NOT REPLAYABLE] The inviter has changed the RoundUp point location
        InstantMessage                  // 12 - [REPLAYABLE]     An instant message has been received (from either the inviter or an invitee)
    }
}