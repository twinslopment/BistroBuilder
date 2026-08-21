using System;
using System.Collections.Generic;

namespace BistroBuilder.LivingArchitecture.Domain
{
    /// <summary>
    /// Suite acumulativa pura de Arquitectura Viva V1. LA1 conserva su runner Editor histórico;
    /// LA2-LA11 se agregan aquí para detectar regresiones cruzadas con una sola ejecución.
    /// </summary>
    public static class ArchitectureV1SelfTestSuite
    {
        public const int ExpectedCaseCount = 107;

        public static IReadOnlyList<string> Run()
        {
            var failures = new List<string>();
            Append(failures, "LA2", ArchitectureRegionSelfTest.Run());
            Append(failures, "LA3", ArchitectureOperationSelfTest.Run());
            Append(failures, "LA4", ArchitectureIntentSelfTest.Run());
            Append(failures, "LA5", ArchitectureSnapSelfTest.Run());
            Append(failures, "LA6", ArchitectureImpactSelfTest.Run());
            Append(failures, "LA7", ArchitectureMesherSelfTest.Run());
            Append(failures, "LA8", ArchitecturePersistenceSelfTest.Run());
            Append(failures, "LA9", ArchitectureEditSessionSelfTest.Run());
            Append(failures, "LA10", ArchitectureEditFeedbackSelfTest.Run());
            Append(failures, "LA11", ArchitectureV1QueenSelfTest.Run());
            return failures;
        }

        private static void Append(ICollection<string> target, string milestone, IReadOnlyList<string> source)
        {
            if (source == null)
            {
                target.Add(milestone + ": self-test returned null");
                return;
            }

            for (var i = 0; i < source.Count; i++)
                target.Add(milestone + "/" + source[i]);
        }
    }
}
