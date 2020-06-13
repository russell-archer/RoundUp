using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using RArcher.Phone.Toolkit.Common;
using RArcher.Phone.Toolkit.Logging;
using RArcher.Phone.Toolkit.Mpns.Common;
using RoundUp.Annotations;

namespace RoundUp.Model
{
    /// <summary>
    /// Defines the content of each message sent from the RoundUp Azure service via the MPNS. 
    /// </summary>
    public class RoundUpNotification : IMpnsNotification, IRoundUpNotification, IAutoSaveRestore, INotifyPropertyChanged
    {
        // Events -------------------------------------------------------------

        /// <summary>The PropertyChanged event is raised when any of this object's properties change</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        // Properties ---------------------------------------------------------

        /// <summary>Row id</summary>
        public int id
        {
            get { return _id; }
            set { _id = value; OnPropertyChanged();}
        }

        /// <summary>The intended recipient of the message (0 == inviter; 1 == invitee; -1 == unknown)</summary>
        public int Recipient
        {
            get { return _recipient; }
            set { _recipient = value; OnPropertyChanged();}
        }

        /// <summary>Session ID (RoundUp Azure service's Session table row id) for the notification</summary>
        public int SessionId
        {
            get { return _sessionId; }
            set { _sessionId = value; OnPropertyChanged(); }
        }

        /// <summary>Invitee ID (RoundUp Azure service's Invitee table row id) for the notification. If not applicable will be -1</summary>
        public int InviteeId
        {
            get { return _inviteeId; }
            set { _inviteeId = value; OnPropertyChanged(); }
        }

        /// <summary>Notification-specific message ID. Holds a RoundUpNotificationMessage enum in string format</summary>
        public string MessageId
        {
            get { return _messageId; }
            set { _messageId = value; OnPropertyChanged(); }
        }

        /// <summary>Additional info related to the message (may be an empty string)</summary>
        public string Data
        {
            get { return _data; }
            set { _data = value; OnPropertyChanged(); }
        }

        /// <summary>A 8-character string representing a simplified id for the device</summary>
        public string ShortDeviceId
        {
            get { return _shortDeviceId; }
            set { _shortDeviceId = value; OnPropertyChanged();}
        }

        /// <summary>Latitude value related to the notification (may be an empty string)</summary>
        public double Latitude
        {
            get { return _latitude; }
            set { _latitude = value; OnPropertyChanged();}
        }

        /// <summary>Longitude value related to the notification (may be an empty string)</summary>
        public double Longitude
        {
            get { return _longitude; }
            set { _longitude = value; OnPropertyChanged();}
        }

        // Private members ----------------------------------------------------

        private int _sessionId;
        private int _inviteeId;
        private string _messageId;
        private string _data;
        private string _shortDeviceId;
        private double _latitude;
        private double _longitude;
        private int _id;
        private int _recipient;

        // Methods ------------------------------------------------------------

        /// <summary>The PropertyChanged event is raised when any of this object's properties change</summary>
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>Flattens an instance of the object to a string that can be saved to app state or isolated storage</summary>
        /// <returns>Returns a flattened instance of the object that can be saved to app state or isolated storage</returns>
        public string ToStringRepresentation()
        {
            // We save our properties in the following order: 
            // id, Recipient, SessionId, InviteeId, MessageId, Data, ShortDeviceId, Latitude, Longitude

            try
            {
                var sb = new StringBuilder();

                sb.Append(id.ToString(CultureInfo.InvariantCulture));
                sb.Append("|");

                sb.Append(Recipient.ToString(CultureInfo.InvariantCulture));
                sb.Append("|");

                sb.Append(SessionId.ToString(CultureInfo.InvariantCulture));
                sb.Append("|");

                sb.Append(InviteeId.ToString(CultureInfo.InvariantCulture));
                sb.Append("|");

                sb.Append(string.IsNullOrEmpty(MessageId) ? string.Empty : MessageId);
                sb.Append("|");

                sb.Append(string.IsNullOrEmpty(Data) ? string.Empty : Data);
                sb.Append("|");

                sb.Append(string.IsNullOrEmpty(ShortDeviceId) ? string.Empty : ShortDeviceId);
                sb.Append("|");

                sb.Append(Latitude.ToString(CultureInfo.InvariantCulture));
                sb.Append("|");

                sb.Append(Longitude.ToString(CultureInfo.InvariantCulture));

                return sb.ToString();
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error saving RoundUpNotiifcation object to string representation", new StackFrame(0, true));
                return string.Empty;
            }
        }

        /// <summary>
        /// A method that takes a JSON object and returns a notification that implements IMpnsNotification
        /// For example, use: JsonConvert.DeserializeObject&lt;T&gt;(json)
        /// </summary>
        /// <param name="json">A JSON-formatted string that contains custom notification to deserialize</param>
        /// <returns>Returns a notification that implements IMpnsNotification</returns>
        public IMpnsNotification FromJsonRepresentation(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<RoundUpNotification>(json);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error deserializing RoundUpNotiifcation from json representation", new StackFrame(0, true));
                return null;
            }
        }

        /// <summary>Repopulates the object from a flattened string representation of its properties</summary>
        /// <param name="sObject">A flat string representation of the object's properties</param>
        /// <returns>Returns true if the object's properties were successfully rehydrated from a flattened string representation</returns>
        public object FromStringRepresentation(string sObject)
        {
            // Our properties should have been saved in the following order:
            // id, Recipient, SessionId, InviteeId, MessageId, Data, ShortDeviceId, Latitude, Longitude

            if(string.IsNullOrEmpty(sObject)) return null;

            try
            {
                var properties = sObject.Split('|');

                id = int.Parse(properties[0]);
                Recipient = int.Parse(properties[1]);
                SessionId = int.Parse(properties[2]);
                InviteeId = int.Parse(properties[3]);
                MessageId = properties[4];
                Data = properties[5];
                ShortDeviceId = properties[6];
                Latitude = double.Parse(properties[7]);
                Longitude = double.Parse(properties[8]);

                return this;
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error restoring RoundUpNotiifcation object from string representation", new StackFrame(0, true));
                return false;
            }
        }
    }
}
