﻿// The MIT License (MIT)

// Copyright (c) 2016 Ben Abelshausen

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using IDP.Processors;
using IDP.Processors.RouterDb;
using Itinero.Osm.Vehicles;
using IDP.Processors.Osm;
using Itinero.Algorithms.Search.Hilbert;

namespace IDP.Switches.RouterDb
{
    /// <summary>
    /// A switch to create a router db.
    /// </summary>
    class SwitchCreateRouterDb : Switch
    {
        /// <summary>
        /// Creates a switch to create a router db.
        /// </summary>
        public SwitchCreateRouterDb(string[] a)
            : base(a)
        {

        }
        
        /// <summary>
        /// Gets the names.
        /// </summary>
        public static string[] Names
        {
            get
            {
                return new string[] { "--create-routerdb" };
            }
        }

        /// <summary>
        /// Parses this command into a processor given the arguments for this switch. Consumes the previous processors and returns how many it consumes.
        /// </summary>
        public override int Parse(List<Processor> previous, out Processor processor)
        {
            var vehicles = new List<Vehicle>(new Vehicle[]
            {
                Vehicle.Car
            });
            var allCore = false;
            var keepWayIds = false;

            for(var i = 0; i < this.Arguments.Length; i++)
            {
                string key, value;
                if (SwitchParsers.SplitKeyValue(this.Arguments[i], out key, out value))
                {
                    switch (key.ToLower())
                    {
                        case "vehicles":
                        case "vehicle":
                            string[] vehicleValues;
                            if (SwitchParsers.SplitValuesArray(value.ToLower(), out vehicleValues))
                            { // split the values array.
                                vehicles = new List<Vehicle>(vehicleValues.Length);
                                for (int v = 0; v < vehicleValues.Length; v++)
                                {
                                    Vehicle vehicle;
                                    if (!Vehicle.TryGetByUniqueName(vehicleValues[v], out vehicle))
                                    {
                                        if (vehicleValues[v] == "all")
                                        { // all vehicles.
                                            vehicles.Add(Vehicle.Bicycle);
                                            vehicles.Add(Vehicle.BigTruck);
                                            vehicles.Add(Vehicle.Bus);
                                            vehicles.Add(Vehicle.Car);
                                            vehicles.Add(Vehicle.Moped);
                                            vehicles.Add(Vehicle.MotorCycle);
                                            vehicles.Add(Vehicle.Pedestrian);
                                            vehicles.Add(Vehicle.SmallTruck);
                                        }
                                        else if (vehicleValues[v] == "motorvehicle" ||
                                            vehicleValues[v] == "motorvehicles")
                                        { // all motor vehicles.
                                            vehicles.Add(Vehicle.BigTruck);
                                            vehicles.Add(Vehicle.Bus);
                                            vehicles.Add(Vehicle.Car);
                                            vehicles.Add(Vehicle.MotorCycle);
                                            vehicles.Add(Vehicle.SmallTruck);
                                        }
                                        else
                                        {
                                            throw new SwitchParserException("--create-routerdb",
                                                string.Format("Invalid parameter value for command --create-routerdb: Vehicle profile '{0}' not found.",
                                                    vehicleValues[v]));
                                        }
                                    }
                                    else
                                    {
                                        vehicles.Add(vehicle);
                                    }
                                }
                            }
                            break;
                        case "allcore":
                            if (SwitchParsers.IsTrue(value))
                            {
                                allCore = true;;
                            }
                            break;
                        case "keepwayids":
                            if (SwitchParsers.IsTrue(value))
                            {
                                keepWayIds = true; ;
                            }
                            break;
                        default:
                            throw new SwitchParserException("--create-routerdb",
                                string.Format("Invalid parameter for command --create-routerdb: {0} not recognized.", key));
                    }
                }
            }
            
            if (!(previous[previous.Count - 1] is Processors.Osm.IProcessorOsmStreamSource))
            {
                throw new Exception("Expected an OSM stream source.");
            }

            var source = (previous[previous.Count - 1] as Processors.Osm.IProcessorOsmStreamSource).Source;
            Func<Itinero.RouterDb> getRouterDb = () =>
            {
                var routerDb = new Itinero.RouterDb();

                // load the data.
                var target = new Itinero.IO.Osm.Streams.RouterDbStreamTarget(routerDb,
                    vehicles.ToArray(), allCore);
                if (keepWayIds)
                { // add way id's.
                    var eventsFilter = new OsmSharp.Streams.Filters.OsmStreamFilterDelegate();
                    eventsFilter.MoveToNextEvent += EventsFilter_AddWayId;
                    eventsFilter.RegisterSource(source);
                    target.RegisterSource(eventsFilter, false);
                }
                else
                { // use the source as-is.
                    target.RegisterSource(source);
                }
                target.Pull();

                // sort the network.
                routerDb.Sort();

                return routerDb;
            };
            processor = new Processors.RouterDb.ProcessorRouterDbSource(getRouterDb);

            return 1;
        }

        
        static OsmSharp.OsmGeo EventsFilter_AddWayId(OsmSharp.OsmGeo osmGeo, object param)
        {
            if (osmGeo.Type == OsmSharp.OsmGeoType.Way)
            {
                var tags = new OsmSharp.Tags.TagsCollection(osmGeo.Tags);
                foreach (var tag in tags)
                {
                    if (tag.Key == "bridge")
                    {
                        continue;
                    }
                    if (tag.Key == "tunnel")
                    {
                        continue;
                    }
                    if (tag.Key == "lanes")
                    {
                        continue;
                    }
                    if (!Vehicle.Car.IsRelevant(tag.Key, tag.Value))
                    {
                        osmGeo.Tags.RemoveKeyValue(tag);
                    }
                }

                osmGeo.Tags.Add("way_id", osmGeo.Id.ToString());
            }
            return osmGeo;
        }
    }
}