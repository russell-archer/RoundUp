using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using RoundUp.Enum;

namespace RoundUp.Model
{
    /// <summary>Defines the interface for the RoundUpService object</summary>
    public interface IRoundUpService
    {
        // Events -------------------------------------------------------------

        /// <summary>The PropertyChanged event</summary>
        event PropertyChangedEventHandler PropertyChanged;

        // Properties ---------------------------------------------------------

        /// <summary>True if we're connected to Azure through the use of our specific URI and secret key, false otherwise</summary>
        bool Connected { get; set; }

        // Methods ------------------------------------------------------------

        /// <summary>Save state to persistent (isolated) storage</summary>
        void SaveState();

        /// <summary>Restore state from to persistent (isolated) storage</summary>
        void RestoreState();

        /// <summary>Dump our internal state to the console</summary>
        void Dump();

        /// <summary>Attempts to connect to Azure using our uri and secret key</summary>
        /// <returns>Returns true if we can connect to Azure, false otherwise</returns>
        bool Connect();

        /// <summary>
        /// Start a new RoundUp session. The device initiating the session becomes the "inviter". The device must previously 
        /// have registered with the MPNS and received back a unique channel URI (see MicrosoftPushNotificationService.Connect)
        /// </summary>
        /// <param name="channel">Unique channel URI assigned by the MPNS. If null an ArgumentNullException is thrown</param>
        /// <param name="latitude">The latitude of the RoundUp location</param>
        /// <param name="longtitude">The longitude of the RoundUp location</param>
        /// <param name="shortDeviceId">The short device id (see DeviceHelper.GetShortDigitDeviceId(). If null an ArgumentNullException is thrown</param>
        /// <param name="name">The name (alias, if any) of the persion initiating the session. Can be null or empty</param>
        /// <param name="address">The address (if any) of the RoundUp location. Can be null or empty</param>
        /// <returns>
        /// Returns RoundUpServiceOperation.Result.OperationSuccess if the operation succeeded. The session id for the new session
        /// is returned in RoundUpServiceOperation.SessionId
        /// </returns>
        Task<RoundUpServiceOperation> RegisterAsInviterAsync(string channel, double latitude, double longtitude, string shortDeviceId, string name, string address);

        /// <summary>
        /// Attempts to join the device to an existing session. A new row is inserted into the Invitee Azure SQL table.
        /// When the insert request is received by Azure, the InviteeInsert script runs (see the AzureScripts/InviteeInsert.js).
        /// Be aware the the request can fail for a number of reasons - see AzureScripts/InviteeInsert.js. The script
        /// will also request the MPNS to send notifications to both the inviter and this device (the invitee)
        /// </summary>
        /// <param name="sessionId">The SessionId of the session to be joined to. This is the first part of the "invite code" sent to the invitee</param>
        /// <param name="channel">Our unique MPNS Channel Uri</param>
        /// <param name="latitude">This device's latitude</param>
        /// <param name="longtitude">This device's longitude</param>
        /// <param name="inviterShortDeviceId">The inviter's short device id. This is the second part of the "invite code" sent to the invitee(</param>
        /// <param name="name">The user's name/alias</param>
        /// <param name="address">The address (if any) at the device's current location</param>
        /// <returns>
        /// Returns RoundUpServiceOperation.Result.OperationSuccess if the operation succeeded. 
        /// The session id for the session is returned in RoundUpServiceOperation.SessionId.
        /// The invitee id for the session is returned in RoundUpServiceOperation.InviteeId
        /// </returns>
        Task<RoundUpServiceOperation> RegisterAsInviteeAsync(int sessionId, string channel, double latitude, double longtitude, string inviterShortDeviceId, string name, string address);

        /// <summary>
        /// Changes the location of the RoundUp point. The Session table is updated and notifications sent to 
        /// all invitees. Uses the SessionUpdate.js script in Azure Mobile Services
        /// </summary>
        /// <param name="sessionId">The session (row) id to be updated</param>
        /// <param name="channel">Inviter's channel uri</param>
        /// <param name="inviterShortDeviceId">The inviter's short device id</param>
        /// <param name="latitude">New latitude of RoundUp point</param>
        /// <param name="longtitude">New longitude of RoundUp point</param>
        /// <param name="name">Inviter's name/alias</param>
        /// <param name="address">Inviter's address</param>
        /// <returns>
        /// Returns RoundUpServiceOperationResult.OperationSuccess if the operation succeeded. If the operation fails,
        /// either MobileServiceInvalidOperation or OperationFailureCanRetry is returned. In the latter case, a second attempt
        /// at the operation may succeed. In the former, additional attempts to try the operation will very likely fail (probably
        /// because the request was badly formatted)
        /// </returns>
        Task<RoundUpServiceOperationResult> UpdateRoundUpPointLocationAsync(int sessionId, string channel, string inviterShortDeviceId, double latitude, double longtitude, string name, string address);

