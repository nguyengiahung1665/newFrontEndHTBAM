# Tích hợp model sau khi train xong

Hiện `services/ai` cố ý là contract-only: `sessionInference=false`, `faceEnrollment=false` và không tạo kết quả AI giả. Web/Backend/DB có thể kiểm thử cấu hình/orchestration đến bước capability gate; Start inference thật chỉ mở khi model production load thành công.

## Cấu trúc khuyến nghị khi model hoàn tất
```text
services/ai/
  app/
    detection/          # YOLO
    tracking/           # BoT-SORT
    identity/
      arcface.py
      osnet.py
      stable_identity_resolver.py
      session_gallery.py
    features/           # 14 low-level features
    htbam/
      model.py
      inference.py
      smoothing.py
    clustering/        # KMeans/Intelligent KMeans (research/training/offline)
    weak_labeling/      # pseudo-label pipeline
    events/
      event_engine.py
    inference/          # ProductionInferenceEngine/orchestrator
  models/               # checkpoint mounted/ignored from Git as needed
```

## Face Enrollment
1. `/capabilities` đổi `faceEnrollment=true` khi ArcFace/quality pipeline load thành công.
2. `/face-enrollments/start` nhận manifest có presigned GET cho ảnh và presigned PUT cho template.
3. AI đọc ảnh -> detect/align/quality -> ArcFace -> aggregate.
4. AI PUT template vào `templateUploadUrl` trước.
5. AI callback Backend với quality mỗi ảnh, `TemplateRef`, model/version/dimension/pose coverage.
6. Backend tự kiểm tra object tồn tại trước READY.

## Classroom runtime
1. `/jobs/start` nhận source CAMERA/VIDEO, roster candidate, callback URL/API key.
2. Per frame: YOLO -> tracker -> face/Re-ID -> Stable Identity Resolver.
3. `StudentId` có thể null; không ép nearest student khi score/margin chưa đủ.
4. Per stable_id: feature buffer riêng -> 30x14 -> HTBAM -> smoothing -> BehaviorEvent.
5. Event callback phải có unique `eventId`, stableId, timestamps, quality, modelVersion, thresholdVersion.
6. `/jobs/{id}/stop` phải flush/close BehaviorEvent đang mở trước khi trả COMPLETED; endpoint cần idempotent để Backend retry finalize an toàn.

Đổi checkpoint/model không cần migration DB nếu contract trên không đổi.
