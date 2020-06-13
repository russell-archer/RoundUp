function insert(item, user, request) {

    // The item parameter represents the object being inserted into the 
    // Azure Mobile Services database (an Invitee object)

    // Constant: Maximum number of invitees allowed on a session
    var maxInviteeCount = 10;
    
    if (item.Channel.length === 0) {
        request.respond(statusCodes.BAD_REQUEST, "ERR_CHANNEL_URI_NULL");
        return;
    }

    // An Invitee row insert should be accompanied by a RequestMessageId of 3 (InviteeJoin)
    if (item.RequestMessageId !== 3) {
        request.respond(statusCodes.BAD_REQUEST, "ERR_INVALID_REQUEST_MESSAGE_ID");
        return;
    }

    // Set the Invitee status for the row to be InviteeHasAccepted (= 2) (see InviteeStatusValue enum)
    item.InviteeStatusId = 2;

    // Get the Session table
    var sessionTable = tables.getTable("Session");

    // Get the invitee table so we can see how many invitees this session already has
    var inviteeTable = tables.getTable("Invitee");

    // Before we insert the new Invitee row, update the Session table so that the SessionStatusId 
    // is set to SessionActive (=2) and the RequestMessageId = InviteeJoin (3). If this fails, 
    // then we abandon the new Invitee insert and report the error

    //> Read the Session table and get the row for the invitee's session
    sessionTable.where({ id: item.sid }).read({
        success: function (rows) {

            if (rows.length !== 1) {
                request.respond(statusCodes.BAD_REQUEST, "ERR_SESSION_NOT_FOUND");
                return;
            }

            var sessionRow = rows[0];  // Success 

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

            // How many invitees do we have? In version 1.0 we allow up to a maximum of 10
            inviteeTable.where({ sid: item.sid }).read({
                success: function (inviteeRows) {
                    
                    if (inviteeRows.length === maxInviteeCount) {
                        request.respond(statusCodes.BAD_REQUEST, "ERR_TOO_MANY_INVITEES");
                        return;
                    }
                },
                error: function(err) {

                    console.error(err + " Invitee insert failed: Can't read Invitee table (100)");
                    request.respond(statusCodes.BAD_REQUEST, "ERR_READ_FAILED");
                    return;
                }
            });
            // End of invitee read op

            // Update the Session row:
            sessionRow.SessionStatusId = 2;  // SessionActive
            sessionRow.RequestMessageId = 3;  // InviteeJoin

            // Note that crud operations done via tables.getTable() do NOT trigger 
            // the crud scripts in the destination table (e.g. the following update
            // will not cause the Session table update script to run)

            // -------> Commit the changes to the status of the Session row           
            sessionTable.update(sessionRow, {
                success: function () {

                    // ---------------> Commit the insert of the Invitee
                    request.execute({
                        success: function () {

                            // Send MPNS "InviteeHasAccepted" notifications to the *inviter*
                            
                            // Note that we do not send a notification to the *invitee*, as all
                            // processing is handled directly at the point the call to
                            // InsertAsync(invitee) is made in MainViewModel.DoCompleteAcceptInvite().
                            // However, we do still add the notification to the RoundUpNotification
                            // table for consistency
                            
                            var notificationInviter =
                            {
                                Recipient: 0,
                                SessionId: item.sid,
                                InviteeId: item.id,
                                MessageId: "InviteeHasAccepted",
                                Data: item.Name,
                                ShortDeviceId: item.InviterShortDeviceId,
                                Latitude: item.Latitude,
                                Longitude: item.Longitude
                            };

                            // Insert the notification for the *inviter* into the RoundUpNotification table
                            tables.getTable("RoundUpNotification").insert(notificationInviter, {
                                error: function (err) {

                                    console.error(err + " Unable to insert into RoundUpNotification table (101)");
                                }
                            });
                            
                            var notificationInvitee =
                            {
                                Recipient: 1,
                                SessionId: item.sid,
                                InviteeId: item.id,
                                MessageId: "InviteeHasAccepted",
                                Data: sessionRow.Name,
                                ShortDeviceId: item.InviterShortDeviceId,
                                Latitude: sessionRow.Latitude,
                                Longitude: sessionRow.Longitude
                            };
                            
                            // Insert the notification for the *invitee* into the RoundUpNotification table
                            tables.getTable("RoundUpNotification").insert(notificationInvitee, {
                                error: function (err) {
                                    
                                    console.error(err + " RoundUpNotification insert failed (102)");
                                }
                            });

                            var payloadMsgInviter =
                            "{" +
                            "  id:" + -1 + "," +
                            "  Recipient:" + 0 + "," +
                            "  SessionId:" + item.sid.toString() + "," +
                            "  InviteeId:" + item.id.toString() + "," +
                            "  MessageId:" + "'InviteeHasAccepted'" + "," +
                            "  Data:" + "'" + item.Name + "'" + "," +
                            "  ShortDeviceId:" + "'" + item.InviterShortDeviceId + "'" + "," +
                            "  Latitude:" + item.Latitude.toString() + "," +
                            "  Longitude:" + item.Longitude.toString() +
                            "}";

                            // Add inviter data needed by the invitee into the Invitee item
                            item.Latitude = sessionRow.Latitude;    // Inviter's latitude
                            item.Longitude = sessionRow.Longitude;  // Inviter's latitude
                            item.RequestData = sessionRow.Name;     // Inviter's name
                            
                            // Notify the inviter
                            push.mpns.sendRaw(sessionRow.Channel, { payload: payloadMsgInviter }, {
                                success: function () {

                                    console.log("MPNS: InviteeHasAccepted sent to inviter. InviteeId = " + item.id + ", SessionId = " + item.sid);
                                    request.respond();  // Success (pass back info on the inviter's location, etc. in the Invitee object)
                                },
                                error: function (err) {

                                    // Non-fatal error
                                    if (isRealMpnsError(err)) {

                                        logMpnsResult(err, "InviteeHasAccepted failed (103)");

                                        if (notificationLimitExceeded(err)) {

                                            request.respond(statusCodes.BAD_REQUEST, "ERR_NOTIFICATION_LIMIT_EXCEEDED");
                                            return;
                                        }
                                    }

                                    // Report success, even though the notification failed, which can happen if the device is off-line.
                                    // The notification will have been inserted into the RoundUpNotification table and will "replayed"
                                    // by the client app once the device is active
                                    request.respond();  // Pass back info on the inviter's location, etc. in the Invitee object
                                }
                            });
                        },
                        // -------------------> error: Commit the insert of the Invitee 
                        error: function (err) {

                            console.error(err + " Invitee row update failed (104)");
                            request.respond(statusCodes.BAD_REQUEST, "ERR_INSERT_FAILED");
                            return;
                        }
                    });
                },
                // -----------> error: Commit the changes to the Session row 
                error: function (err) {

                    console.error(err + " Invitee insert failed: can't commit changes to Session (105)");
                    request.respond(statusCodes.BAD_REQUEST, "ERR_INSERT_FAILED");
                    return;
                }
            });
        },
        // ---> error: Read the Session table
        error: function (err) {

            console.error(err + " Invitee insert failed: can't read Session (106)");
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

