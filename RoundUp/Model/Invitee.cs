using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RoundUp.Annotations;

namespace RoundUp.Model
{
    /// <summary>Maps to the Session table in the RoundUp Windows Azure Mobile Service database</summary>
    public class Invitee : IInvitee, INotifyPropertyChanged
    {
        // Events -------------------------------------------------------------

        public event PropertyChangedEventHandler PropertyChanged;

        // Properties ---------------------------------------------------------

        /// <summary>The invitee (row) id</summary>
        /// <remarks>The max value for Azure Mobile Services SQL id columns (defined as bigint(MSSQL)) is 9,223,372,036,854,775,807</remarks>
        public int id
        {
            get { return _id; }
            set { _id = value; OnPropertyChanged();}
        }

        /// <summary>The session (row) id</summary>
        public int sid
        {
            get { return _sid; }
            set { _sid = value; OnPropertyChanged(); }
        }

        /// <summary>When the invitee accepted the invitee to join the RoundUp</summary>
        public DateTime Timestamp
        {
            get { return _timestamp; }
            set { _timestamp = value; OnPropertyChanged(); }
        }

        /// <summary>The invitee's name/alias</summary>
        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged(); }
        }

        /// <summary>The invitee's MPNS channel URI</summary>
        public string Channel
        {
            get { return _channel; }
            set { _channel = value; OnPropertyChanged(); }
        }

        /// <summary>The latitude of the invitee's current location</summary>
        public double Latitude
        {
            get { return _latitude; }
            set { _latitude = value; OnPropertyChanged(); }
        }

        /// <summary>The longitude of the invitee's current location</summary>
        public double Longitude
        {
            get { return _longitude; }
            set { _longitude = value; OnPropertyChanged(); }
        }

        /// <summary>The address (if any) for the invitee's current location</summary>
        public string Address
        {
            get { return _address; }
            set { _address = value; OnPropertyChanged(); }
        }

        /// <summary>Code for the type of device. See SessionDeviceType (0 = WP8, 1 = Win8/RT, 2 = iPhone, 3 = Android)</summary>
        public int Device
        {
            get { return _device; }
            set { _device = value; OnPropertyChanged(); }
        }

        /// <summary>The message being sent to the session. See RoundUpRequestMessage enum</summary>
        public int RequestMessageId
        {
            get { return _requestMessageId; }
            set { _requestMessageId = value; OnPropertyChanged();}
        }

        /// <summary>The current status of the invitee. See InviteeStatus enum</summary>
        public int InviteeStatusId
        {
            get { return _inviteeStatusId; }
            set { _inviteeStatusId = value; OnPropertyChanged();}
        }

        /// <summary>The 8-char short device id for the inviter's device (part of the invite code) </summary>
        public string InviterShortDeviceId
        {
            get { return _inviterShortDeviceId; }
            set { _inviterShortDeviceId = value; OnPropertyChanged();}
        }

        /// <summary>Additional request-specific numberic data related to the request</summary>
        public int RequestDataId
        {
            get { return _requestDataId; }
            set { _requestDataId = value; OnPropertyChanged();}
        }

        /// <summary>Additional request-specific string data related to the request (e.g. used for instant messages)</summary>
        public string RequestData
        {
            get { return _requestData; }
            set { _requestData = value; OnPropertyChanged();}
        }

        // Private members ----------------------------------------------------

        private int _id;
        private int _sid;
        private DateTime _timestamp;
        private string _name;
        private string _channel;
        private double _latitude;
        private double _longitude;
        private string _address;
        private int _device;
        private int _requestMessageId;
        private int _inviteeStatusId;
        private string _inviterShortDeviceId;
        private int _requestDataId;
        private string _requestData;

        // Events -------------------------------------------------------------

        /// <summary>The PropertyChanged event is raised whenany of this object's properties are changed</summary>
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}