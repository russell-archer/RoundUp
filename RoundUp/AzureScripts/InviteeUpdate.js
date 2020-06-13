function update(item, user, request) {

    // The item parameter represents the object being updated into the 
    // Azure Mobile Services database (an Invitee object)

    //> Update the Invitee table
    request.execute({
        success: function () {

            var sessionTable = tables.getTable("Session");

            // -------> Get the session channel uri
            sessionTable.where({ id: item.sid }).read({
                success: function (rows) {

                    if (rows.length !== 1) {
                        request.respond(statusCodes.BAD_REQUEST, "ERR_SESSION_NOT_FOUND");
                        return;
                    }

                    // Get the Session row that was returned
                    var sessionRow = rows[0];

                    // Check to see if the session is *alive* (SessionStarted (=1) or SessionActive (=2))
                    if (sessionRow.SessionStatusId === 0 || sessionRow.SessionStatusId > 2) {
                        request.respond(statusCodes.BAD_REQUEST, "ERR_SESSION_DEAD");
                        return;
                    }

                    // Does the session have the same ShortDeviceId that has been given to the invitee as 
                    // part of the invite code?
                    if (sessionRow.ShortDeviceId !== item.InviterShortDeviceId) {
                        request.respond(statusCodes.BAD_REQUEST, "ERR_WRONG_INVITER_SHORT_DEVICE_ID");
                        return;
                    }

                    // ---------------> RequestMessageId processing:
                    var payloadMsg;

                    // InviteeLocationUpdate (=5). An invitee has changed location (InviteeLocationUpdate will be sent to the inviter)
                    if (item.RequestMessageId === 5) {

                        // Notify the inviter that an invitee changed location
                        payloadMsg = "{" +
                                     "  id:" + -1 + "," +
                                     "  Recipient:" + 0 + "," +
                                     "  SessionId:" + item.sid.toString() + "," +
                                     "  InviteeId:" + item.id.toString() + "," +
                                     "  MessageId:" + "'InviteeLocationUpdate'" + "," +
                                     "  Data:" + "'" + item.Name + "'" + "," +
                                     "  ShortDeviceId:" + "'" + item.InviterShortDeviceId + "'" + "," +
                                     "  Latitude:" + item.Latitude.toString() + "," +
                                     "  Longitude:" + item.Longitude.toString() +
                                     "}";

                        // Send a notifiction to the inviter using the MPNS
                        push.mpns.sendRaw(sessionRow.Channel, { payload: payloadMsg }, {
                            success: function () {

                                request.respond();  // Success
                                return;
                            },
                            error: function (err) {

                                // Is it a non-fatal error (can happen when the device is off-line)?
                                if (isRealMpnsError(err)) {

                                    logMpnsResult(err, "InviteeLocationUpdate failed (100)");

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
                    }

                    // InviteeHasArrived (=8). An invitee has arrived at the roundup location (InviteeHasArrived will be sent to the inviter)
                    if (item.RequestMessageId === 8) {

                        // Notify the inviter that an invitee has arrived
                        payloadMsg = "{" +
                                     "  id:" + -1 + "," +
                                     "  Recipient:" + 0 + "," +
                                     "  SessionId:" + item.sid.toString() + "," +
                                     "  InviteeId:" + item.id.toString() + "," +
                                     "  MessageId:" + "'InviteeHasArrived'" + "," +
                                     "  Data:" + "'" + item.Name + "'" + "," +
                                     "  ShortDeviceId:" + "'" + item.InviterShortDeviceId + "'" + "," +
                                     "  Latitude:" + item.Latitude.toString() + "," +
                                     "  Longitude:" + item.Longitude.toString() +
                                     "}";

                        var notificationInviteeHasArrived =
                        {
                            Recipient: 0,
                            SessionId: item.sid,
                            InviteeId: item.id,
                            MessageId: "InviteeHasArrived",
                            Data: item.Name,
                            ShortDeviceId: item.InviterShortDeviceId,
                            Latitude: item.Latitude,
                            Longitude: item.Longitude
                        };

                        // Insert the notification for the inviter into the RoundUpNotification table
                        tables.getTable("RoundUpNotification").insert(notificationInviteeHasArrived, {
                            error: function (err) {

                                console.error(err + " Unable to insert into RoundUpNotification table (101)");
                            }
                        });

                        // Send a notifiction to the inviter using the MPNS
                        push.mpns.sendRaw(sessionRow.Channel, { payload: payloadMsg }, {
                            success: function () {

                                console.log("MPNS: InviteeHasArrived (InviteeId = " + item.id + ") sent to inviter. SessionId = " + item.sid);
                                request.respond();  // Success
                                return;
                            },
                            error: function (err) {

                                // Is it a non-fatal error (can happen when the device is off-line)?
                                if (isRealMpnsError(err)) {

                                    logMpnsResult(err, "Invitee update failed (102)");

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
                    }

                    // UpdateInviteeChannelUri (=11). Update the Invitee table ChannelUri for the specified SessionID and InviteeId
                    if (item.RequestMessageId === 11) {

                        request.respond();  // Success
                        return;
                    }

                    // InviteeCancel (=4). Invitee is cancelling their participation in the session
                    if (item.RequestMessageId === 4) {

                        // Notify the inviter that an invitee has cancelled
                        payloadMsg = "{" +
                                     "  id:" + -1 + "," +
                                     "  Recipient:" + 0 + "," +
                                     "  SessionId:" + item.sid.toString() + "," +
                                     "  InviteeId:" + item.id.toString() + "," +
                                     "  MessageId:" + "'InviteeHasCancelled'" + "," +
                                     "  Data:" + "'" + item.Name + "'" + "," +
                                     "  ShortDeviceId:" + "'" + item.InviterShortDeviceId + "'" + "," +
                                     "  Latitude:" + item.Latitude.toString() + "," +
                                     "  Longitude:" + item.Longitude.toString() +
                                     "}";

                        var notificationInviteeHasCancelled =
                        {
                            Recipient: 0,
                            SessionId: item.sid,
                            InviteeId: item.id,
                            MessageId: "InviteeHasCancelled",
                            Data: item.Name,
                            ShortDeviceId: item.InviterShortDeviceId,
                            Latitude: item.Latitude,
                            Longitude: item.Longitude
                        };

                        // Insert the notification for the inviter into the RoundUpNotification table
                        tables.getTable("RoundUpNotification").insert(notificationInviteeHasCancelled, {
                            error: function (err) {

                                console.error(err + " Unable to insert into RoundUpNotification table (103)");
                            }
                        });

                        // Send a notifiction to the inviter using the MPNS
                        push.mpns.sendRaw(sessionRow.Channel, { payload: payloadMsg }, {
                            success: function () {

                                console.log("MPNS: InviteeHasCancelled (InviteeId = " + item.id + ") sent to inviter. SessionId = " + item.sid);
                                request.respond();  // Success
                                return;
                            },
                            error: function (err) {

                                // Is it a non-fatal error (can happen when the device is off-line)?
                                if (isRealMpnsError(err)) {

                                    logMpnsResult(err, "Invitee update failed (104)");

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
                    }

                    // Add more RequestMessageId processing here as required...
                    return;
                },
                // -----------> error: session channel uri                
                error: function (err) {

                    console.error(err + " Invitee update failed: unable to read Session table (105)");
                    request.respond(statusCodes.BAD_REQUEST, "ERR_READ_FAILED");
                    return;
                }
            });
        },
        // ---> error: Update the Invitee table        
        error: function (err) {

            console.error(err + " Invitee update failed (106)");
            request.respond(statusCodes.BAD_REQUEST, "ERR_UPDATE_FAILED");
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
