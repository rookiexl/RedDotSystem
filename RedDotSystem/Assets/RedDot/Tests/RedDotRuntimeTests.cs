using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

            Assert.IsNotNull(RedDotInitializer.Singleton.LastValidationReport);
            Assert.IsFalse(RedDotInitializer.Singleton.HasValidationErrors);
            CollectionAssert.Contains(childInfo.RedDotUnitList, redDotUnit);
            CollectionAssert.Contains(parentInfo.RedDotUnitList, redDotUnit);
            RedDotManager.Singleton.EvaluateAllDirtyNow();
            Assert.AreEqual(6, RedDotManager.Singleton.GetRedDotResult(parentRedDotName));
        }

        [Test]
        public void AutomaticValidationCanBeDisabled()
        {
            var initializer = RedDotInitializer.Singleton;
            initializer.EnableAutomaticValidation = false;
            initializer.BeginRegistration();
            Assert.IsTrue(RedDotModel.Singleton.AddRedDotInfo(
                "TEST_DISABLED_VALIDATION_PARENT|CHILD",
                "Automatic validation disabled test").Item1);

            initializer.CompleteRegistration();

            Assert.IsNull(initializer.LastValidationReport);
            Assert.IsFalse(initializer.HasValidationErrors);
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
        public void ThrowingCalculationPreservesLastValueAndDoesNotBlockBatch()
        {
            const string redDotName = "TEST_THROWING_CALCULATION";
            const string firstUnit = "TEST_THROWING_CALCULATION_FIRST";
            const string secondUnit = "TEST_THROWING_CALCULATION_SECOND";
            var shouldThrow = false;
            var secondValue = 1;

            Assert.IsTrue(RedDotModel.Singleton.AddRedDotInfo(
                redDotName,
                "Throwing calculation test").Item1);
            Assert.IsTrue(RedDotModel.Singleton.AddRedDotUnitInfo(
                firstUnit,
                "Throwing calculation first unit",
                () =>
                {
                    if (shouldThrow)
                    {
                        throw new InvalidOperationException("Intentional calculation failure");
                    }
                    return 3;
                },
                RedDotType.NUMBER).Item1);
            Assert.IsTrue(RedDotModel.Singleton.AddRedDotUnitInfo(
                secondUnit,
                "Throwing calculation second unit",
                () => secondValue,
                RedDotType.NUMBER).Item1);

            var redDotInfo = RedDotModel.Singleton.GetRedDotInfoByName(redDotName);
            Assert.IsTrue(redDotInfo.AddRedDotUnit(firstUnit));
            Assert.IsTrue(redDotInfo.AddRedDotUnit(secondUnit));
            RedDotManager.Singleton.EvaluateAllDirtyNow();
            Assert.AreEqual(4, RedDotManager.Singleton.GetRedDotResult(redDotName));

            shouldThrow = true;
            secondValue = 2;
            RedDotManager.Singleton.MarkRedDotUnitDirty(firstUnit);
            RedDotManager.Singleton.MarkRedDotUnitDirty(secondUnit);

            var previousIgnoreValue = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                RedDotManager.Singleton.EvaluateAllDirtyNow();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreValue;
            }

            Assert.AreEqual(5, RedDotManager.Singleton.GetRedDotResult(redDotName));
        }

        [Test]
        public void ThrowingSubscriberDoesNotBlockOtherSubscribers()
        {
            const string redDotName = "TEST_THROWING_SUBSCRIBER";
            const string redDotUnit = "TEST_THROWING_SUBSCRIBER_UNIT";
            var successfulCallbackCount = 0;

            Assert.IsTrue(RedDotModel.Singleton.AddRedDotInfo(
                redDotName,
                "Throwing subscriber test").Item1);
            Assert.IsTrue(RedDotModel.Singleton.AddRedDotUnitInfo(
                redDotUnit,
                "Throwing subscriber test unit",
                () => 1,
                RedDotType.RED).Item1);
            Assert.IsTrue(RedDotModel.Singleton.GetRedDotInfoByName(redDotName)
                .AddRedDotUnit(redDotUnit));

            using (RedDotManager.Singleton.Subscribe(
                       redDotName,
                       _ => throw new InvalidOperationException("Intentional subscriber failure")))
            using (RedDotManager.Singleton.Subscribe(
                       redDotName,
                       _ => successfulCallbackCount++))
            {
                RedDotManager.Singleton.ForceRefreshRedDotName(redDotName);
                var previousIgnoreValue = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                try
                {
                    RedDotManager.Singleton.EvaluateAllDirtyNow();
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = previousIgnoreValue;
                }
            }

            Assert.GreaterOrEqual(successfulCallbackCount, 1);
        }

        [Test]
        public void ValidationReportsMissingParentAndMissingUnitDefinition()
        {
            const string childRedDotName = "TEST_VALIDATION_MISSING_PARENT|CHILD";
            const string missingUnit = "TEST_VALIDATION_MISSING_UNIT";

            Assert.IsTrue(RedDotModel.Singleton.AddRedDotInfo(
                childRedDotName,
                "Validation missing parent test").Item1);
            Assert.IsTrue(RedDotModel.Singleton.GetRedDotInfoByName(childRedDotName)
                .AddRedDotUnit(missingUnit));

            var report = RedDotModel.Singleton.Validate();

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(HasIssue(
                report,
                "RED_DOT_PARENT_MISSING",
                RedDotValidationSeverity.Error));
            Assert.IsTrue(HasIssue(
                report,
                "RED_DOT_UNIT_REFERENCE_MISSING",
                RedDotValidationSeverity.Error));
        }

        [Test]
        public void ValidationReportsNullFunctionAndEmptyLeaf()
        {
            Assert.IsTrue(RedDotModel.Singleton.AddRedDotInfo(
                "TEST_VALIDATION_EMPTY_LEAF",
                "Empty leaf test").Item1);
            Assert.IsTrue(RedDotModel.Singleton.AddRedDotUnitInfo(
                "TEST_VALIDATION_UNUSED_UNIT",
                "Unused unit test",
                null,
                RedDotType.NUMBER).Item1);

            var report = RedDotModel.Singleton.Validate();

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(HasIssue(
                report,
                "RED_DOT_UNIT_FUNC_MISSING",
                RedDotValidationSeverity.Error));
            Assert.IsTrue(HasIssue(
                report,
                "RED_DOT_EMPTY_LEAF",
                RedDotValidationSeverity.Warning));
            Assert.IsTrue(HasIssue(
                report,
                "RED_DOT_UNIT_UNUSED",
                RedDotValidationSeverity.Warning));
        }

        [Test]
        public void DuplicateRegistrationsAppearInValidationReport()
        {
            const string redDotName = "TEST_VALIDATION_DUPLICATE";
            const string redDotUnit = "TEST_VALIDATION_DUPLICATE_UNIT";

            var previousIgnoreValue = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                Assert.IsTrue(RedDotModel.Singleton.AddRedDotInfo(
                    redDotName,
                    "Duplicate node test").Item1);
                Assert.IsFalse(RedDotModel.Singleton.AddRedDotInfo(
                    redDotName,
                    "Duplicate node test").Item1);
                Assert.IsTrue(RedDotModel.Singleton.AddRedDotUnitInfo(
                    redDotUnit,
                    "Duplicate unit test",
                    () => 1,
                    RedDotType.NUMBER).Item1);
                Assert.IsFalse(RedDotModel.Singleton.AddRedDotUnitInfo(
                    redDotUnit,
                    "Duplicate unit test",
                    () => 1,
                    RedDotType.NUMBER).Item1);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreValue;
            }

            var report = RedDotModel.Singleton.Validate();

            Assert.IsTrue(HasIssue(
                report,
                "RED_DOT_DUPLICATE_NODE",
                RedDotValidationSeverity.Error));
            Assert.IsTrue(HasIssue(
                report,
                "RED_DOT_DUPLICATE_UNIT",
                RedDotValidationSeverity.Error));
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

        private static bool HasIssue(
            RedDotValidationReport report,
            string code,
            RedDotValidationSeverity severity)
        {
            foreach (var issue in report.Issues)
            {
                if (issue.Code == code && issue.Severity == severity)
                {
                    return true;
                }
            }
            return false;
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
