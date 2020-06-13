namespace RoundUp.Model
{
    /// <summary>
    /// Defines the content of each message sent from the RoundUp Azure service via the MPNS. 
    /// </summary>
    public interface IRoundUpNotification
    {
        /// <summary>Row id</summary>
        int id { get; set; }

        /// <summary>The intended recipient of the message (0 == inviter; 1 == invitee; -1 == unknown)</summary>
        int Recipient { get; set; }

        /// <summary>Session Id</summary>
        int SessionId { get; set; }  

        /// <summary>Invitee Id</summary>
        int InviteeId { get; set; } 
        
        /// <summary>See RoundUpNotificationMessage enum</summary>
        string MessageId { get; set; } 
        
        /// <summary>Notification-specific data </summary>
        string Data { get; set; }   
        
        /// <summary>The inviter's short device id</summary>
        string ShortDeviceId { get; set; } 
        
        /// <summary>The latitude related to this notification</summary>
        double Latitude { get; set; } 
        
        /// <summary>The longitude related to this notification</summary>
        double Longitude { get; set; }
    }
}