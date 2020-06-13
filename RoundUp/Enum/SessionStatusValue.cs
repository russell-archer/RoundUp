namespace RoundUp.Enum
{
    /// <summary>The various states a session can have</summary>
    public enum SessionStatusValue
    {
        NotSet,                         // 0 - Default value
        SessionStarted,                 // 1 - Session has started (new Session row, no invitees have accepted yet)
        SessionActive,                  // 2 - Session has one or more invitee (at least one invitee has accepted)
        SessionCancelledByInviter,      // 3 - Session has been cancelled by the inviter
        SessionCancelledByInvitees,     // 4 - [NOT USED] Session has been cancelled because all the invitees have cancelled
        SessionHasEnded,                // 5 - Session has ended (all invitees arrived)
        SessionAborted,                 // 6 - [NOT USED] Session has been terminated by the system 
        SessionDead                     // 7 - Session has been marked as "dead" by the Azure scheduled task script
    }
}