using System;
using System.ComponentModel;
using System.Device.Location;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using RArcher.Phone.Toolkit.Common;
using RArcher.Phone.Toolkit.Location;
using RArcher.Phone.Toolkit.Logging;
using RoundUp.Annotations;
using RoundUp.ViewModel;

namespace RoundUp.Model
{
    /// <summary>This class encapsulates data related to the roundup (meet-up) location</summary>
    public class RoundUpPoint : IRoundUpPoint, IAutoSaveRestore, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The GeoCoordinate for the roundup point. Setting Location causes the RoundUpPoint Address and 
        /// DistanceToRoundUpPoint properties to be re-evaluated</summary>
        public GeoCoordinate Location
        {
            get { return _location; }
            set
            {
                try
                {
                    if(value == null) return;
                    if(_locationCached != null && _locationCached.Equals(value)) return;

                    _location = value;
                    _locationCached = value;

                    var locationService = IocContainer.Get<ILocationService>();
                    var viewModel = IocContainer.Get<IMainViewModel>();

                    DistanceToRoundUpPoint = locationService.GetDistance(viewModel.CurrentLocation, _location);
                    OnPropertyChanged();

                    // Get the address of the new roundup point - this is an async op so can't be done directly 
                    // from a property, hence the private helper method
                    GetAddressAsync();  
                }
                catch(Exception ex)
                {
                    Logger.Log(ex, "Unable to change RoundUpPoint location", new StackFrame(0, true));                    
                }
            }
        }

        /// <summary>The "Meet here" text</summary>
        public string Text
        {
            get { return Strings.GetStringResource("RoundUpLocationMarker", "Meet here"); }
        }

        /// <summary>The address of the RoundUp point (if any)</summary>
        public string Address
        {
            get { return _address; }
            set 
            { 
                _address = value; 
                OnPropertyChanged();
                OnPropertyChanged("CombinedTextAddressAndDistance");
                OnPropertyChanged("CombinedTextAndDistance");
            }
        }

        /// <summary>
        /// The distance (in meters) between the device's current location and the RoundUp 
        /// location (will be zero if the inviter is using their current location as the roundup point)
        /// </summary>
        public double DistanceToRoundUpPoint
        {
            get { return _distanceToRoundUpPoint; }
            set
            {
                _distanceToRoundUpPoint = value; 
                OnPropertyChanged();
                OnPropertyChanged("CombinedTextAddressAndDistance");
                OnPropertyChanged("CombinedTextAndDistance");
            }
        }

        /// <summary>A string containing the "Meet here" text, and the distance to the RoundUp location</summary>
        public string CombinedTextAndDistance
        {
            get
            {
                try
                {
                    string unit;
                    double distance;

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

                    return string.Format("{0}, {1} {2}", Text, distance, unit);
                }
                catch(Exception ex)
                {
                    Logger.Log(ex, new StackFrame(0, true));
                    return "???";
                }
            }
        }

        /// <summary>A string containing the "Meet here" text, the address of the roundup point, and the distance to the RoundUp location</summary>
        public string CombinedTextAddressAndDistance
        {
            get
            {
                try
                {
                    string unit;
                    double distance;

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

                    var tmp = Address;
                    return string.IsNullOrEmpty(tmp) ? string.Format("{0}, {1} {2}", Text, distance, unit) : string.Format("{0}, {1} {2}\n{3}", Text, distance, unit, Address);
                }
                catch(Exception ex)
                {
                    Logger.Log(ex, new StackFrame(0, true));
                    return "???";
                }
            }
        }

        /// <summary>A string containing the distance to the roundup point, including either the "km" or "meters" prefix</summary>
        public string DistanceText
        {
            get
            {
                try
                {
                    string unit;
                    double distance;

                    if(_distanceToRoundUpPoint < 1000)
                    {
                        unit = "meters";
                        distance = Math.Round(_distanceToRoundUpPoint, 0);
                    }
                    else
                    {
                        unit = "km";
                        distance = Math.Round((_distanceToRoundUpPoint / 1000), 2);
                    }

                    return string.Format("{0} {1}", distance, unit);
                }
                catch(Exception ex)
                {
                    Logger.Log(ex, new StackFrame(0, true));
                    return "???";
                }
            }
        }

        private string _address;
        private double _distanceToRoundUpPoint;
        private GeoCoordinate _location;
        private GeoCoordinate _locationCached;

        /// <summary>Asynchronously gets the address for the roundup location</summary>
        private async void GetAddressAsync()
        {
            try
            {
                var locationService = IocContainer.Get<ILocationService>();
                Address = await locationService.GetAddressAsync(Location);
            }
            catch(Exception ex)
            {
                Logger.Log(ex, new StackFrame(0, true));                                    
            }
        }

        /// <summary>Flattens an instance of the object to a string that can be saved to app state or isolated storage</summary>
        /// <returns>Returns a flattened instance of the object that can be saved to app state or isolated storage</returns>
        public string ToStringRepresentation()
        {
            try
            {
                var sb = new StringBuilder();

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

                sb.Append(Address ?? string.Empty);
                sb.Append("|");

                sb.Append(DistanceToRoundUpPoint.ToString(CultureInfo.InvariantCulture));

                return sb.ToString();
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error saving RoundUpPoint object to string representation", new StackFrame(0, true));
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

                Location = new GeoCoordinate(double.Parse(properties[0]), double.Parse(properties[1]), double.Parse(properties[2]));
                Address = properties[3];
                DistanceToRoundUpPoint = double.Parse(properties[4]);

                return this;
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "Error restoring RoundUpPoint object from string representation", new StackFrame(0, true));
                return false;
            }
        }

        /// <summary>The PropertyChanged event is raised when any of this object's properties are changed</summary>
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}