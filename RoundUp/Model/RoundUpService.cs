using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using RArcher.Phone.Toolkit;
using RArcher.Phone.Toolkit.Common;
using Microsoft.WindowsAzure.MobileServices;
using RArcher.Phone.Toolkit.Logging;
using RoundUp.Enum;

namespace RoundUp.Model
{
    /// <summary>Encapsulates use of Windows Azure Mobiles Services</summary>
    public class RoundUpService : ModelBase, IRoundUpService
    {
        // Properties ---------------------------------------------------------

        /// <summary>True if we're connected to Azure through the use of our specific URI and secret key, false otherwise</summary>
        [Dump]
        [AutoState]
        public bool Connected
        {
            get { return _connected; }
            set
            {
                if(value == _connected) return;  // No change

                Logger.Log(value ? "RoundUpService Connected" : "RoundUpService Disconnected");

                _connected = value;
                OnPropertyChanged();
            }
        }

        // Private members ----------------------------------------------------

        private MobileServiceClient _mobileService;
        private bool _connected;

        // Methods ------------------------------------------------------------

        /// <summary>Constructor</summary>
        public RoundUpService()
        {
            Connected = false;  // No auto-connect
        }

        /// <summary>Attempts to connect to Azure using our uri and secret key</summary>
        /// <returns>Returns true if we can connect to Azure, false otherwise</returns>
        public bool Connect()
        {
            try
            {
                // Create the object that is a proxy for our Azure Mobile Service
                if (_mobileService != null && Connected) return true;

                var uri = Strings.GetStringResource("_RoundUpAzureMobileServiceUri", "https://roundup.azure-mobile.net/");
                var key = Strings.GetStringResource("_RoundUpAzureMobileServiceKey", "rnKizRbmtqcMZjwCTCsoIfVgVEJWON18");

                _mobileService = new MobileServiceClient(uri, key);
                
                Connected = true;
                return true;
            }
            catch(MobileServiceInvalidOperationException ex)
            {
                // Error likely caused by an internal data formatting error - retrying probably won't succeed
                Logger.Log(string.Format("RoundUpService: Error connecting. HTTP Status Code: {0}. {1}", ex.Response.StatusCode, ex.Message), new StackFrame(0, true));
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }

            Connected = false;
            return false;
        }

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
        /// If -1, only notifications intended for the inviter are returned (InviteeId is ignored - messages related to all invitees are retrieved)
        /// </param>
        /// <param name="recipientIsAnInviter">True if you want notifications intended for an inviter, false for an invitee</param>
        /// <returns>Returns a list of all the RoundUpNotifications sent by mpns as requested by the RoundUp Azure service</returns>
        public async Task<IEnumerable<RoundUpNotification>> GetStoredNotificationsAsync(int sessionId, int inviteeId, bool recipientIsAnInviter)
        {
            try
            {
                if(recipientIsAnInviter)
                {
                    var query = _mobileService.GetTable<RoundUpNotification>()
                        .CreateQuery()
                        .Where(row => row.SessionId == sessionId && row.Recipient == 0);

                    return await query.ToEnumerableAsync();                    
                }
                else
                {
                    var query = _mobileService.GetTable<RoundUpNotification>()
                        .CreateQuery()
                        .Where(row => row.SessionId == sessionId && row.InviteeId == inviteeId && row.Recipient == 1);

                    return await query.ToEnumerableAsync();
                }
            }
            catch (MobileServiceInvalidOperationException ex)
            {
                // Error likely caused by an internal data formatting error - retrying probably won't succeed
                Logger.Log(string.Format("RoundUpService HTTP Status Code: {0}. {1}", ex.Response.StatusCode, ex.Message), new StackFrame(0, true));
                return null;
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return null;
            }
        }

