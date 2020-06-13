function update(item, user, request) {

    // The item parameter represents the object being updated in the 
    // Azure Mobile Services (AMS) database (a Session object).

    if (item.Channel.length === 0) {
        request.respond(statusCodes.BAD_REQUEST, "ERR_CHANNEL_URI_NULL");
        return;
    }

    var sessionTable = tables.getTable("Session");
    var roundUpNotificationTable = tables.getTable("RoundUpNotification");

    // Get the session row to check details
    sessionTable.where({ id: item.id }).read({
        success: function (rows) {

            if (rows.length !== 1) {
                request.respond(statusCodes.BAD_REQUEST, "ERR_SESSION_NOT_FOUND");
                return;
            }

            // Get the Session row that was returned
            var sessionRow = rows[0];

            // Does the incoming session object have the same ShortDeviceId as the existing row
            if (sessionRow.ShortDeviceId !== item.ShortDeviceId) {
                request.respond(statusCodes.BAD_REQUEST, "ERR_WRONG_INVITER_SHORT_DEVICE_ID");
                return;
            }

            // RequestMessageId processing:
            var payloadMsg;

            // SessionHasEnded (=9) - All invitees have arrived - close the session (no invitees will be able to join)
            if (item.RequestMessageId === 9) {
                request.execute({
                    success: function () {

                        // Notify the inviter that the session has closed
                        payloadMsg = "{" +
                                     "  id:" + -1 + "," +
                                     "  Recipient:" + 0 + "," +
                                     "  SessionId:" + item.id.toString() + "," +
                                     "  InviteeId:" + -1 + "," +
                                     "  MessageId:" + "'SessionHasEnded'" + "," +
                                     "  Data:" + "' '" + "," +
                                     "  ShortDeviceId:" + "'" + item.ShortDeviceId + "'" + "," +
                                     "  Latitude:" + item.Latitude.toString() + "," +
                                     "  Longitude:" + item.Longitude.toString() +
                                     "}";

                        var notification =
                        {
                            Recipient: 0,
                            SessionId: item.id,
                            InviteeId: -1,
                            MessageId: "SessionHasEnded",
                            Data: "",
                            ShortDeviceId: item.ShortDeviceId,
                            Latitude: item.Latitude,
                            Longitude: item.Longitude
                        };

                        // Insert the notification into the RoundUpNotification table
                        roundUpNotificationTable.insert(notification, {
                            error: function (err) {
                                
                                console.error(err + " Unable to insert into RoundUpNotification table (100)");
                            }
                        });
                        
                        // Send a notifiction to the inviter using the MPNS
                        push.mpns.sendRaw(item.Channel, { payload: payloadMsg }, {
                            success: function () {

                                console.log("MPNS: SessionHasEnded sent to inviter. SessionId = " + item.id);
                                request.respond();  // Success
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

                        console.error(err + " Session update failed (102)");
                        request.respond(statusCodes.BAD_REQUEST, "ERR_UPDATE_FAILED");
                        return;
                    }
                });
            }

            // UpdateInviterChannelUri (=10) - Update the session inviter's Channel URI
            if (item.RequestMessageId === 10) {
                request.execute({
                    
                    success: function () {
                        
                        request.respond();  // Success
                        return;
                    },
                    error: function (err) {

                        console.error(err + " Session update failed (103)");
                        request.respond(statusCodes.BAD_REQUEST, "ERR_UPDATE_FAILED");
                        return;
                    }
                });
            }

            // SessionCancel (=2) - Inviter is cancelling a session (a SessionCancelledByInviter notification will be sent to all invitees)
            if (item.RequestMessageId === 2) {
                request.execute({
                    success: function () {
                        
                        // We don't need to send a confirmation notification to the inviter - the roundup app takes
                        // the necessary actions to close the session on the client, even if we run into errors
                        
                        // We do need to add a SessionCancelledByInviter notification to the RoundUpNotification 
                        // table to signal that the session is dead (recipient is inviter)
                        var notification =
                        {
                            Recipient: 0,
                            SessionId: item.id,
                            InviteeId: -1,
                            MessageId: "SessionCancelledByInviter",
                            Data: item.Name,
                            ShortDeviceId: item.ShortDeviceId,
                            Latitude: item.Latitude,
                            Longitude: item.Longitude
                        };

                        roundUpNotificationTable.insert(notification, {
                            error: function (err) {
                                
                                // Non-fatal error
                                console.error(err + " Unable to insert into RoundUpNotification table (104)");
                            }
                        });
                    
                        // Get all invitees and send them a SessionCancelledByInviter notification
                        var inviteeTable = tables.getTable("Invitee");
                        
                        inviteeTable.where({ sid: item.id }).read({
                            success: function (inviteeRows) {
                                
                                if (inviteeRows.length === 0) {
                                    
                                    request.respond();  // Success - there were no invitees on the session
                                    return;
                                }

                                var notificationInvitee;
                                
                                inviteeRows.forEach(function (row) {

                                    // We only send cancel messages to invitees with a status of
                                    // InviteeHasAccepted (2) or InviteeIsEnRoute (6)
                                    if (row.InviteeStatusId === 2 || row.InviteeStatusId === 6) {
                                        
                                        payloadMsg = "{" +
                                            "  id:" + -1 + "," +
                                            "  Recipient:" + 1 + "," +
                                            "  SessionId:" + item.id.toString() + "," +
                                            "  InviteeId:" + row.id + "," +
                                            "  MessageId:" + "'SessionCancelledByInviter'" + "," +
                                            "  Data:" + "'" + item.Name + "'" + "," +
                                            "  ShortDeviceId:" + "'" + item.ShortDeviceId + "'" + "," +
                                            "  Latitude:" + 0 + "," +
                                            "  Longitude:" + 0 +
                                            "}";

                                        notificationInvitee =
                                        {
                                            Recipient: 0,
                                            SessionId: item.id,
                                            InviteeId: row.id,
                                            MessageId: "SessionCancelledByInviter",
                                            Data: item.Name,
                                            ShortDeviceId: item.ShortDeviceId,
                                            Latitude: 0,
                                            Longitude: 0
                                        };
                                        
                                        roundUpNotificationTable.insert(notificationInvitee, {
                                            error: function (err) {

                                                // Non-fatal error
                                                console.error(err + " Unable to insert into RoundUpNotification table (105)");
                                            }
                                        });
                                        
                                        // Send a notifiction to the invitee using the MPNS
                                        push.mpns.sendRaw(row.Channel, { payload: payloadMsg }, {
                                            
                                            success: function () {

                                                console.log("MPNS: SessionCancelledByInviter sent to InviteeId = " + row.id + ", SessionId = " + item.id);
                                            },
                                            error: function(err) {

                                                // Is it a non-fatal error (can happen when the device is off-line)?
                                                if (isRealMpnsError(err)) {

                                                    logMpnsResult(err, "Session update failed (106)");

                                                    if (notificationLimitExceeded(err)) {

                                                        request.respond(statusCodes.BAD_REQUEST, "ERR_NOTIFICATION_LIMIT_EXCEEDED");
                                                        return;
                                                    }
                                                }
                                            }
                                        });
                                    }
                                });
                                
                                request.respond();  // Success
                                return;
                            },
                            error: function (err) {
                                
                                // Couldn't read invitee rows
                                console.error(err + " Session update failed (107)");
                                request.respond(statusCodes.BAD_REQUEST, "ERR_UPDATE_FAILED");
                                return;
                            }
                        });
                        
                    },
                    error: function (err) {

                        console.error(err + " Session update failed (108)");
                        request.respond(statusCodes.BAD_REQUEST, "ERR_UPDATE_FAILED");
                        return;
                    }
                });
            }
            
            // Add more RequestMessageId processing here as required...
            return;
        },
        // -----------> error: session row read                
        error: function (err) {

            console.error(err + " Session update failed: unable to read Session table (109)");
            request.respond(statusCodes.BAD_REQUEST, "ERR_READ_FAILED");
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
