﻿
using Adapt.Presentation.Geolocator;
using System.Collections.Generic;
using System.Linq;
using Windows.Services.Maps;

namespace Adapt.Presentation.UWP.Geolocator
{
    internal static class GeolocatorUtils
    {
        internal static IEnumerable<Address> ToAddresses(this IEnumerable<MapLocation> addresses)
        {
            return addresses.Select(address => new Address
            {
                Longitude = address.Point.Position.Longitude,
                Latitude = address.Point.Position.Latitude,
                FeatureName = address.DisplayName,
                PostalCode = address.Address.PostCode,
                CountryCode = address.Address.CountryCode,
                CountryName = address.Address.Country,
                Thoroughfare = address.Address.Street,
                SubThoroughfare = address.Address.Region,
                Locality = address.Address.Town
            });
        }
    }
}