        /// <summary>
        /// Checks to see if the indicated session is still alive (i.e. it's not been closed, cancelled, timeout, etc.).
        /// The Azure Session table is checked to see if the SessionStatusId column contains one of SessionStarted (1),
        /// or SessionActive (2). If it does, the session is alive, otherwise it's dead
        /// </summary>
        /// <param name="sessionId">A valid SessionId, which will be used as the primary lookup</param>
        /// <returns>Returns true if the session is still alive, false otherwise</returns>        
        public async Task<bool> IsSessionAliveAsync(int sessionId)
        {
            if(sessionId == -1) return false;

            try
            {
                var query = _mobileService.GetTable<Session>()
                    .CreateQuery()
                    .Where(row => row.id == sessionId);

                var result = await query.ToListAsync();
                if(result == null || !result.Any()) return false;

                var status = result[0].SessionStatusId;
                return (status == 1 || status == 2);
            }
            catch (MobileServiceInvalidOperationException ex)
            {
                // Error likely caused by an internal data formatting error - retrying probably won't succeed
                Logger.Log(string.Format("RoundUpService HTTP Status Code: {0}. {1}", ex.Response.StatusCode, ex.Message), new StackFrame(0, true));
                return false;
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return false;
            }
        }

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
        public async Task<RoundUpServiceOperationResult> UpdateInviterChannelUriAsync(
            int sessionId,
            string channel,
            string inviterShortDeviceId,
            double latitude,
            double longtitude,
            string name,
            string address)
        {
            if(!Connected) return RoundUpServiceOperationResult.OperationFailureCanRetry;

            if(sessionId == -1) throw new ArgumentOutOfRangeException(sessionId.ToString(CultureInfo.InvariantCulture));
            if(string.IsNullOrEmpty(channel)) throw new ArgumentNullException(channel);
            if(string.IsNullOrEmpty(inviterShortDeviceId)) throw new ArgumentNullException(inviterShortDeviceId);

            // Important: You can't pass null strings to Azure (you'll get a BAD_REQUEST response if you do)
            if(string.IsNullOrEmpty(name)) name = string.Empty;
            if(string.IsNullOrEmpty(address)) address = string.Empty;

            var session = new Session
            {
                id = sessionId,
                Timestamp = DateTime.Now.ToUniversalTime(),
                Name = name,
                Channel = channel,
                Latitude = latitude,
                Longitude = longtitude,
                Address = address,
                Device = (int)SessionDeviceType.WindowsPhone8,
                ShortDeviceId = inviterShortDeviceId,
                RequestMessageId = (int)RoundUpRequestMessage.UpdateInviterChannelUri,
                SessionStatusId = (int)SessionStatusValue.SessionActive,  // We only ever need to change the channel uri for an active session
                RequestDataId = -1,
                RequestData = string.Empty // You can't pass null strings to Azure
            };

            try
            {
                // Insert a new session row
                await _mobileService.GetTable<Session>().UpdateAsync(session);

                return RoundUpServiceOperationResult.OperationSuccess;
            }
            catch(MobileServiceInvalidOperationException ex)
            {
                // Error likely caused by an internal data formatting error - retrying probably won't succeed
                Logger.Log(string.Format("RoundUpService HTTP Status Code: {0}. {1}", ex.Response.StatusCode, ex.Message), new StackFrame(0, true));
                return MapAzureResponseToServiceOpResult(ex.Message);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return RoundUpServiceOperationResult.OperationFailureCanRetry; // Session row update failed - probably a network error
            }
        }

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
        public async Task<RoundUpServiceOperationResult> UpdateInviteeChannelUriAsync(
            int id,
            int sessionId,
            string channel,
            double latitude,
            double longtitude,
            string inviterShortDeviceId,
            string name,
            string address)
        {
            if(!Connected) return RoundUpServiceOperationResult.OperationFailureCanRetry;

            if(sessionId == -1) throw new ArgumentOutOfRangeException(sessionId.ToString(CultureInfo.InvariantCulture));
            if(id == -1) throw new ArgumentOutOfRangeException(id.ToString(CultureInfo.InvariantCulture));
            if(string.IsNullOrEmpty(channel)) throw new ArgumentNullException(channel);
            if(string.IsNullOrEmpty(inviterShortDeviceId)) throw new ArgumentNullException(inviterShortDeviceId);

            // Important: You can't pass null strings to Azure (you'll get a BAD_REQUEST response if you do)
            if(string.IsNullOrEmpty(name)) name = string.Empty;
            if(string.IsNullOrEmpty(address)) address = string.Empty;

            var invitee = new Invitee
            {
                id = id,
                sid = sessionId,
                Channel = channel,
                Latitude = latitude,
                Longitude = longtitude,
                Device = (int)SessionDeviceType.WindowsPhone8,
                Name = name,
                Address = address,
                Timestamp = DateTime.Now.ToUniversalTime(),
                RequestMessageId = (int)RoundUpRequestMessage.UpdateInviteeChannelUri,
                InviteeStatusId = (int)InviteeStatusValue.InviteeIsEnRoute, 
                InviterShortDeviceId = inviterShortDeviceId,
                RequestDataId = -1,
                RequestData = string.Empty // You can't pass null strings to Azure
            };

            try
            {
                await _mobileService.GetTable<Invitee>().UpdateAsync(invitee);
                return RoundUpServiceOperationResult.OperationSuccess;
            }
            catch(MobileServiceInvalidOperationException ex)
            {
                // Error likely caused by an internal data formatting error - retrying probably won't succeed
                Logger.Log(string.Format("RoundUpService HTTP Status Code: {0}. {1}", ex.Response.StatusCode, ex.Message));
                return MapAzureResponseToServiceOpResult(ex.Message);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return RoundUpServiceOperationResult.OperationFailureCanRetry; // The lookup or Invitee row update failed - probably a network error
            }
        }

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
        public async Task<RoundUpServiceOperation> RegisterAsInviterAsync(
            string channel,
            double latitude,
            double longtitude,
            string shortDeviceId,
            string name,
            string address)
        {
            if(!Connected) return new RoundUpServiceOperation {Result = RoundUpServiceOperationResult.OperationFailureCanRetry, SessionId = -1, InviteeId = -1};

            if(string.IsNullOrEmpty(channel)) throw new ArgumentNullException(channel);
            if(string.IsNullOrEmpty(shortDeviceId)) throw new ArgumentNullException(shortDeviceId);

            // Important: You can't pass null strings to Azure (you'll get a BAD_REQUEST response if you do)
            if(string.IsNullOrEmpty(name)) name = string.Empty;
            if(string.IsNullOrEmpty(address)) address = string.Empty;

            // Note that we explicitly cast enum values for Status and Device to integers,  
            // otherwise Azure interprets them as strings. Also note that the row (session) id
            // will be sent back to us from Azure Mobile Services via an MNPS notification

            var session = new Session
            {
                Timestamp = DateTime.Now.ToUniversalTime(),
                Name = name,
                Channel = channel,
                Latitude = latitude,
                Longitude = longtitude,
                Address = address,
                Device = (int) SessionDeviceType.WindowsPhone8,
                ShortDeviceId = shortDeviceId,
                RequestMessageId = (int) RoundUpRequestMessage.SessionStart,
                SessionStatusId = (int) SessionStatusValue.NotSet, // This value is set by the insert script
                RequestDataId = -1,
                RequestData = string.Empty // You can't pass null strings to Azure
            };

            try
            {
                // Insert a new session row
                await _mobileService.GetTable<Session>().InsertAsync(session);

                // We have a new session. The "Insert" script on the Azure Mobile Service "Session" table will now run.
                // This script will request the MPNS to send a "SessionStarted" notification. The new session id (row id, 
                // which will be available to the script) will be included in the "SessionStarted" notification. 
                // The "SessionStarted" notification will be handled by MicrosoftPushNotificationService.OnHttpNotificationReceived()
                // where the raw message will be read and the JSON message body deserialized into a RoundUpNotification
                // object. MicrosoftPushNotificationService then raises the PushNotification event, allowing the message
                // to be processed by the MainPageViewModel (where the session id is saved)

                return new RoundUpServiceOperation { Result = RoundUpServiceOperationResult.OperationSuccess, SessionId = session.id, InviteeId = -1 }; 
            }
            catch(MobileServiceInvalidOperationException ex)
            {
                // Error likely caused by an internal data formatting error - retrying probably won't succeed
                Logger.Log(string.Format("RoundUpService HTTP Status Code: {0}. {1}", ex.Response.StatusCode, ex.Message), new StackFrame(0, true));
                return new RoundUpServiceOperation { Result = MapAzureResponseToServiceOpResult(ex.Message), SessionId = -1, InviteeId = -1 };
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return new RoundUpServiceOperation { Result = RoundUpServiceOperationResult.OperationFailureCanRetry, SessionId = -1, InviteeId = -1 };
            }
        }

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
        public async Task<RoundUpServiceOperation> RegisterAsInviteeAsync(int sessionId,
            string channel,
            double latitude,
            double longtitude,
            string inviterShortDeviceId,
            string name,
            string address)
        {
            if(!Connected) return new RoundUpServiceOperation { Result = RoundUpServiceOperationResult.OperationFailureCanRetry, SessionId = -1, InviteeId = -1 };

            if(string.IsNullOrEmpty(channel)) throw new ArgumentNullException(channel);
            if(string.IsNullOrEmpty(inviterShortDeviceId)) throw new ArgumentNullException(inviterShortDeviceId);

            // Important: You can't pass null strings to Azure (you'll get a BAD_REQUEST response if you do)
            if(string.IsNullOrEmpty(name)) name = string.Empty;
            if(string.IsNullOrEmpty(address)) address = string.Empty;

            var invitee = new Invitee
            {
                sid = sessionId,
                Channel = channel,
                Latitude = latitude,
                Longitude = longtitude,
                Device = (int) SessionDeviceType.WindowsPhone8,
                Name = name,
                Address = address,
                Timestamp = DateTime.Now.ToUniversalTime(),
                RequestMessageId = (int) RoundUpRequestMessage.InviteeJoin,
                InviteeStatusId = (int) InviteeStatusValue.NotSet, // This value is set by the insert script 
                InviterShortDeviceId = inviterShortDeviceId,
                RequestDataId = -1,
                RequestData = string.Empty // You can't pass null strings to Azure
            };

            try
            {
                // The main logic behind the Invitee insert is contained in the Azure Mobile Services Invitee "Insert" script.
                // When the insert takes place, the script:
                //
                // 1. Queries the Session table to find the row for the session id
                // 2. Checks to see if the selected session is *alive* (the SessionStatusId column = SessionStarted or SessionActive)
                // 3. Checks to see that the invitee has the same InviterShortDeviceId as the session's ShortDeviceId
                // 4. The session row is updated:
                //    SessionStatusId => SessionStatusValue.SessionActive (=2)
                //    RequestMessageId => RoundUpRequestMessage.InviteeJoin (=3)
                // 5. The Invitee row is then inserted into the Invitee table
                // 5. The MPNS is used to send an "InviteeHasAccepted" notification to the Inviter

                await _mobileService.GetTable<Invitee>().InsertAsync(invitee);

                // The inviter-related data we required is passed back from the Azure invitee insert script 
                // as part of the *Invitee object* we passed to the script:
                // invitee.Latitude => Inviter's latitude
                // invitee.Longitude => Inviter's latitude
                // invitee.RequestData => Inviter's name

                return new RoundUpServiceOperation
                {
                    Result = RoundUpServiceOperationResult.OperationSuccess, 
                    SessionId = sessionId,          // The session id
                    InviteeId = invitee.id,         // Our invitee id
                    Latitude = invitee.Latitude,    // The *inviter's* latitude
                    Longitude = invitee.Longitude,  // The *inviter's* longitude
                    Data = invitee.Name             // The *inviter's* name
                }; 

            }
            catch(MobileServiceInvalidOperationException ex)
            {
                // Error likely caused by an internal data formatting error - retrying probably won't succeed
                Logger.Log(string.Format("RoundUpService HTTP Status Code: {0}. {1}", ex.Response.StatusCode, ex.Message), new StackFrame(0, true));
                return new RoundUpServiceOperation
                {
                    Result = MapAzureResponseToServiceOpResult(ex.Message), 
                    SessionId = -1, 
                    InviteeId = -1
                };
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return new RoundUpServiceOperation
                {
                    Result = RoundUpServiceOperationResult.OperationFailureCanRetry, 
                    SessionId = -1, 
                    InviteeId = -1
                };
            }
        }

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
        public async Task<RoundUpServiceOperationResult> UpdateRoundUpPointLocationAsync(
            int sessionId,
            string channel,
            string inviterShortDeviceId,
            double latitude,
            double longtitude,
            string name,
            string address)
        {
            return await Task.Run(() => RoundUpServiceOperationResult.OperationNotSupported);

            // *** In version 1.0 we do not support changing the roundup location while the session is started/active ***
            // **********************************************************************************************************

            //if(!Connected) return RoundUpServiceOperationResult.OperationFailureCanRetry;

            //if(string.IsNullOrEmpty(channel)) throw new ArgumentNullException(channel);
            //if(string.IsNullOrEmpty(inviterShortDeviceId)) throw new ArgumentNullException(inviterShortDeviceId);

            //// Important: You can't pass null strings to Azure (you'll get a BAD_REQUEST response if you do)
            //if(string.IsNullOrEmpty(name)) name = string.Empty;
            //if(string.IsNullOrEmpty(address)) address = string.Empty;

            //var session = new Session
            //{
            //    Timestamp = DateTime.Now,
            //    id = sessionId,
            //    Name = name,
            //    Address = address,
            //    Channel = channel,
            //    Latitude = latitude,
            //    Longitude = longtitude,
            //    ShortDeviceId = inviterShortDeviceId,
            //    Device = (int)SessionDeviceType.WindowsPhone8,
            //    RequestMessageId = (int) RoundUpRequestMessage.RoundUpLocationChange,
            //    SessionStatusId = (int)SessionStatusValue.SessionActive,  // Only ever need to update Azure for an active session
            //    RequestDataId = -1,
            //    RequestData = string.Empty // You can't pass null strings to Azure
            //};

            //try
            //{
            //    // Update the session row
            //    await _mobileService.GetTable<Session>().UpdateAsync(session);

            //    // The Azure update script will update the session row with the new RoundUp location.
            //    // It will then send notifications to all invitees passing the new location
            //    // as part of the "RoundUpLocationChange" notification

            //    return RoundUpServiceOperationResult.OperationSuccess;
            //}
            //catch(MobileServiceInvalidOperationException ex)
            //{
            //    // Error likely caused by an internal data formatting error - retrying probably won't succeed
            //    Logger.Log(string.Format("HTTP Status Code: {0}. {1}", ex.Response.StatusCode, ex.Message));
            //    return MapAzureResponseToServiceOpResult(ex.Message);
            //}
            //catch(Exception ex)
            //{
            //    Logger.Log(ex, new StackFrame(0, true));
            //    return RoundUpServiceOperationResult.OperationFailureCanRetry;  // Session row update failed - probably a network error
            //}
        }

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
        public async Task<RoundUpServiceOperationResult> UpdateInviteeLocationAsync(int id,
            int sessionId,
            string channel,
            double latitude,
            double longtitude,
            string inviterShortDeviceId,
            string name,
            string address)
        {
            if(!Connected) return RoundUpServiceOperationResult.OperationFailureCanRetry;

            if(string.IsNullOrEmpty(channel)) throw new ArgumentNullException(channel);
            if(string.IsNullOrEmpty(inviterShortDeviceId)) throw new ArgumentNullException(inviterShortDeviceId);

            // Important: You can't pass null strings to Azure (you'll get a BAD_REQUEST response if you do)
            if(string.IsNullOrEmpty(name)) name = string.Empty;
            if(string.IsNullOrEmpty(address)) address = string.Empty;

            var invitee = new Invitee
            {
                id = id,
                sid = sessionId,
                Channel = channel,
                Latitude = latitude,
                Longitude = longtitude,
                Device = (int) SessionDeviceType.WindowsPhone8,
                Name = name,
                Address = address,
                Timestamp = DateTime.Now.ToUniversalTime(),
                RequestMessageId = (int) RoundUpRequestMessage.InviteeLocationUpdate,
                InviteeStatusId = (int) InviteeStatusValue.InviteeIsEnRoute,
                InviterShortDeviceId = inviterShortDeviceId,
                RequestDataId = -1,
                RequestData = string.Empty
            };

            try
            {
                await _mobileService.GetTable<Invitee>().UpdateAsync(invitee);
                return RoundUpServiceOperationResult.OperationSuccess;
            }
            catch(MobileServiceInvalidOperationException ex)
            {
                Logger.Log(string.Format("RoundUpService HTTP Status Code: {0}. {1}", ex.Response.StatusCode, ex.Message));
                return MapAzureResponseToServiceOpResult(ex.Message);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return RoundUpServiceOperationResult.OperationFailureCanRetry;
            }
        }

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
        public async Task<RoundUpServiceOperationResult> InviteeHasArrivedAsync(int id,
            int sessionId,
            string channel,
            double latitude,
            double longtitude,
            string inviterShortDeviceId,
            string name,
            string address)
        {
            if(!Connected) return RoundUpServiceOperationResult.OperationFailureCanRetry;

            if(string.IsNullOrEmpty(channel)) throw new ArgumentNullException(channel);
            if(string.IsNullOrEmpty(inviterShortDeviceId)) throw new ArgumentNullException(inviterShortDeviceId);

            // Important: You can't pass null strings to Azure (you'll get a BAD_REQUEST response if you do)
            if(string.IsNullOrEmpty(name)) name = string.Empty;
            if(string.IsNullOrEmpty(address)) address = string.Empty;

            var invitee = new Invitee
            {
                id = id,
                sid = sessionId,
                Channel = channel,
                Latitude = latitude,
                Longitude = longtitude,
                Device = (int) SessionDeviceType.WindowsPhone8,
                Name = name,
                Address = address,
                Timestamp = DateTime.Now.ToUniversalTime(),
                RequestMessageId = (int) RoundUpRequestMessage.InviteeHasArrived,
                InviteeStatusId = (int) InviteeStatusValue.InviteeHasArrived,
                InviterShortDeviceId = inviterShortDeviceId,
                RequestDataId = -1,
                RequestData = string.Empty
            };

            try
            {
                await _mobileService.GetTable<Invitee>().UpdateAsync(invitee);
                return RoundUpServiceOperationResult.OperationSuccess;
            }
            catch(MobileServiceInvalidOperationException ex)
            {
                Logger.Log(string.Format("RoundUpService HTTP Status Code: {0}. {1}", ex.Response.StatusCode, ex.Message));
                return MapAzureResponseToServiceOpResult(ex.Message);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return RoundUpServiceOperationResult.OperationFailureCanRetry;
            }
        }

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
        public async Task<RoundUpServiceOperationResult> CancelSessionAsync(int sessionId, string inviterShortDeviceId, string name, string channel)
        {
            if(!Connected) return RoundUpServiceOperationResult.OperationFailureCanRetry;

            if(string.IsNullOrEmpty(channel)) throw new ArgumentNullException(channel);
            if(string.IsNullOrEmpty(inviterShortDeviceId)) throw new ArgumentNullException(inviterShortDeviceId);
            if(string.IsNullOrEmpty(name)) name = string.Empty;

            var session = new Session
            {
                Timestamp = DateTime.Now.ToUniversalTime(), 
                id = sessionId,
                Name = name,
                Address = string.Empty,
                Channel = channel,
                Latitude = 0,
                Longitude = 0,
                ShortDeviceId = inviterShortDeviceId,
                Device = (int)SessionDeviceType.WindowsPhone8,
                RequestMessageId = (int)RoundUpRequestMessage.SessionCancel,
                SessionStatusId = (int)SessionStatusValue.SessionCancelledByInviter,
                RequestDataId = -1,
                RequestData = string.Empty 
            };

            try
            {
                // Update the session row
                await _mobileService.GetTable<Session>().UpdateAsync(session);

                // The Azure update script will update the session row to the cancelled status.
                // It will then send notifications to all invitees telling the session is cancelled

                return RoundUpServiceOperationResult.OperationSuccess;
            }
            catch(MobileServiceInvalidOperationException ex)
            {
                // Error likely caused by an internal data formatting error - retrying probably won't succeed
                Logger.Log(string.Format("RoundUpService HTTP Status Code: {0}. {1}", ex.Response.StatusCode, ex.Message));
                return MapAzureResponseToServiceOpResult(ex.Message);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return RoundUpServiceOperationResult.OperationFailureCanRetry;  // Session row update failed - probably a network error
            }
        }

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
        public async Task<RoundUpServiceOperationResult> CancelInviteeSessionAsync(int id, int sessionId, string inviterShortDeviceId, string name)
        {
            if(!Connected) return RoundUpServiceOperationResult.OperationFailureCanRetry;

            if(string.IsNullOrEmpty(inviterShortDeviceId)) throw new ArgumentNullException(inviterShortDeviceId);
            if(string.IsNullOrEmpty(name)) name = string.Empty;

            var invitee = new Invitee
            {
                id = id,
                sid = sessionId,
                Channel = string.Empty,
                Latitude = 0,
                Longitude = 0,
                Device = (int)SessionDeviceType.WindowsPhone8,
                Name = name,
                Address = string.Empty,
                Timestamp = DateTime.Now.ToUniversalTime(),
                RequestMessageId = (int)RoundUpRequestMessage.InviteeCancel,
                InviteeStatusId = (int)InviteeStatusValue.InviteeHasCancelled,
                InviterShortDeviceId = inviterShortDeviceId,
                RequestDataId = -1,
                RequestData = string.Empty
            };

            try
            {
                // Update the invitee row
                await _mobileService.GetTable<Invitee>().UpdateAsync(invitee);

                // The Azure update script will update the session row to the cancelled status.
                // A notification will be sent to the inviter

                return RoundUpServiceOperationResult.OperationSuccess;
            }
            catch(MobileServiceInvalidOperationException ex)
            {
                // Error likely caused by an internal data formatting error - retrying probably won't succeed
                Logger.Log(string.Format("RoundUpService HTTP Status Code: {0}. {1}", ex.Response.StatusCode, ex.Message));
                return MapAzureResponseToServiceOpResult(ex.Message);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return RoundUpServiceOperationResult.OperationFailureCanRetry;  // Session row update failed - probably a network error
            }
        }

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
        public async Task<RoundUpServiceOperationResult> CloseSessionAsync(int id,
            string channel,
            double latitude,
            double longtitude,
            string shortDeviceId,
            string name,
            string address)
        {
            if(!Connected) return RoundUpServiceOperationResult.OperationFailureCanRetry;

            if(string.IsNullOrEmpty(channel)) throw new ArgumentNullException(channel);
            if(string.IsNullOrEmpty(shortDeviceId)) throw new ArgumentNullException(shortDeviceId);

            // Important: You can't pass null strings to Azure (you'll get a BAD_REQUEST response if you do)
            if(string.IsNullOrEmpty(name)) name = string.Empty;
            if(string.IsNullOrEmpty(address)) address = string.Empty;

            var session = new Session
            {
                id = id,
                Timestamp = DateTime.Now.ToUniversalTime(),
                Name = name,
                Channel = channel,
                Latitude = latitude,
                Longitude = longtitude,
                Address = address,
                Device = (int) SessionDeviceType.WindowsPhone8,
                ShortDeviceId = shortDeviceId,
                RequestMessageId = (int) RoundUpRequestMessage.SessionHasEnded,
                SessionStatusId = (int) SessionStatusValue.SessionHasEnded,
                RequestDataId = -1,
                RequestData = string.Empty
            };

            try
            {
                // Update a session row
                await _mobileService.GetTable<Session>().UpdateAsync(session);
                return RoundUpServiceOperationResult.OperationSuccess;
            }
            catch(MobileServiceInvalidOperationException ex)
            {
                // Error likely caused by an internal data formatting error - retrying probably won't succeed
                Logger.Log(string.Format("RoundUpService HTTP Status Code: {0}. {1}", ex.Response.StatusCode, ex.Message), new StackFrame(0, true));
                return MapAzureResponseToServiceOpResult(ex.Message);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
                return RoundUpServiceOperationResult.OperationFailureCanRetry;  // Session row update failed - probably a network error
            }
        }

