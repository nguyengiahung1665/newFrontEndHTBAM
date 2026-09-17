from pathlib import Path
root=Path(__file__).resolve().parents[1]
backend=(root/'services/backend/src/HTBAM.Application/DTOs/Contracts.cs').read_text()
ai=(root/'services/ai/app/models.py').read_text()
for field in ['SessionId','CorrelationId','RosterStudentIds','CallbackUrl','CallbackApiKey']:
    assert field in backend, field
for field in ['sessionId','correlationId','rosterStudentIds','callbackUrl','callbackApiKey']:
    assert field in ai, field
for field in ['EventId','StableId','StudentId','IdentityConfidence','BehaviorLabel','ObservationQuality','ModelVersion','ThresholdVersion','StartedAt','EndedAt','IdentityDecision']:
    assert field in backend, field
for field in ['eventId','stableId','studentId','identityConfidence','behaviorLabel','observationQuality','modelVersion','thresholdVersion','startedAt','endedAt','identityDecision']:
    assert field in ai, field
for field in ['TemplateObjectKey','TemplateUploadUrl']:
    assert field in backend, field
for field in ['templateObjectKey','templateUploadUrl']:
    assert field in ai, field
print('CONTRACT_OK')
