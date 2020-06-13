function RemoveExpiredSessionData() {

    var sessionTable = tables.getTable("Session");
    var inviteeTable = tables.getTable("Invitee");
    var notificationTable = tables.getTable("RoundUpNotification");
    var deadSessionTimestamp = new Date();  // This will be in UTC (universal time, same as out timestamp values)
    
    deadSessionTimestamp.setDate(deadSessionTimestamp.getDate() - 1);  // Subtract 1 day

    // Get all the Session table rows where the row timestamp is <= deadSessionTimestamp and not previously processed 
    // Note that in the .where filter we define a query object as a function (to filter the session rows)
    // * The function must return a boolean value (if true, the row is added the result set)
    // * You have to pass variables to the where clause that you wish to use in the function (can't access vars in outer scope)
    // * The row being filtered is represented by the this object in the function
    sessionTable.where(function (deadTs) { return this.Timestamp <= deadTs && this.SessionStatusId !== 7; }, deadSessionTimestamp).read({
        success: function(rows) {

            console.log("Scheduled task: " + rows.length + " expired sessions found where Timestamp <= " + deadSessionTimestamp);
            if(rows.length === 0) return;

            // Update each row so that SessionStatusId = 7 ("SessionDead") and other fields are set to defaults
            rows.forEach(function (row) {
                
                row.SessionStatusId = 7;
                row.Name = "";
                row.Channel = "";
                row.Address = "";
                row.ShortDeviceId = "";
                row.Device = 0;
                row.RequestDataId = 0;
                row.RequestData = "";
                
                sessionTable.update(row, {
                    
                    success: function () {
                        
                        // Remove all Invitee table rows with a session id (sid) == the SessionDead row
                        mssql.query("SELECT * FROM Invitee WHERE sid=" + row.id, {
                            success: function(r) {

                                if (r.length !== 0) for (var i = 0; i < r.length; i++) inviteeTable.del(r[i].id);
                            }
                        });
                        
                        // Remove all RoundUpNotification rows with a SessionId == the SessionDead row  
                        mssql.query("SELECT * FROM RoundUpNotification WHERE SessionId=" + row.id, {
                            success: function(n) {

                                if (n.length !== 0) for (var i = 0; i < n.length; i++) notificationTable.del(n[i].id);
                            }
                        });
                    },
                    error: function() {
                        
                        console.log("Scheduled task: Unable to update session row, SessionId = " + row.id);
                    }
                });
            });
        }
    });
}