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
        public void ParentRegisteredAfterChildStillAggregatesChildUnits()
        {
            const string parentRedDotName = "TEST_LATE_PARENT";
            const string childRedDotName = parentRedDotName + "|CHILD";
            const string redDotUnit = "TEST_LATE_PARENT_UNIT";

            var (childAdded, _) = RedDotModel.Singleton.AddRedDotInfo(
                childRedDotName,
                "Child registered before parent");
            Assert.IsTrue(childAdded);

            var (unitAdded, _) = RedDotModel.Singleton.AddRedDotUnitInfo(
                redDotUnit,
                "Late parent test unit",
                () => 4,
                RedDotType.NUMBER);
            Assert.IsTrue(unitAdded);

            var childInfo = RedDotModel.Singleton.GetRedDotInfoByName(childRedDotName);
            Assert.IsTrue(childInfo.AddRedDotUnit(redDotUnit));

            var (parentAdded, parentInfo) = RedDotModel.Singleton.AddRedDotInfo(
                parentRedDotName,
                "Parent registered after child");
            Assert.IsTrue(parentAdded);
            Assert.IsNotNull(parentInfo);

            RedDotManager.Singleton.EvaluateAllDirtyNow();

            CollectionAssert.Contains(parentInfo.RedDotUnitList, redDotUnit);
            Assert.AreEqual(4, RedDotManager.Singleton.GetRedDotResult(childRedDotName));
            Assert.AreEqual(4, RedDotManager.Singleton.GetRedDotResult(parentRedDotName));
        }

        [Test]
        public void RegistrationDefersParentResolutionUntilComplete()
        {
            const string parentRedDotName = "TEST_BATCH_PARENT";
            const string childRedDotName = parentRedDotName + "|CHILD";
            const string redDotUnit = "TEST_BATCH_PARENT_UNIT";

            RedDotInitializer.Singleton.BeginRegistration();
            Assert.IsTrue(RedDotModel.Singleton.AddRedDotInfo(
                childRedDotName,
                "Child added during registration batch").Item1);
            Assert.IsTrue(RedDotModel.Singleton.AddRedDotUnitInfo(
                redDotUnit,
                "Registration batch test unit",
                () => 6,
                RedDotType.NUMBER).Item1);

            var childInfo = RedDotModel.Singleton.GetRedDotInfoByName(childRedDotName);
            Assert.IsTrue(childInfo.AddRedDotUnit(redDotUnit));
            Assert.IsTrue(RedDotModel.Singleton.AddRedDotInfo(
                parentRedDotName,
                "Parent added during registration batch").Item1);

            var parentInfo = RedDotModel.Singleton.GetRedDotInfoByName(parentRedDotName);
            CollectionAssert.DoesNotContain(parentInfo.RedDotUnitList, redDotUnit);

            RedDotInitializer.Singleton.CompleteRegistration();

            CollectionAssert.Contains(childInfo.RedDotUnitList, redDotUnit);
            CollectionAssert.Contains(parentInfo.RedDotUnitList, redDotUnit);
            RedDotManager.Singleton.EvaluateAllDirtyNow();
            Assert.AreEqual(6, RedDotManager.Singleton.GetRedDotResult(parentRedDotName));
        }

        [Test]
        public void SharedUnitRemainsOnParentUntilLastChildUnbinds()
        {
            const string parentRedDotName = "TEST_SHARED_PARENT";
            const string childRedDotName = parentRedDotName + "|CHILD";
            const string redDotUnit = "TEST_SHARED_PARENT_UNIT";

            Assert.IsTrue(RedDotModel.Singleton.AddRedDotInfo(
                parentRedDotName,
                "Shared parent").Item1);
            Assert.IsTrue(RedDotModel.Singleton.AddRedDotInfo(
                childRedDotName,
                "First shared child").Item1);
            Assert.IsTrue(RedDotModel.Singleton.AddRedDotInfo(
                parentRedDotName + "|OTHER_CHILD",
                "Second shared child").Item1);

            Assert.IsTrue(RedDotModel.Singleton.AddRedDotUnitInfo(
                redDotUnit,
                "Shared parent test unit",
                () => 5,
                RedDotType.NUMBER).Item1);

            var firstChildInfo = RedDotModel.Singleton.GetRedDotInfoByName(childRedDotName);
            var secondChildInfo = RedDotModel.Singleton.GetRedDotInfoByName(
                parentRedDotName + "|OTHER_CHILD");
            var parentInfo = RedDotModel.Singleton.GetRedDotInfoByName(parentRedDotName);
            Assert.IsTrue(firstChildInfo.AddRedDotUnit(redDotUnit));
            Assert.IsTrue(secondChildInfo.AddRedDotUnit(redDotUnit));

            RedDotManager.Singleton.EvaluateAllDirtyNow();
            CollectionAssert.Contains(parentInfo.RedDotUnitList, redDotUnit);
            Assert.AreEqual(5, RedDotManager.Singleton.GetRedDotResult(parentRedDotName));

            Assert.IsTrue(firstChildInfo.RemoveRedDotUnit(redDotUnit));
            CollectionAssert.Contains(parentInfo.RedDotUnitList, redDotUnit);
            Assert.AreEqual(5, RedDotManager.Singleton.GetRedDotResult(parentRedDotName));

            Assert.IsTrue(secondChildInfo.RemoveRedDotUnit(redDotUnit));
            CollectionAssert.DoesNotContain(parentInfo.RedDotUnitList, redDotUnit);
            Assert.AreEqual(0, RedDotManager.Singleton.GetRedDotResult(parentRedDotName));
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
