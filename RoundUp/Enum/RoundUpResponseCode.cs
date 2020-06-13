namespace RoundUp.Enum
{
    /// <summary>RoundUpResponseCode provides custom response code that are returned as strings from our RoundUp Azure Mobile Services</summary>
    /// <example>request.respond(statusCodes.BAD_REQUEST, "ERR_SESSION_NOT_FOUND")</example>
    public enum RoundUpResponseCode
    {
        INVALID_CODE,
        SUCCESS,
        ERR_CHANNEL_URI_NULL,
        ERR_INVALID_REQUEST_MESSAGE_ID,
        ERR_SESSION_NOT_FOUND,
        ERR_SESSION_DEAD,
        ERR_WRONG_INVITER_SHORT_DEVICE_ID,
        ERR_MPNS_NOTIFICATION_FAILED,
        ERR_INSERT_FAILED,
        ERR_UPDATE_FAILED,
        ERR_READ_FAILED,
        ERR_GENERAL_FAILURE,
        ERR_BAD_REQUEST,
        ERR_UNAUTHORIZED,
        ERR_NOT_ALLOWED,
        ERR_NOTIFICATION_LIMIT_EXCEEDED,
        ERR_TOO_MANY_INVITEES
    }
}