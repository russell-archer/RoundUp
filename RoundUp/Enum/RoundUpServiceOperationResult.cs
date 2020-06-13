namespace RoundUp.Enum
{
    /// <summary>Results that a RoundUp Service (Azure SQL) operation can have</summary>
    public enum RoundUpServiceOperationResult
    {
        OperationFailure,               // The op failed. Retry unlikely to succeed
        OperationFailureCanRetry,       // The op failed but should be retried 
        OperationSuccess,               // The op succeeded
        MobileServiceInvalidOperation,  // Azure Mobile Services threw a MobileServiceInvalidOperationException
        SessionDoesNotExist,            // The search for a session (using the session id) returned no results
        SessionIsNotAlive,              // The requested session was found but it's status is not one of SessionStarted | SessionActive    
        ChannelUriNull,                 // The channel uri passed to Azure was null
        InvalidRequest,                 // The request id was invalid or used out of context
        InvalidInviterShortDeviceId,    // The inviter's short device id didn't match what was expected
        MpnsNotificationFailed,         // A request to send a notifcation via MPNS failed
        InsertFailed,                   // An insert failed
        UpdateFailed,                   // An update failed
        ReadFailed,                     // A read operation failed
        GeneralFailure,                 // Some general error occurred
        OperationNotSupported,          // Not implemented/supported yet
        BadRequest,                     // Request was malformed
        Unauthorized,                   // Would normally happen when using certified (no-limit) MPNS
        NotAllowed,                     // The wrong http verb was used 
        MpnsNotificationLimitExceeded,  // We've exceeded the 500 notification-pre-day limit for the device + app combination
        TooManyInvitees                 // Can't add another invitee to the session - we've hit our maximum limit 
    }
}