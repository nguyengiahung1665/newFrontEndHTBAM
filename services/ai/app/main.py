from fastapi import FastAPI, HTTPException
from .models import StartJobRequest, StartJobResponse, StopJobResponse, CapabilitiesResponse, FaceEnrollmentManifest, StartFaceEnrollmentResponse

app = FastAPI(title="HTBAM AI Service", version="3.0.0-contract")

@app.get("/health")
async def health():
    return {"status": "ok", "mode": "CONTRACT_ONLY", "loadedModels": [], "note": "Production models have not been integrated."}

@app.get("/capabilities", response_model=CapabilitiesResponse)
async def capabilities():
    return CapabilitiesResponse(sessionInference=False, faceEnrollment=False, loadedModels=[])

@app.post("/jobs/start", response_model=StartJobResponse)
async def start_job(req: StartJobRequest):
    raise HTTPException(503, "Session inference models are not available yet")

@app.post("/jobs/{job_id}/stop", response_model=StopJobResponse)
async def stop_job(job_id: str):
    raise HTTPException(503, "No production inference job is running")

@app.post("/face-enrollments/start", response_model=StartFaceEnrollmentResponse)
async def start_face_enrollment(req: FaceEnrollmentManifest):
    raise HTTPException(503, "Face enrollment model is not available yet")
