using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using System.Threading;
using Adapt.Presentation.Geolocator;
#if !WINDOWS_APP
using Windows.Services.Maps;
#endif

namespace Adapt.Presentation.UWP.Geolocator
{
    /// <summary>
    /// Implementation for Geolocator
    /// </summary>
    public class Geolocator : IGeolocator
    {
        private double desiredAccuracy;
        private Windows.Devices.Geolocation.Geolocator locator = new Windows.Devices.Geolocation.Geolocator();


        public Geolocator()
        {
            DesiredAccuracy = 100;
        }

        /// <summary>
        /// Position error event handler
        /// </summary>
        public event EventHandler<PositionEventArgs> PositionChanged;

        /// <summary>
        /// Position changed event handler
        /// </summary>
        public event EventHandler<PositionErrorEventArgs> PositionError;

        /// <summary>
        /// Gets if device supports heading
        /// </summary>
        public bool SupportsHeading => false;

        /// <summary>
        /// Gets if geolocation is available on device
        /// </summary>
        public bool IsGeolocationAvailable
        {
            get
            {
                var status = GetGeolocatorStatus();

                while (status == PositionStatus.Initializing)
                {
                    Task.Delay(10).Wait();
                    status = GetGeolocatorStatus();
                }

                return status != PositionStatus.NotAvailable;
            }
        }

        /// <summary>
        /// Gets if geolocation is enabled on device
        /// </summary>
        public bool IsGeolocationEnabled
        {
            get
            {
                var status = GetGeolocatorStatus();

                while (status == PositionStatus.Initializing)
                {
                    Task.Delay(10).Wait();
                    status = GetGeolocatorStatus();
                }

                return status != PositionStatus.Disabled && status != PositionStatus.NotAvailable;
            }
        }

        /// <summary>
        /// Desired accuracy in meters
        /// </summary>
        public double DesiredAccuracy
        {
            get => desiredAccuracy;
            set
            {
                desiredAccuracy = value;
                GetGeolocator().DesiredAccuracy = value < 100 ? PositionAccuracy.High : PositionAccuracy.Default;
            }
        }

        /// <summary>
        /// Gets if you are listening for location changes
        /// </summary>
        public bool IsListening { get; private set; }


        /// <summary>
        /// Gets the last known and most accurate location.
        /// This is usually cached and best to display first before querying for full position.
        /// </summary>
        /// <returns>Best and most recent location or null if none found</returns>
        public Task<Position> GetLastKnownLocationAsync()
        {
            return Task.Factory.StartNew<Position>(()=> null);
        }

        /// <summary>
        /// Gets position async with specified parameters
        /// </summary>
        public Task<Position> GetPositionAsync(TimeSpan? timeout, CancellationToken? cancelToken = null, bool includeHeading = false)
        {
            var timeoutMilliseconds = timeout.HasValue ? (int)timeout.Value.TotalMilliseconds : Timeout.Infite;

            if (timeoutMilliseconds < 0 && timeoutMilliseconds != Timeout.Infite)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            if (!cancelToken.HasValue)
                cancelToken = CancellationToken.None;

            var pos = GetGeolocator().GetGeopositionAsync(TimeSpan.FromTicks(0), TimeSpan.FromDays(365));
            cancelToken.Value.Register(o => ((IAsyncOperation<Geoposition>)o).Cancel(), pos);


            var timer = new Timeout(timeoutMilliseconds, pos.Cancel);

            var tcs = new TaskCompletionSource<Position>();

            pos.Completed = (op, s) =>
            {
                timer.Cancel();

                switch (s)
                {
                    case AsyncStatus.Canceled:
                        tcs.SetCanceled();
                        break;
                    case AsyncStatus.Completed:
                        tcs.SetResult(GetPosition(op.GetResults()));
                        break;
                    case AsyncStatus.Error:
                        var ex = op.ErrorCode;
                        if (ex is UnauthorizedAccessException)
                            ex = new GeolocationException(GeolocationError.Unauthorized, ex);

                        tcs.SetException(ex);
                        break;
                }
            };

            return tcs.Task;
        }

