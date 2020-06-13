using System;

namespace RoundUp.Model
{
    /// <summary>Defines the interface for Invitee objects. The structure is mirrored in the "Invitee" table in the RoundUp Azure SQL database</summary>
    public interface IInvitee
    {
        /// <summary>The invitee (row) id</summary>
        int id { get; set; }  
        
        /// <summary>The session (row) id</summary>
        int sid { get; set; } 
        
        /// <summary>When the invitee accepted the invitee to join the RoundUp</summary>
        DateTime Timestamp { get; set; } 
        
        /// <summary>The invitee's name/alias</summary>
        string Name { get; set; } 
        
        /// <summary>The invitee's MPNS channel URI</summary>
        string Channel { get; set; } 
        
        /// <summary>The latitude of the invitee's current location</summary>
        double Latitude { get; set; } 
        
        /// <summary>The longitude of the invitee's current location</summary>
        double Longitude { get; set; }
        
        /// <summary>The address (if any) for the invitee's current location</summary>
        string Address { get; set; }
        
        /// <summary>Code for the type of device. See SessionDeviceType</summary>
        int Device { get; set; } 
        
        /// <summary>The message being sent to the session. See RoundUpRequestMessage enum</summary>
        int RequestMessageId { get; set; }
        
        /// <summary>The current status of the invitee. See InviteeStatus enum</summary>
        int InviteeStatusId { get; set; } 
        
        /// <summary>The 8-char short device id for the inviter's device (part of the invite code) </summary>
        string InviterShortDeviceId { get; set; } 
        
        /// <summary>Additional request-specific numberic data related to the request</summary>
        int RequestDataId { get; set; } 
        
        /// <summary>Additional request-specific string data related to the request (e.g. used for instant messages)</summary>
        string RequestData { get; set; }
    }
}