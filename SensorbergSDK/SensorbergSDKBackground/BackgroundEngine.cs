﻿using SensorbergSDK;
using SensorbergSDK.Internal;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using MetroLog;
using SensorbergSDK.Internal.Data;
using SensorbergSDK.Internal.Services;

namespace SensorbergSDKBackground
{
    /// <summary>
    /// BackgroundEngine resolves actions from BluetoothLEAdvertisementWatcherTriggerDetails object
    /// and resolves delayed actions. This is not part of the public API. Making modifications into
    /// background tasks is not required in order to use the SDK.
    /// </summary>
    public class BackgroundEngine : IDisposable
    {
        private static readonly ILogger logger = LogManagerFactory.DefaultLogManager.GetLogger<BackgroundEngine>();

        public event EventHandler<BackgroundWorkerType> Finished;

        private SDKEngine SdkEngine { get; }
        private IList<Beacon> Beacons { get; set; }
        private readonly IList<BeaconEventArgs> _beaconArgs;
        private AppSettings AppSettings { get; set; }

        public event EventHandler<BeaconAction> BeaconActionResolved
        {
            add { SdkEngine.BeaconActionResolved += value; }
            remove { SdkEngine.BeaconActionResolved -= value; }
        }

        public event EventHandler<string> FailedToResolveBeaconAction
        {
            add { SdkEngine.FailedToResolveBeaconAction += value; }
            remove { SdkEngine.FailedToResolveBeaconAction -= value; }
        }

        public event EventHandler<bool> LayoutValidityChanged
        {
            add { SdkEngine.LayoutValidityChanged += value; }
            remove { SdkEngine.LayoutValidityChanged -= value; }
        }

        public BackgroundEngine()
        {
            SdkEngine = new SDKEngine(false);
            _beaconArgs = new List<BeaconEventArgs>();
            SdkEngine.BeaconActionResolved += OnBeaconActionResolvedAsync;
        }

        /// <summary>
        /// Initializes BackgroundEngine
        /// </summary>
        public async Task InitializeAsync()
        {
            await SdkEngine.InitializeAsync();
            AppSettings = await ServiceManager.SettingsManager.GetSettings();

            //TODO verfiy
            if (BackgroundTaskManager.CheckIfBackgroundFilterUpdateIsRequired())
            {
                ToastNotification toastNotification = NotificationUtils.CreateToastNotification("New beacon signature available", "Launch the application to update");
                NotificationUtils.ShowToastNotification(toastNotification);
            }
        }

        /// <summary>
        /// Resolves beacons, which triggered the background task.
        /// </summary>
        public async Task ResolveBeaconActionsAsync(List<Beacon> beacons, int outOfRangeDb)
        {
            logger.Trace("ResolveBeaconActionsAsync");

            Beacons = beacons;
            if (Beacons.Count > 0)
            {
                await AddBeaconsToBeaconArgsAsync(outOfRangeDb);
            }

            if (_beaconArgs.Count > 0)
            {
                // Resolve new events
                foreach (var beaconArg in _beaconArgs)
                {
                    await SdkEngine.ResolveBeaconAction(beaconArg);
                }
            }
            Finished?.Invoke(this, BackgroundWorkerType.ADVERTISEMENT_WORKER);
        }

        /// <summary>
        /// Processes the delayed actions, executes them as necessary and sends history statistics.
        /// </summary>
        public async Task ProcessDelayedActionsAsync()
        {
            await SdkEngine.ProcessDelayedActionsAsync();
            await SdkEngine.FlushHistory();
            Finished?.Invoke(this, BackgroundWorkerType.TIMED_WORKER);
        }


        /// <summary>
        /// Generates BeaconArgs from beacon events.
        /// For instance if a beacon is seen for the first time, BeaconArgs with enter type is generated
        /// </summary>
        private async Task AddBeaconsToBeaconArgsAsync(int outOfRangeDb)
        {
            logger.Trace("AddBeaconsToBeaconArgsAsync");
            foreach (var beacon in Beacons)
            {
                BackgroundEvent history = await ServiceManager.StorageService.GetLastEventStateForBeacon(beacon.Pid);

                if (history == null || history.LastEvent == BeaconEventType.Exit ||
                    (!IsOutOfRange(outOfRangeDb, beacon) && history.EventTime.AddMilliseconds(AppSettings.BeaconExitTimeout) < DateTimeOffset.Now))
                {
                    // No history for this beacon. Let's save it and add it to event args array for solving.
                    AddBeaconArgs(beacon, BeaconEventType.Enter);
                    await ServiceManager.StorageService.SaveBeaconEventState(beacon.Pid, BeaconEventType.Enter);
                }
                else if (history.LastEvent == BeaconEventType.Enter)
                {
                    if (IsOutOfRange(outOfRangeDb, beacon))
                    {
                        // Exit event
                        AddBeaconArgs(beacon, BeaconEventType.Exit);
                        await ServiceManager.StorageService.SaveBeaconEventState(beacon.Pid, BeaconEventType.Exit);
                    }
                }
            }
        }

        private static bool IsOutOfRange(int outOfRangeDb, Beacon beacon)
        {
            return beacon.RawSignalStrengthInDBm == outOfRangeDb;
        }

        private void AddBeaconArgs(Beacon beacon, BeaconEventType eventType)
        {
            var args = new BeaconEventArgs();
            args.Beacon = beacon;
            args.EventType = eventType;
            _beaconArgs.Add(args);
        }

        /// <summary>
        /// Handles BeaconActions that are resolved in the SDKEngine.
        /// All resolved actions are stored into local database. And the UI app will show actions to the user.
        /// When the UI app is not running, toast notification is shown for the user.
        /// </summary>
        private void OnBeaconActionResolvedAsync(object sender, BeaconAction beaconAction)
        {
            logger.Trace("BackgroundEngine.OnBeaconActionResolvedAsync()");
        }

        /// <summary>
        /// Finishes background processing and releases all resources
        /// </summary>

        public void Dispose()
        {
            try
            {
                SdkEngine.BeaconActionResolved -= OnBeaconActionResolvedAsync;
            }
            finally
            {
                SdkEngine.Dispose();
            }
        }
    }
}