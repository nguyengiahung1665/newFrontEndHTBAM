from datetime import datetime
from pydantic import BaseModel, Field, HttpUrl

class StartJobRequest(BaseModel):
    sessionId: int
    sourceType: str
    source: str
    correlationId: str
    rosterStudentIds: list[int]
    alertProfile: str = "DEFAULT"
    callbackUrl: str
    callbackApiKey: str

class StartJobResponse(BaseModel):
    jobId: str
    status: str
    modelVersion: str

class StopJobResponse(BaseModel):
    jobId: str
    status: str

class CapabilitiesResponse(BaseModel):
    sessionInference: bool
    faceEnrollment: bool
    loadedModels: list[str]

class FaceEnrollmentManifestImage(BaseModel):
    imageId: int
    capturePose: str
    contentType: str
    readUrl: str

class FaceEnrollmentManifest(BaseModel):
    enrollmentId: int
    studentId: int
    studentCode: str
    images: list[FaceEnrollmentManifestImage]
    templateObjectKey: str
    templateUploadUrl: str
    callbackUrl: str
    callbackApiKey: str

class StartFaceEnrollmentResponse(BaseModel):
    jobId: str
    status: str

class AiEvent(BaseModel):
    eventId: str
    sessionId: int
    eventType: str
    timestamp: datetime
    stableId: str
    trackId: str | None = None
    studentId: int | None = None
    identityConfidence: float | None = None
    identityMargin: float | None = None
    behaviorLabel: str | None = None
    behaviorProbability: float | None = None
    observationQuality: float = Field(ge=0, le=1)
    alertType: str | None = None
    alertDurationSeconds: int | None = None
    modelVersion: str = "stub-contract-v2"
    thresholdVersion: str = "stub-threshold-v2"
    startedAt: datetime | None = None
    endedAt: datetime | None = None
    identityDecision: str | None = None
