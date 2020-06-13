namespace RoundUp.Enum
{
    /// <summary>Enum for messages sent as part of a request to the RoundUp Azure service</summary>
    public enum RoundUpRequestMessage
    {
        InvalidMessage,             // 0  - Illegal message
        SessionStart,               // 1  - Inviter is requesting a session to start (no invitees have accepted yet)
        SessionCancel,              // 2  - Inviter is cancelling a session (a SessionCancelledByInviter notification will be sent to all invitees)
        InviteeJoin,                // 3  - Invitee is joining the session
        InviteeCancel,              // 4  - Invitee is canceling their participation in the session
        InviteeLocationUpdate,      // 5  - An invitee has changed location (a InviteeLocationUpdate will be sent to the inviter)
        RoundUpLocationChange,      // 6  - The inviter wants to change the location of the RoundUp point
        InstantMessage,             // 7  - An instant message request (can come from either the inviter or an invitee)
        InviteeHasArrived,          // 8  - An invitee has arrived at the roundup location
        SessionHasEnded,            // 9  - All invitees have arrived - close the session (no invitees will be able to join)
        UpdateInviterChannelUri,    // 10 - Update the Session table ChannelUri for the specified SessionID
        UpdateInviteeChannelUri     // 11 - Update the Invitee table ChannelUri for the specified SessionID and InviteeId
    }
}