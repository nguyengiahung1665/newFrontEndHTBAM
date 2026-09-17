from abc import ABC, abstractmethod
from dataclasses import dataclass
from uuid import uuid4
from .models import StartJobRequest

@dataclass
class Job:
    id: str
    request: StartJobRequest
    status: str = "RUNNING"

class InferenceEngine(ABC):
    @abstractmethod
    async def start(self, request: StartJobRequest) -> Job: ...
    @abstractmethod
    async def stop(self, job_id: str) -> Job: ...
    @abstractmethod
    async def health(self) -> dict: ...

class StubInferenceEngine(InferenceEngine):
    """Không giả vờ là model thật. Chỉ giữ contract/lifecycle để test web-backend-AI."""
    def __init__(self): self.jobs: dict[str, Job] = {}
    async def start(self, request: StartJobRequest) -> Job:
        job = Job(id=f"stub-{uuid4().hex}", request=request)
        self.jobs[job.id] = job
        return job
    async def stop(self, job_id: str) -> Job:
        if job_id not in self.jobs: raise KeyError(job_id)
        self.jobs[job_id].status = "COMPLETED"
        return self.jobs[job_id]
    async def health(self) -> dict:
        return {"status":"ok","mode":"STUB","loadedModels":[],"runningJobs":sum(j.status=="RUNNING" for j in self.jobs.values())}
