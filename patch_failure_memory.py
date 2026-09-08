from pathlib import Path
p=Path(r'C:\Users\mruperez\ProyectoBB\BistroBuilder\Assets\Scripts\Application\Navigation\BistroBuilderNavigationRuntimeV1Closure.cs')
s=p.read_text(encoding='utf-8')
def rep(a,b):
    global s
    if a not in s: raise SystemExit('missing: '+a[:70])
    s=s.replace(a,b,1)
rep('        Vector3 current = trip.lastPosition;','''        string recoverySignature = string.IsNullOrWhiteSpace(signature) ? "generic" : signature;
        if (v1FailureMemory.IsSuppressed(ownerId, stage, recoverySignature, now))
        {
            trip.trace.lastDecision = "Recovery suppressed by Failure Memory: " + stage + ".";
            return false;
        }

        Vector3 current = trip.lastPosition;''')
rep('        if (!found) return false;','''        if (!found)
        {
            v1FailureMemory.RecordFailure(ownerId, stage, recoverySignature, now);
            return false;
        }''')
s=s.replace('            signature = signature ?? string.Empty','            signature = recoverySignature',1)
rep('            v1BlockGraph?.ClearDependency(ownerId);\n            RecordReplayV1("recovery.complete", ownerId, current, maneuver.stage.ToString());','            v1BlockGraph?.ClearDependency(ownerId);\n            v1FailureMemory.RecordSuccess(ownerId, maneuver.stage, maneuver.signature);\n            RecordReplayV1("recovery.complete", ownerId, current, maneuver.stage.ToString());')
rep('            v1RecoveryManeuvers.Remove(ownerId);\n            RecordReplayV1("recovery.blocked", ownerId, current, maneuver.stage.ToString());','            v1RecoveryManeuvers.Remove(ownerId);\n            v1FailureMemory.RecordFailure(ownerId, maneuver.stage, maneuver.signature, now);\n            RecordReplayV1("recovery.blocked", ownerId, current, maneuver.stage.ToString());')
rep('        for (int i = 0; i < v1ScratchIds.Count; i++)\n            v1RecoveryManeuvers.Remove(v1ScratchIds[i]);','        for (int i = 0; i < v1ScratchIds.Count; i++)\n            v1RecoveryManeuvers.Remove(v1ScratchIds[i]);\n        v1FailureMemory?.Cleanup(now);')
p.write_text(s,encoding='utf-8')
print('FAILURE_MEMORY_INTEGRATED')