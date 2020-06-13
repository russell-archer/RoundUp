function insert(item, user, request) {

    // The item parameter represents the object being inserted into the 
    // Azure Mobile Services (AMS) database (a Session object)

    if (item.Channel.length === 0) {
        request.respond(statusCodes.BAD_REQUEST, "ERR_CHANNEL_URI_NULL");
        return;
    }

    // A Session row insert should be accompanied by a RequestMessageId of 1 (SessionStart)
    if (item.RequestMessageId !== 1) {
        request.respond(statusCodes.BAD_REQUEST, "ERR_INVALID_REQUEST_MESSAGE_ID");
        return;
    }

    // Set the Session status to SessionStarted (= 1) (see SessionStatusValue enum)
    item.SessionStatusId = 1;

    request.execute({
        success: function () {

            // Note that the id value for the row-object being inserted should NOT be 
            // defined by the app calling the AMS InsertAsync() method. It will be 
            // auto-generated during the database insert operation. The item parameter 
            // will then have its id field populated with the row id value.

            // The MPNS payload has to be a string, so construct a json string
            // containing the required object. Notice that we send the id of the
            // new row back to the caller as part of the MPNS message payload.
            // The payload should conform to the IRoundUpNotification interface:
            //
            //     public interface IRoundUpNotification
            //     {
            //         int id;
            //         int Recipient { get; set; }
            //         int SessionId { get; set; }
            //         int InviteeId { get; set; } 
            //         string MessageId { get; set; } --> see RoundUpNotificationMessage enum
            //         string Data { get; set; }
            //         string ShortDeviceId { get; set; }
            //         double Latitude { get; set; }
            //         double Longitude { get; set; }
            //     }

            // Notify the inviter
            var payloadMsg = "{" +
                             "  id:" + -1 + "," +
                             "  Recipient:" + 0 + "," +
                             "  SessionId:" + item.id.toString() + "," +
                             "  InviteeId:" + -1 + "," +
                             "  MessageId:" + "'SessionStarted'" + "," +
                             "  Data:" + "''" + "," +
                             "  ShortDeviceId:" + "'" + item.ShortDeviceId + "'" + "," +
                             "  Latitude:" + item.Latitude.toString() + "," +
                             "  Longitude:" + item.Longitude.toString() +
                             "}";

            var notification =
            {
                Recipient: 0,
                SessionId: item.id,
                InviteeId: -1,
                MessageId: "SessionStarted",
                Data: "",
                ShortDeviceId: item.ShortDeviceId,
                Latitude: item.Latitude,
                Longitude: item.Longitude
            };

            // Insert the notification into the RoundUpNotification table
            var roundUpNotificationTable = tables.getTable("RoundUpNotification");
            roundUpNotificationTable.insert(notification, {

                error: function (err) {
                    
                    console.error(err + " Unable to insert into RoundUpNotification table (100)");
                }
            });
            
            push.mpns.sendRaw(item.Channel, { payload: payloadMsg }, {
                success: function () {

                    console.log("MPNS: SessionStarted sent to inviter. SessionId = ", item.id);
                    request.respond();  // Success - notification sent OK (device was online)
                    return;
                },
                error: function (err) {

                    // Is it a non-fatal error (can happen when the device is off-line)?
                    if (isRealMpnsError(err)) {

                        logMpnsResult(err, "Session update failed (101)");

                        if (notificationLimitExceeded(err)) {

                            request.respond(statusCodes.BAD_REQUEST, "ERR_NOTIFICATION_LIMIT_EXCEEDED");
                            return;
                        }
                    }

                    // Report success, even though the notification failed, which can happen if the device is off-line.
                    // The notification will have been inserted into the RoundUpNotification table and will "replayed"
                    // by the client app once the device is active
                    request.respond();  
                    return;
                }
            });
        },
        error: function (err) {

            console.error(err + " Session insert failed (102)");
            request.respond(statusCodes.BAD_REQUEST, "ERR_INSERT_FAILED");
            return;
        }
    });
}

function logMpnsResult(result, message) {

    console.error(
        "MPNS failure: " +
        message +
        ", statusCode = " +
        result.statusCode +
        ", notificationStatus = " +
        result.notificationStatus +
        ", deviceConnectionStatus = " +
        result.deviceConnectionStatus +
        ", subscriptionStatus = " +
        result.subscriptionStatus);
}

function isRealMpnsError(mpnsResult) {

    // If the status code of an mpns operation is one of the following values we consider 
    // it not to be an error, for the reason given:
    //
    // 200 : Not an error. Op succeeded
    // 404 : Not found. Device is off-line (e.g. no network or the client app is not the active foreground app)
    // 412 : Precondition failed. An internal MPNS error - keeping trying to send future messages
    // 503 : Service Unavailable. MPNS is not available. Try again in future
    //
    // The following status codes ARE errors. Scripts should check for these and send the client the 
    // appropriate RoundUpResponseCode:
    //
    // 400 : Bad request (ERR_BAD_REQUEST). Something was malformed
    // 401 : Unauthorized (ERR_UNAUTHORIZED). Normally happens with MPNS certificate errors
    // 405 : Not allowed (ERR_NOT_ALLOWED). Bad HTTP verb used
    // 406 : Not acceptable (ERR_NOTIFICATION_LIMIT_EXCEEDED). The device + app combination has exceeded the 500 notification limit

    if (mpnsResult.statusCode == 200 ||
        mpnsResult.statusCode == 404 ||
        mpnsResult.statusCode == 412 ||
        mpnsResult.statusCode == 503)
        return false;

    return true;
}

function notificationLimitExceeded(mpnsResult) {

    if (mpnsResult.statusCode == 406) return true;
    return false;
}