        /// <summary>Saves our state</summary>
        public override void SaveState()
        {
            try
            {
                Logger.Log("RoundUpService.SaveState");
                SaveAutoState();  // Save all properties marked with the [AutoState] attribute
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary>Restores our state</summary>
        public override void RestoreState()
        {
            try
            {
                Logger.Log("RoundUpService.RestoreState");
                RestoreAutoState();
            }
            catch (Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));
            }
        }

        /// <summary></summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private RoundUpResponseCode ParseResponseCode(string code)
        {
            RoundUpResponseCode c;
            return !RoundUpResponseCode.TryParse(code, true, out c) ? RoundUpResponseCode.INVALID_CODE : c;
        }

        /// <summary>Maps a custom response from Azure to our RoundUpServiceOperationResult</summary>
        /// <param name="code">The response code from Azure as a string (e.g. "ERR_INSERT_FAILED")</param>
        /// <returns>Returns a RoundUpServiceOperationResult</returns>
        private RoundUpServiceOperationResult MapAzureResponseToServiceOpResult(string code)
        {
            var responseCode = ParseResponseCode(code);
            if(responseCode == RoundUpResponseCode.INVALID_CODE)
                return RoundUpServiceOperationResult.MobileServiceInvalidOperation;

            return MapAzureResponseToServiceOpResult(responseCode);
        }