        /// <summary>
        /// Updates the Invitee table for the specified sesison id and invitee id. This creates a 
        /// RoundUpRequestMessage.InviteeLocationUpdate request. The Azure Invitee tables' UpdateInvitee.js 
        /// script will update the row and then send a RoundUpNotificationMessage.InviteeLocationUpdate
        /// to the inviter (see MainViewModel.OnMpnsPushNotification), allowing an updated position for 
        /// the invitee to be shown on the inviter's map
        /// </summary>
        /// <param name="id">The InviteeId</param>
        /// <param name="sessionId">The SessionId of the session to be updated</param>
        /// <param name="channel">The invitee's MPNS channel uri</param>
        /// <param name="latitude">The Invitee's latitude</param>
        /// <param name="longtitude">The Invitee's longitude</param>
        /// <param name="inviterShortDeviceId">The inviter's short device id</param>
        /// <param name="name">The invitee's name/alias</param>
        /// <param name="address">The invitee's address</param>
        /// <returns>
        /// Returns RoundUpServiceOperationResult.OperationSuccess if the operation succeeded. If the operation fails,
        /// either MobileServiceInvalidOperation or OperationFailureCanRetry is returned. In the latter case, a second attempt
        /// at the operation may succeed. In the former, additional attempts to try the operation will very likely fail (probably
        /// because the request was badly formatted)
        /// </returns>
        Task<RoundUpServiceOperationResult> UpdateInviteeLocationAsync(int id, int sessionId, string channel, double latitude, double longtitude, string inviterShortDeviceId, string name, string address);

        /// <summary>
        /// Updates the Invitee table for the specified sesison id and invitee id. This creates a 
        /// RoundUpRequestMessage.InviteeHasArrived request. The Azure Invitee tables' UpdateInvitee.js 
        /// script will update the row and then send a RoundUpNotificationMessage.InviteeHasArrived
        /// to the inviter (see MainViewModel.OnMpnsPushNotification)
        /// </summary>
        /// <param name="id">The InviteeId</param>
        /// <param name="sessionId">The SessionId of the session to be updated</param>
        /// <param name="channel">The invitee's MPNS channel uri</param>
        /// <param name="latitude">The Invitee's latitude</param>
        /// <param name="longtitude">The Invitee's longitude</param>
        /// <param name="inviterShortDeviceId">The inviter's short device id</param>
        /// <param name="name">The invitee's name/alias</param>
        /// <param name="address">The invitee's address</param>
        /// <returns>
        /// Returns RoundUpServiceOperationResult.OperationSuccess if the operation succeeded. If the operation fails,
        /// either MobileServiceInvalidOperation or OperationFailureCanRetry is returned. In the latter case, a second attempt
        /// at the operation may succeed. In the former, additional attempts to try the operation will very likely fail (probably
        /// because the request was badly formatted)
        /// </returns>
        Task<RoundUpServiceOperationResult> InviteeHasArrivedAsync(int id, int sessionId, string channel, double latitude, double longtitude, string inviterShortDeviceId, string name, string address);

        /// <summary>Close a RoundUp session. This is normally done when all invitees have arrived</summary>
        /// <param name="id">Session (row) id</param>
        /// <param name="channel">Unique channel URI assigned by the MPNS. If null an ArgumentNullException is thrown</param>
        /// <param name="latitude">The latitude of the RoundUp location</param>
        /// <param name="longtitude">The longitude of the RoundUp location</param>
        /// <param name="shortDeviceId">The short device id (see DeviceHelper.GetShortDigitDeviceId(). If null an ArgumentNullException is thrown</param>
        /// <param name="name">The name (alias, if any) of the persion initiating the session. Can be null or empty</param>
        /// <param name="address">The address (if any) of the RoundUp location. Can be null or empty</param>
        /// <returns>
        /// Returns RoundUpServiceOperationResult.OperationSuccess if the operation succeeded. If the operation fails,
        /// either MobileServiceInvalidOperation or OperationFailureCanRetry is returned. In the latter case, a second attempt
        /// at the operation may succeed. In the former, additional attempts to try the operation will very likely fail (probably
        /// because the request was badly formatted)
        /// </returns>
        Task<RoundUpServiceOperationResult> CloseSessionAsync(int id, string channel, double latitude, double longtitude, string shortDeviceId, string name, string address);

        /// <summary>
        /// Gets a list of all the RoundUpNotifications stored in the SQL database by the RoundUp Azure service. 
        /// Whenever the RoundUp Azure service requests the MPNS to send a notification a copy of that notification
        /// is saved. This allows us to compare the list of notifications sent by the MPNS and the notifications
        /// received by our MicrosoftPushNotificationService. This is normally done when the app is activated and
        /// allows us to see if we've missed any MPNS notifications while deactivated. For each missed notification,
        /// we can call MicrosoftPushNotificationService.ResendMissedNotification() to resend the notification
        /// </summary>
        /// <param name="sessionId">A valid SessionId, which will be used as the primary lookup</param>
        /// <param name="inviteeId">
        /// Should either be valid InviteeId or -1. 
        /// If an InviteeId, all notifications for that invitee in the specified session will be returned.
        /// If -1, only notifications intended for the inviter are returned
        /// </param>
        /// <param name="recipientIsAnInviter">True if you want notifications intended for an inviter, false for an invitee</param>
        /// <returns>Returns a list of all the RoundUpNotifications sent by mpns as requested by the RoundUp Azure service</returns>        
        Task<IEnumerable<RoundUpNotification>> GetStoredNotificationsAsync(int sessionId, int inviteeId, bool recipientIsAnInviter);

