using System;
using System.ComponentModel;
using System.Device.Location;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using RArcher.Phone.Toolkit.Common;
using RArcher.Phone.Toolkit.Logging;
using RoundUp.Annotations;

namespace RoundUp.Model
{
    /// <summary>This class is used to represent individual invitees on the View's map and in the invitee list</summary>
    public class InviteeLocationMarker : IInviteeLocationMarker, IAutoSaveRestore, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Invitee id (same as Invitee table row id)</summary>
        public int id
        {
            get { return _id; }
            set { _id = value; OnPropertyChanged(); }
        }

        /// <summary>The name/alias of the invitee</summary>
        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged();}
        }

        /// <summary>The invitee's current location</summary>
        public GeoCoordinate Location
        {
            get { return _location; }
            set { _location = value; OnPropertyChanged();}
        }

        /// <summary>Distance to the RoundUp location</summary>
        public double DistanceToRoundUpPoint
        {
            get { return _distanceToRoundUpPoint; }
            set { _distanceToRoundUpPoint = value; OnPropertyChanged(); OnPropertyChanged("CombinedNameAndDistance"); }
        }

        /// <summary>A string containing the invitees name and distance to the RoundUp location</summary>
        public string CombinedNameAndDistance
        {
            get
            {
                try
                {
                    if(_distanceToRoundUpPoint < 0) return Name;

                    string unit;
                    double distance;

                    if(_distanceToRoundUpPoint <= 0) return string.Format("{0} arrived", Name);

                    if(_distanceToRoundUpPoint < 1000)
                    {
                        unit = "meters";
                        distance = Math.Round(_distanceToRoundUpPoint, 0);
                    }
                    else
                    {
                        unit = "km";
                        distance = Math.Round((_distanceToRoundUpPoint/1000), 2);
                    }

                    return string.Format("{0}, {1} {2}", Name, distance, unit);
                }
                catch(Exception ex)
                {
                    Logger.Log(ex, new StackFrame(0, true));
                    return "???";                   
                }
            }
        }

        /// <summary>The text of an instant message to send or that has been received (not usedin v1)</summary>
        public string InstantMessage
        {
            get { return _instantMessage; }
            set { _instantMessage = value; OnPropertyChanged(); }
        }

        private int _id;
        private string _name;
        private GeoCoordinate _location;
        private string _instantMessage;
        private double _distanceToRoundUpPoint;

        /// <summary>Flattens an instance of the object to a string that can be saved to app state or isolated storage</summary>
        /// <returns>Returns a flattened instance of the object that can be saved to app state or isolated storage</returns>
        public string ToStringRepresentation()
        {
            // We save our properties in the following order: 
            // id, Name, Location.Latitude, Location.Longitude, Location.Altitude, DistanceToRoundUpPoint (InstantMessage is not saved)
            try
            {
                var sb = new StringBuilder();

                sb.Append(id.ToString(CultureInfo.InvariantCulture));
                sb.Append("|");

                sb.Append(Name);
                sb.Append("|");

                if(Location != null)
                {
                    sb.Append(double.IsNaN(Location.Latitude) ? "0" : Location.Latitude.ToString(CultureInfo.InvariantCulture));
                    sb.Append("|");

                    sb.Append(double.IsNaN(Location.Longitude) ? "0" : Location.Longitude.ToString(CultureInfo.InvariantCulture));
                    sb.Append("|");

                    sb.Append(double.IsNaN(Location.Altitude) ? "0" : Location.Altitude.ToString(CultureInfo.InvariantCulture));
                    sb.Append("|");
                }
                else sb.Append("0|0|0|");

                sb.Append(DistanceToRoundUpPoint.ToString(CultureInfo.InvariantCulture));

                return sb.ToString();
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error saving InviteeLocationMarker object to string representation", new StackFrame(0, true));
                return string.Empty;
            }
        }

        /// <summary>Repopulates the object from a flattened string representation of its properties</summary>
        /// <param name="sObject">A flat string representation of the object's properties</param>
        /// <returns>Returns true if the object's properties were successfully rehydrated from a flattened string representation</returns>
        public object FromStringRepresentation(string sObject)
        {
            if(string.IsNullOrEmpty(sObject)) return null;

            try
            {
                var properties = sObject.Split('|');

                // Our properties should have been saved in the following order: 
                // id, Name, Location.Latitude, Location.Longitude, Location.Altitude, DistanceToRoundUpPoint (InstantMessage is not saved)

                id = int.Parse(properties[0]);
                Name = properties[1];
                Location = new GeoCoordinate(double.Parse(properties[2]), double.Parse(properties[3]), double.Parse(properties[4]));
                DistanceToRoundUpPoint = double.Parse(properties[5]);
                InstantMessage = string.Empty;

                return this;
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error restoring InviteeLocationMarker object from string representation", new StackFrame(0, true));
                return false;
            }
        }

        /// <summary>The PropertyChanged event is raised whenany of this object's properties are changed</summary>
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}