        /// <summary>Maps a custom response from Azure to our RoundUpServiceOperationResult</summary>
        /// <param name="code">The response code from Azure</param>
        /// <returns>Returns a RoundUpServiceOperationResult</returns>
        private RoundUpServiceOperationResult MapAzureResponseToServiceOpResult(RoundUpResponseCode code)
        {
            switch(code) 
            {
                case RoundUpResponseCode.SUCCESS:
                    return RoundUpServiceOperationResult.OperationSuccess;

                case RoundUpResponseCode.ERR_NOTIFICATION_LIMIT_EXCEEDED:
                    return RoundUpServiceOperationResult.MpnsNotificationLimitExceeded;
                
                case RoundUpResponseCode.ERR_CHANNEL_URI_NULL:
                    return RoundUpServiceOperationResult.ChannelUriNull;

                case RoundUpResponseCode.ERR_INSERT_FAILED:
                    return RoundUpServiceOperationResult.InsertFailed;

                case RoundUpResponseCode.ERR_INVALID_REQUEST_MESSAGE_ID:
                    return RoundUpServiceOperationResult.InvalidRequest;

                case RoundUpResponseCode.ERR_READ_FAILED:
                    return RoundUpServiceOperationResult.ReadFailed;

                case RoundUpResponseCode.ERR_SESSION_DEAD:
                    return RoundUpServiceOperationResult.SessionIsNotAlive;

                case RoundUpResponseCode.ERR_SESSION_NOT_FOUND:
                    return RoundUpServiceOperationResult.SessionDoesNotExist;

                case RoundUpResponseCode.ERR_UPDATE_FAILED:
                    return RoundUpServiceOperationResult.UpdateFailed;

                case RoundUpResponseCode.ERR_WRONG_INVITER_SHORT_DEVICE_ID:
                    return RoundUpServiceOperationResult.InvalidInviterShortDeviceId;

                case RoundUpResponseCode.ERR_TOO_MANY_INVITEES:
                    return RoundUpServiceOperationResult.TooManyInvitees;
            }

            return RoundUpServiceOperationResult.MobileServiceInvalidOperation;
        }
    }
}