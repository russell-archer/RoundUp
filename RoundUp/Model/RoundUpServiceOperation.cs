using RoundUp.Enum;

namespace RoundUp.Model
{
    /// <summary>This class encapsulates information on a RoundUp Windows Azure Mobile Service operation</summary>
    public class RoundUpServiceOperation
    {
        /// <summary>The overall result of the operation. If a success, this property will be OperationSuccess</summary>
        public RoundUpServiceOperationResult Result { get; set; }

        /// <summary>The SessionId returned from the operation (if any). If -1, the value should be ignored</summary>
        public int SessionId { get; set; }

        /// <summary>The InviteeId returned from the operation (if any). If -1, the value should be ignored</summary>
        public int InviteeId { get; set; }

        /// <summary>The Inviter's latitude</summary>
        public double Latitude { get; set; }

        /// <summary>The Inviter's longitude</summary>
        public double Longitude { get; set; }

        /// <summary>The Inviter's name</summary>
        public string Data { get; set; }
    }
}