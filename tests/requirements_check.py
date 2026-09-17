from pathlib import Path
root=Path(__file__).resolve().parents[1]
def t(p): return (root/p).read_text(encoding='utf-8')
app=t('apps/web/src/App.tsx'); schema=t('database/001_schema.sql'); ai=t('services/ai/app/main.py')
controllers='\n'.join(p.read_text(encoding='utf-8') for p in (root/'services/backend/src/HTBAM.Api/Controllers').glob('*.cs'))
required_tables=['Faculties','StudentClasses','Students','Teachers','Courses','ClassSections','Rooms','Cameras','Videos','FaceEnrollments','Sessions','StableIdentities','BehaviorEvents','Alerts','Attendance','AuditLogs']
for x in required_tables: assert f'CREATE TABLE {x}' in schema, x
for route in ['students','management','videos','sessions','alerts','history','reports','search','account']:
    assert f'path="{route}"' in app, route
for marker in ['change-password','logout','api/attendance-policies','import-csv','roster/import-csv','excel','pdf','auto-comment','HttpGet("rules")','HttpPost("rules")']:
    assert marker in controllers, marker
# No production AI is allowed before trained models are integrated.
assert 'sessionInference=False' in ai and 'faceEnrollment=False' in ai
for folder in ['detection','tracking','identity','features','htbam','clustering','weak_labeling','events','inference']:
    assert (root/f'services/ai/app/{folder}').is_dir(), folder
print('REQUIREMENTS_OK')
