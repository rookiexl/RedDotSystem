using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.RedDot.Tests
{
    public class RedDotRuntimeTests
    {
        [SetUp]
        public void SetUp()
        {
            RedDotInitializer.Singleton.Dispose();
            RedDotManager.Singleton.Dispose();
            RedDotModel.Singleton.Dispose();
            RedDotModel.Singleton.Init();
        }

        [TearDown]
        public void TearDown()
        {
            RedDotInitializer.Singleton.Dispose();
            RedDotManager.Singleton.Dispose();
            RedDotModel.Singleton.Dispose();
        }

        [Test]
        public void SubscribeBeforeNodeCreationReceivesInitialResult()
        {
            const string redDotName = "TEST_ROOT|DYNAMIC_ENTRY";
            const string redDotUnit = "TEST_DYNAMIC_UNIT";
            var callbackCount = 0;
            var lastSnapshot = RedDotSnapshot.Empty;

            using (RedDotManager.Singleton.Subscribe(
                       redDotName,
                       snapshot =>
                       {
                           callbackCount++;
                           lastSnapshot = snapshot;
                       }))
            {
                RedDotModel.Singleton.AddRedDotInfo(redDotName, "Dynamic test node");
                RedDotModel.Singleton.AddRedDotUnitInfo(
                    redDotUnit,
                    "Dynamic test unit",
                    () => 3,
                    RedDotType.NUMBER);
                RedDotModel.Singleton.GetRedDotInfoByName(redDotName)
                    .AddRedDotUnit(redDotUnit);

                AdvanceManagerFrames();

                Assert.GreaterOrEqual(callbackCount, 1);
                Assert.AreEqual(3, lastSnapshot.Result);
                Assert.IsTrue(lastSnapshot.IsVisible);
            }
        }

        [Test]
        public void DisposedSubscriptionStopsReceivingUpdates()
        {
            const string redDotName = "TEST_ROOT|DISPOSED_ENTRY";
            const string redDotUnit = "TEST_DISPOSED_UNIT";
            var callbackCount = 0;

            RedDotModel.Singleton.AddRedDotInfo(redDotName, "Disposed test node");
            RedDotModel.Singleton.AddRedDotUnitInfo(
                redDotUnit,
                "Disposed test unit",
                () => 1,
                RedDotType.RED);
            RedDotModel.Singleton.GetRedDotInfoByName(redDotName)
                .AddRedDotUnit(redDotUnit);

            var subscription = RedDotManager.Singleton.Subscribe(
                redDotName,
                _ => callbackCount++);
            subscription.Dispose();
            subscription.Dispose();

            RedDotManager.Singleton.ForceRefreshRedDotName(redDotName);
            AdvanceManagerFrames();

            Assert.AreEqual(0, callbackCount);
        }

        [Test]
        public void EvaluateAllDirtyNowProducesInitialSnapshotImmediately()
        {
            const string redDotName = "TEST_ROOT|STARTUP_ENTRY";
            const string redDotUnit = "TEST_STARTUP_UNIT";
            var callbackCount = 0;
            var lastSnapshot = RedDotSnapshot.Empty;

            RedDotModel.Singleton.AddRedDotInfo(redDotName, "Startup test node");
            RedDotModel.Singleton.AddRedDotUnitInfo(
                redDotUnit,
                "Startup test unit",
                () => 2,
                RedDotType.NUMBER);
            RedDotModel.Singleton.GetRedDotInfoByName(redDotName)
                .AddRedDotUnit(redDotUnit);

            using (RedDotManager.Singleton.Subscribe(
                       redDotName,
                       snapshot =>
                       {
                           callbackCount++;
                           lastSnapshot = snapshot;
                       }))
            {
                RedDotManager.Singleton.EvaluateAllDirtyNow();
            }

            Assert.GreaterOrEqual(callbackCount, 1);
            Assert.AreEqual(2, lastSnapshot.Result);
        }

        [Test]
        public void InitializerSupportsModuleRegistrationPhases()
        {
            var initializer = RedDotInitializer.Singleton;
            var feature = new TestFeatureInitializer();

            initializer.BeginRegistration();
            Assert.IsTrue(initializer.IsRegistrationOpen);
            Assert.IsTrue(initializer.Register(feature));
            initializer.CompleteRegistration();

            Assert.IsFalse(initializer.IsRegistrationOpen);
            Assert.IsTrue(initializer.IsInitComplete);
            Assert.AreSame(feature, initializer.RegisteredInitializers[0]);
            Assert.IsTrue(feature.IsActive);
        }

        [Test]
        public void RuntimeAssemblyHasNoBusinessDependencies()
        {
            var runtimeDirectory = Path.Combine(
                Application.dataPath,
                "RedDot",
                "Runtime");
            var asmdefPath = Path.Combine(
                runtimeDirectory,
                "RedDot.Runtime.asmdef");

            Assert.IsTrue(File.Exists(asmdefPath));
            var asmdef = JsonUtility.FromJson<AssemblyDefinitionData>(
                File.ReadAllText(asmdefPath));
            Assert.IsNotNull(asmdef);
            Assert.AreEqual(0, asmdef.references.Length);

            var forbiddenTokens = new[]
            {
                "SystemUnlockModel",
                "SystemType",
                "MainUIRDInitializer",
                "BackpackRDInitializer",
                "RedDotNames.",
                "RedDotUnit.",
                "EventId",
                "ObjectPool",
                "SingletonTemplate",
                "BaseModel",
                "using Game.Core",
                "Game.Business"
            };
            var runtimeSource = string.Empty;

            foreach (var file in Directory.GetFiles(
                         runtimeDirectory,
                         "*.cs",
                         SearchOption.AllDirectories))
            {
                runtimeSource += File.ReadAllText(file);
            }

            foreach (var forbiddenToken in forbiddenTokens)
            {
                Assert.IsFalse(
                    runtimeSource.Contains(forbiddenToken, StringComparison.Ordinal),
                    $"RedDot.Runtime must not contain business dependency: {forbiddenToken}");
            }
        }

        private static void AdvanceManagerFrames()
        {
            for (var frame = 0; frame < 12; frame++)
            {
                RedDotManager.Singleton.Update();
            }
        }

        private sealed class TestFeatureInitializer : FuncRDInitializer
        {
            protected override void InitRedDotInfos()
            {
            }
        }

        [Serializable]
        private sealed class AssemblyDefinitionData
        {
            public string[] references;
        }
    }
}
