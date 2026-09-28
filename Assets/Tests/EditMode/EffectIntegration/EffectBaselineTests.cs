using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;

namespace REmind.Effects.Tests
{
    // Unity's EditMode runner is sequential; the isolated history check also guards live editor state.
    public sealed class EffectBaselineTests
    {
        [TestCase("LegacyV7_AllNoteFamilies")]
        [TestCase("ChartJacket_RoundTripAndRejectsNested")]
        [TestCase("LongScratchAdapter_PreservesIntermediatePoint")]
        [TestCase("LegacyEffect_RemainsUnresolved")]
        [TestCase("UnclosedLongSave_DoesNotMutateSource")]
        [TestCase("EffectPair_RoundTripAndBackupRecovery")]
        [TestCase("EffectPair_ProtectsBackupOnlyOwnership")]
        [TestCase("InlineEffect_RoundTripInOneChartFile")]
        [TestCase("EffectPair_FirstSaveFailureRollsBackForRetry")]
        [TestCase("EffectPair_MidSaveFailureRecoversAndResaves")]
        [TestCase("EffectPair_CorruptCurrentFailureKeepsMatchingBackup")]
        [TestCase("FileLoader_RecoversMissingOrCorruptCurrentAndStaysDirty")]
        [TestCase("EffectPair_InvalidCurrentSettingsRecoverOrLeaveEditorUntouched")]
        [TestCase("ParameterTypes_RejectMalformedValues")]
        [TestCase("ParameterSave_ValidatesKnownAndPreservesUnresolved")]
        [TestCase("Judgement_DelayedInputBeatsFrameMiss")]
        [TestCase("Judgement_InputDeliveredAfterPreviousFrame")]
        [TestCase("Judgement_OffsetsAndExactMissBoundary")]
        [TestCase("Judgement_DirectAndIndirectWindows")]
        [TestCase("Judgement_EffectBeforeSameTimeInput")]
        [TestCase("Judgement_DelayedFrameUsesActualTimeAndTotalOrder")]
        [TestCase("Judgement_RuleChangeIsNotRetroactive")]
        [TestCase("Judgement_RuleIntervalBoundariesAreExact")]
        [TestCase("Judgement_TimelineFailureStillCompletesFrame")]
        [TestCase("AutoPlay_ChronologicalAndResettable")]
        [TestCase("EffectServices_RejectStaleCameraAndDuplicateTransition")]
        [TestCase("Gameplay_ResumeKeepsSessionRestartReplacesIt")]
        [TestCase("Gameplay_CommitRejectionBlocksSchedulingAndReentrancy")]
        [TestCase("ChartPreview_StartStopTransactionsAreReentrancySafe")]
        [TestCase("ChartPreview_ReentrantEffectStopDoesNotUseClearedSession")]
        [TestCase("ChartPreview_UnresolvedEffectUsesSetupGuidance")]
        [Category("EffectBaseline")]
        public void Integration(string name) => Run(name);

        [TestCase("SaveAsExisting_ProtectsOtherSidecar")]
        [TestCase("UndoRedo_PreserveLastSavedRevision")]
        [TestCase("EditorAndRuntimeValidation_Agree")]
        [TestCase("ChartMaker_SaveReopenPreview_RoundTrips")]
        [TestCase("ChartMaker_CompositeChartRoundTripsToGame")]
        [TestCase("GameplayPreparation_UsesSharedPairAndOffset")]
        [TestCase("GameplayChartSession_FailedReplacementInvalidatesAll")]
        [TestCase("GameplayChartSession_DisabledServiceRejectsStart")]
        [TestCase("GameplaySessionState_UsesRuleStateAndRestartBoundary")]
        [TestCase("GameplayScene_ConnectsSnapshotStateAndCamera")]
        [TestCase("GameScene_WiresFullSongAndFlow")]
        [Category("EffectAcceptance")]
        public void Acceptance(string name) => Run(name);

        private static void Run(string name)
        {
            Type bridge = Type.GetType("REmindBaselineChecks, Assembly-CSharp-Editor", true);
            try { bridge.GetMethod("Run", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { name }); }
            catch (TargetInvocationException exception)
            { ExceptionDispatchInfo.Capture(exception.InnerException ?? exception).Throw(); throw; }
        }
    }
}
