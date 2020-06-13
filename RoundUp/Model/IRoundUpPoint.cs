using System.Device.Location;

namespace RoundUp.Model
{
    /// <summary>Encapsulates data related to the roundup (meet-up) location</summary>
    public interface IRoundUpPoint
    {
        /// <summary>The GeoCoordinate for the roundup point</summary>
        GeoCoordinate Location { get; set; }   
        
        /// <summary>The "Meet here" text</summary>
        string Text { get; }    
        
        /// <summary>The address of the RoundUp point (if any)</summary>
        string Address { get; set; }  
        
        /// <summary>The distance (in meters) between the device's current location and the RoundUp location (will be zero if the inviter is using their current location as the roundup point)</summary>
        double DistanceToRoundUpPoint { get; set; }   
        
        /// <summary>A string containing the "Meet here" text, and the distance to the RoundUp location </summary>
        string CombinedTextAndDistance { get; }         
        
        /// <summary>A string containing the "Meet here" text, the address of the roundup point, and the distance to the RoundUp location</summary>
        string CombinedTextAddressAndDistance { get; }

        /// <summary>A string containing the distance to the roundup point, including either the "km" or "meters" prefix</summary>
        string DistanceText { get; }
    }
}