        /// <summary>
        /// Retrieve addresses for position.
        /// </summary>
        /// <param name="position">Desired position (latitude and longitude)</param>
        /// <returns>Addresses of the desired position</returns>
#if !WINDOWS_APP
        public async Task<IEnumerable<Address>> GetAddressesForPositionAsync(Position position)
#else
        public Task<IEnumerable<Address>> GetAddressesForPositionAsync(Position position)
#endif
        {
#if !WINDOWS_APP
            if (position == null)
                return null;

            var queryResults =
                await MapLocationFinder.FindLocationsAtAsync(
                        new Geopoint(new BasicGeoposition { Latitude = position.Latitude, Longitude = position.Longitude }));

            return queryResults?.Locations.ToAddresses();
#else
            return Task.FromResult<IEnumerable<Address>>(null);
#endif
        }

        /// <summary>
		/// Start listening for changes
		/// </summary>
		public Task<bool> StartListeningAsync(TimeSpan minTime, double minDistance, bool includeHeading = false, ListenerSettings settings = null)
        {

            if (minTime.TotalMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(minTime));
            if (minDistance < 0)
                throw new ArgumentOutOfRangeException(nameof(minDistance));
            if (IsListening)
                throw new InvalidOperationException();

            IsListening = true;

            var loc = GetGeolocator();
            loc.ReportInterval = (uint)minTime.TotalMilliseconds;
            loc.MovementThreshold = minDistance;
            loc.PositionChanged += OnLocatorPositionChanged;
            loc.StatusChanged += OnLocatorStatusChanged;

            return Task.FromResult(true);
        }

        /// <summary>
        /// Stop listening
        /// </summary>
        public Task<bool> StopListeningAsync()
        {
            if (!IsListening)
                return Task.FromResult(true);

            locator.PositionChanged -= OnLocatorPositionChanged;
            locator.StatusChanged -= OnLocatorStatusChanged;
            IsListening = false;

            return Task.FromResult(true);
        }


        private async void OnLocatorStatusChanged(Windows.Devices.Geolocation.Geolocator sender, StatusChangedEventArgs e)
        {
            GeolocationError error;
            switch (e.Status)
            {
                case PositionStatus.Disabled:
                    error = GeolocationError.Unauthorized;
                    break;

                case PositionStatus.NoData:
                    error = GeolocationError.PositionUnavailable;
                    break;

                default:
                    return;
            }

            if (IsListening)
            {
                await StopListeningAsync();
                OnPositionError(new PositionErrorEventArgs(error));
            }

            locator = null;
        }

        private void OnLocatorPositionChanged(Windows.Devices.Geolocation.Geolocator sender, PositionChangedEventArgs e)
        {
            OnPositionChanged(new PositionEventArgs(GetPosition(e.Position)));
        }

        private void OnPositionChanged(PositionEventArgs e) => PositionChanged?.Invoke(this, e);


        private void OnPositionError(PositionErrorEventArgs e) => PositionError?.Invoke(this, e);


        private Windows.Devices.Geolocation.Geolocator GetGeolocator()
        {
            var loc = locator;
            if (loc != null)
            {
                return loc;
            }

            locator = new Windows.Devices.Geolocation.Geolocator();
            locator.StatusChanged += OnLocatorStatusChanged;
            loc = locator;

            return loc;
        }

        private PositionStatus GetGeolocatorStatus()
        {
            var loc = GetGeolocator();
            return loc.LocationStatus;
        }

        private static Position GetPosition(Geoposition position)
        {
            var pos = new Position
            {
                Accuracy = position.Coordinate.Accuracy,
                Latitude = position.Coordinate.Point.Position.Latitude,
                Longitude = position.Coordinate.Point.Position.Longitude,
                Timestamp = position.Coordinate.Timestamp.ToUniversalTime()
            };

            if (position.Coordinate.Heading != null)
                pos.Heading = position.Coordinate.Heading.Value;

            if (position.Coordinate.Speed != null)
                pos.Speed = position.Coordinate.Speed.Value;

            if (position.Coordinate.AltitudeAccuracy.HasValue)
                pos.AltitudeAccuracy = position.Coordinate.AltitudeAccuracy.Value;

            pos.Altitude = position.Coordinate.Point.Position.Altitude;

            return pos;
        }
    }
}