        /// <summary>
        /// Checks to see if the indicated session is still alive (i.e. it's not been closed, cancelled, timeout, etc.).
        /// The Azure Session table is checked to see if the SessionStatusId column contains one of SessionStarted,
        /// or SessionActive. If it does, the session is alive, otherwise it's dead
        /// </summary>
        /// <param name="sessionId">A valid SessionId, which will be used as the primary lookup</param>
        /// <returns>Returns true if the session is still alive, false otherwise</returns>        
        Task<bool> IsSessionAliveAsync(int sessionId);

        /// <summary>
        /// Updates the values of the channel uri column in the Azure Session table for the specified SessionID
        /// Uses the SessionUpdate.js script in Azure Mobile Services
        /// </summary>
        /// <param name="sessionId">The session (row) id to be updated</param>
        /// <param name="channel">The new Inviter channel uri</param>
        /// <param name="inviterShortDeviceId">The inviter's short device id</param>
        /// <param name="latitude">Latitude of RoundUp point</param>
        /// <param name="longtitude">Longitude of RoundUp point</param>
        /// <param name="name">Inviter's name/alias</param>
        /// <param name="address">Inviter's address</param>
        /// <returns>
        /// Returns RoundUpServiceOperationResult.OperationSuccess if the operation succeeded. If the operation fails,
        /// either MobileServiceInvalidOperation or OperationFailureCanRetry is returned. In the latter case, a second attempt
        /// at the operation may succeed. In the former, additional attempts to try the operation will very likely fail (probably
        /// because the request was badly formatted)
        /// </returns>
        Task<RoundUpServiceOperationResult> UpdateInviterChannelUriAsync(int sessionId, string channel, string inviterShortDeviceId, double latitude, double longtitude, string name, string address);

        /// <summary>
        /// Updates the values of the channel uri column in the Azure Invitee table for the specified Session ID and Invitee ID
        /// Uses the InviteeUpdate.js script in Azure Mobile Services
        /// </summary>
        /// <param name="id">The InviteeId</param>
        /// <param name="sessionId">The SessionId of the session to be updated</param>
        /// <param name="channel">The invitee's new MPNS channel uri</param>
        /// <param name="latitude">The Invitee's latitude</param>
        /// <param name="longtitude">The Invitee's longitude</param>
        /// <param name="inviterShortDeviceId">The inviter's short device id</param>
        /// <param name="name">The invitee's name/alias</param>
        /// <param name="address">The invitee's address</param>
        /// <returns>
        /// Returns RoundUpServiceOperationResult.OperationSuccess if the operation succeeded. If the operation fails,
        /// either MobileServiceInvalidOperation or OperationFailureCanRetry is returned. In the latter case, a second attempt
        /// at the operation may succeed. In the former, additional attempts to try the operation will very likely fail (probably
        /// because the request was badly formatted)
        /// </returns>
        Task<RoundUpServiceOperationResult> UpdateInviteeChannelUriAsync(int id, int sessionId, string channel, double latitude, double longtitude, string inviterShortDeviceId, string name, string address);

        /// <summary>
        /// Cancels the specified session. A notification (SessionCancelledByInviter) is sent to all invitees
        /// </summary>
        /// <param name="sessionId">The SessionId</param>
        /// <param name="inviterShortDeviceId">The inviter's short device id</param>
        /// <param name="name">Inviter's name</param>
        /// <param name="channel">The MPNS channel uri</param>
        /// <returns>
        /// Returns RoundUpServiceOperationResult.OperationSuccess if the operation succeeded. If the operation fails,
        /// either MobileServiceInvalidOperation or OperationFailureCanRetry is returned. In the latter case, a second attempt
        /// at the operation may succeed. In the former, additional attempts to try the operation will very likely fail (probably
        /// because the request was badly formatted)
        /// </returns>
        Task<RoundUpServiceOperationResult> CancelSessionAsync(int sessionId, string inviterShortDeviceId, string name, string channel);

        /// <summary>
        /// Cancels the invitee's participation in the session. A notification (InviteeHasCancelled) is sent to the inviter
        /// </summary>
        /// <param name="id">The InviteeId</param>
        /// <param name="sessionId">The SessionId</param>
        /// <param name="inviterShortDeviceId">The inviter's short device id</param>
        /// <param name="name">Invitee's name</param>
        /// <returns>
        /// Returns RoundUpServiceOperationResult.OperationSuccess if the operation succeeded. If the operation fails,
        /// either MobileServiceInvalidOperation or OperationFailureCanRetry is returned. In the latter case, a second attempt
        /// at the operation may succeed. In the former, additional attempts to try the operation will very likely fail (probably
        /// because the request was badly formatted)
        /// </returns>
        Task<RoundUpServiceOperationResult> CancelInviteeSessionAsync(int id, int sessionId, string inviterShortDeviceId, string name);
    }
}
