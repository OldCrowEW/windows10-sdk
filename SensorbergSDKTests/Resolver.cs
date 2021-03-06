﻿using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using SensorbergSDK;
using SensorbergSDK.Internal;
using SensorbergSDK.Internal.Data;
using SensorbergSDK.Internal.Services;
using SensorbergSDK.Services;
using SensorbergSDKTests.Mocks;

namespace SensorbergSDKTests
{
    [TestClass]
    public class UnitTestResolver
    {
        ManualResetEvent _manualEvent = new ManualResetEvent(false);
        Beacon beacon = new Beacon();
        Resolver res = new Resolver(false);
        BeaconEventArgs args = new BeaconEventArgs();
        ResolvedActionsEventArgs _e = null;

        [TestInitialize]
        public async Task TestSetup()
        {
            await TestHelper.Clear();
            ServiceManager.ReadOnlyForTests = false;
            ServiceManager.Clear();
            ServiceManager.LayoutManager = new MockLayoutManager() {FindOneAction = true};
            ServiceManager.SettingsManager = new SettingsManager();
            ServiceManager.StorageService = new StorageService() {Storage = new MockStorage()};
            ServiceManager.LocationService = new LocationService();
            ServiceManager.ReadOnlyForTests = true;
        }

        [TestMethod]
        public async Task resolver_test()
        {
            beacon.Id1 = "7367672374000000ffff0000ffff0006";
            beacon.Id2 = 59242;
            beacon.Id3 = 27189;

            args.Beacon = beacon;
            args.EventType = BeaconEventType.Unknown;
            res.ActionsResolved += Res_ActionResolved;
            await res.CreateRequest(args);
            _manualEvent.WaitOne();

            Assert.IsNotNull(_e);
            Assert.IsNotNull(_e.ResolvedActions);
            Assert.IsTrue(_e.ResolvedActions.Count == 1);
        }

        private void Res_ActionResolved(object sender, ResolvedActionsEventArgs e)
        {
            _e = e;
            _manualEvent.Set();
        }
    }
}
