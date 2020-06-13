using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RoundUp.Annotations;

namespace RoundUp.Model
{
    /// <summary>Maps to the Session table in the RoundUp Windows Azure Mobile Service database</summary>
    public class Session : ISession, INotifyPropertyChanged
    {
        // Events -------------------------------------------------------------

        /// <summary>The PropertyChanged event</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        // Private members ----------------------------------------------------

        private int _id;
        private DateTime _timestamp;
        private string _name;
        private string _channel;
        private double _latitude;
        private double _longitude;
        private string _address;
        private string _shortDeviceId;
        private int _device;
        private int _requestMessageId;
        private int _sessionStatusId;
        private int _requestDataId;
        private string _requestData;

        // Properties ---------------------------------------------------------

        /// <summary>Session (row) id</summary>
        /// <remarks>The max value for Azure Mobile Services SQL id columns (defined as bigint(MSSQL)) is 9,223,372,036,854,775,807</remarks>
        public int id
        {
            get { return _id; }
            set { _id = value; OnPropertyChanged(); }
        }

        /// <summary>When the session was *started*. Will auto-expire after 24hrs</summary>
        public DateTime Timestamp
        {
            get { return _timestamp; }
            set { _timestamp = value; OnPropertyChanged(); }
        }

        /// <summary>Name/alias of the person that started the session</summary>
        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged(); }
        }

        /// <summary>Channel URI used to communicate with the MPNS (inviter's device)</summary>
        public string Channel
        {
            get { return _channel; }
            set { _channel = value; OnPropertyChanged(); }
        }

        /// <summary>Latitude of the RoundUp point (the inviter's device)</summary>
        public double Latitude
        {
            get { return _latitude; }
            set { _latitude = value; OnPropertyChanged(); }
        }

        /// <summary>Longitude of the RoundUp point (the inviter's device)</summary>
        public double Longitude
        {
            get { return _longitude; }
            set { _longitude = value; OnPropertyChanged(); }
        }

        /// <summary>Address (if any) of the RoundUp point</summary>
        public string Address
        {
            get { return _address; }
            set { _address = value; OnPropertyChanged(); }
        }

        /// <summary>The 8-character device id for the device that initiated the session</summary>
        public string ShortDeviceId
        {
            get { return _shortDeviceId; }
            set { _shortDeviceId = value; OnPropertyChanged(); }
        }

        /// <summary>Code for the type of device. See SessionDeviceType (0 = WP8, 1 = Win8/RT, 2 = iOS, 3 = Android)</summary>
        public int Device
        {
            get { return _device; }
            set { _device = value; OnPropertyChanged();}
        }

        /// <summary>The message being sent to the session. See RoundUpRequestMessage enum</summary>
        public int RequestMessageId
        {
            get { return _requestMessageId; }
            set { _requestMessageId = value; OnPropertyChanged(); }
        }

        /// <summary>The current status of the session. See SessionStatus enum</summary>
        public int SessionStatusId
        {
            get { return _sessionStatusId; }
            set { _sessionStatusId = value; OnPropertyChanged();}
        }

        /// <summary>Additional request-specific numberic data related to the request (e.g. InviteeId when RequestMessageId = RoundUpRequestMessage.InviteeJoin)</summary>
        public int RequestDataId
        {
            get { return _requestDataId; }
            set { _requestDataId = value; OnPropertyChanged();}
        }

        /// <summary>Additional request-specific string data related to the request</summary>
        public string RequestData
        {
            get { return _requestData; }
            set { _requestData = value; OnPropertyChanged();}
        }

        /// <summary>PropertyChanged event, raised when any of this object's properties change</summary>
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}