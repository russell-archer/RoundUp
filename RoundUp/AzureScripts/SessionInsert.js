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
                
                success: function() {

                    request.respond();  // Success
                },
                error: function (err) {
                    
                    console.error(err + " Unable to insert into RoundUpNotification table (100)");
                    request.respond();  // Success, even though the notification table updated failed
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
