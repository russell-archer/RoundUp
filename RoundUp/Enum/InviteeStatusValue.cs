namespace RoundUp.Enum
{
    /// <summary>The status an invitee can have</summary>
    public enum InviteeStatusValue
    {
        NotSet,                 // 0 - Default value (i.e. InviteeSatus is not set by the client, only the RoundUp Azure service)
        InviteeHasNotResponded, // 1 - An invitation to this user has not been accepted or declined
        InviteeHasAccepted,     // 2 - An invitee has accepted
        InviteeHasDeclined,     // 3 - An invitee has declined an invitation to join the session
        InviteeHasCancelled,    // 4 - An invitee has cancelled participation in the session
        InviteeHasArrived,      // 5 - An invitee has arrived at the inviter's location 
        InviteeIsEnRoute        // 6 - The invitee is on their way to the invitee (set when a RoundUpRequestMessage.InviteeLocationUpdate request is made)
    }
}