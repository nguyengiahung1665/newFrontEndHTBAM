from pathlib import Path
root=Path(__file__).resolve().parents[1]
def text(p): return (root/p).read_text()
face=text('services/backend/src/HTBAM.Application/Services/FaceEnrollmentService.cs')
session=text('services/backend/src/HTBAM.Application/Services/SessionServices.cs')
events=text('services/backend/src/HTBAM.Application/Services/AiEventService.cs')
storage=text('services/backend/src/HTBAM.Infrastructure/Services/MinioObjectStorage.cs')
program=text('services/backend/src/HTBAM.Api/Program.cs')
web=text('apps/web/src/App.tsx')
assert 'SHA256.HashData' in face and 'DetectImageType' in face
assert 'PENDING_AI' in face and 'NEEDS_RETAKE' in face
assert 'GetInternalWriteUrlAsync' in face and 'face-templates/' in face and 'ExistsAsync(expectedTemplateRef' in face
assert 'FINALIZE_FAILED' in session and 'RetryFinalizeAsync' in session
assert 'UnionSeconds' in session and 'PresentThreshold' in session and 'PartialThreshold' in session and 'PHONE_USE là tín hiệu/rule phụ' in session
assert 'CapabilitiesAsync' in session and 'SessionInference' in session
assert 'AiEventReceipts' in events and 'OUT_OF_ROSTER' in events and 'DUPLICATE_CLAIM' in events and 'CONFLICT' in events and 'IDENTITY_CORRECTION' in events and 'Behavior state segment bị chồng lấn' in events
assert 'AlertEngine' in text('services/backend/src/HTBAM.Application/Services/SupportServices.cs')
assert 'PresignedGetObjectAsync' in storage and 'PresignedPutObjectAsync' in storage and 'StatObjectAsync' in storage
assert 'AddSingleton<IObjectStorage' in program and 'AddRateLimiter' in program and 'ApiExceptionMiddleware' in program
for route in ['face-enrollment','sessions/:id','videos','management','reports','history','policies','admin/users','admin/audit']:
    assert route in web, route
assert 'PresenceRatio=presentSeconds==0?0:1' not in session.replace(' ','')
db=text('services/backend/src/HTBAM.Infrastructure/Data/AppDbContext.cs')
for table in ['Users','Students','FaceEnrollments','StudentFaceImages','StudentFaceTemplates','Sessions','StableIdentities','BehaviorEvents','Attendance']:
    assert f'="{table}"' in db or f'ToTable("{table}")' in db, table
print('WORKFLOW_OK')
