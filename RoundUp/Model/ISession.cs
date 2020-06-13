using System;

namespace RoundUp.Model
{
    /// <summary>Defines the interface for Session objects. The structure is mirrored in the "Session" table in the RoundUp Azure SQL database</summary>
    public interface ISession
    {
        /// <summary>Session (row) id</summary>
        int id { get; set; }  

        /// <summary>When the session was *started*. Will auto-expire after 24hrs</summary>
        DateTime Timestamp { get; set; }  
        
        /// <summary>Name/alias of the person that started the session</summary>
        string Name { get; set; }   
        
        /// <summary>Channel URI used by the inviter's device to communicate with the MPNS </summary>
        string Channel { get; set; }  
        
        /// <summary>Latitude of the RoundUp point (the inviter's device)</summary>
        double Latitude { get; set; }  
        
        /// <summary>Longitude of the RoundUp point (the inviter's device)</summary>
        double Longitude { get; set; }  
        
        /// <summary>Address (if any) of the RoundUp point</summary>
        string Address { get; set; }   
        
        /// <summary>The 8-character device id for the device that initiated the session</summary>
        string ShortDeviceId { get; set; }  
        
        /// <summary>Code for the type of device. See SessionDeviceType</summary>
        int Device { get; set; } 
        
        /// <summary>The message being sent to the session. See RoundUpRequestMessage enum</summary>
        int RequestMessageId { get; set; }  
        
        /// <summary>The current status of the session. See SessionStatus enum</summary>
        int SessionStatusId { get; set; } 
        
        /// <summary>Additional request-specific numberic data related to the request</summary>
        int RequestDataId { get; set; }  
        
        /// <summary>Additional request-specific string data related to the request</summary>
        string RequestData { get; set; }  
    }
}