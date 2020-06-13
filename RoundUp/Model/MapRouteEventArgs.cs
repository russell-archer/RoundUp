using System;
using Microsoft.Phone.Maps.Controls;

namespace RoundUp.Model
{
    /// <summary>Provides Route information when a route to the new RoundUp location becomes available</summary>
    public class MapRouteEventArgs : EventArgs
    {
        /// <summary>Information on the route. Can be added to a Map control using Map.AddRoute</summary>
        public MapRoute NewRoute { get; set; }

        public MapRouteEventArgs() { }
        public MapRouteEventArgs(MapRoute route)
        {
            NewRoute = route;
        }
    }
}