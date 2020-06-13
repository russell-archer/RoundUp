using System.Device.Location;

namespace RoundUp.Model
{
    public interface IInviteeLocationMarker
    {
        /// <summary>The invitee id (same as the (Azure Invitee row) id in IInvitee)</summary>
        int id { get; set; } 
        
        /// <summary>The invitee's name/alias</summary>
        string Name { get; set; }  
        
        /// <summary>Text of a message to send or receive</summary>
        string InstantMessage { get; set; } 
        
        /// <summary>The invitee's GeoCoordinate</summary>
        GeoCoordinate Location { get; set; }  
        
        /// <summary>The distance (in meters) between the invitee's current location and the RoundUp location</summary>
        double DistanceToRoundUpPoint { get; set; } 

        /// <summary>A string containing the invitees name and distance to the RoundUp location</summary>
        string CombinedNameAndDistance { get; } 
    }
}