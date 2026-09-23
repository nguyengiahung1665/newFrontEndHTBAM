const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const files = [
  path.join(root, 'database', '006_add_annotated_videos.sql'),
  path.join(root, 'database', 'sqlserver', '006_add_annotated_videos.sql'),
];

const required = [
  "OBJECT_ID('ManagementAssignments','U') IS NULL",
  "OBJECT_ID('SessionSubstitutions','U') IS NULL",
  "COL_LENGTH('Sessions','OriginalTeacherId') IS NULL",
  "COL_LENGTH('Videos','VideoType') IS NULL",
  "COL_LENGTH('Videos','SessionId') IS NULL",
  "COL_LENGTH('Videos','ParentVideoId') IS NULL",
  "COL_LENGTH('Videos','IsSystemGenerated') IS NULL",
  "sys.foreign_keys WHERE name='FK_Video_Session'",
  "sys.foreign_keys WHERE name='FK_Video_Parent'",
  "sys.check_constraints WHERE name='CK_Video_Type'",
  "sys.check_constraints WHERE name='CK_Video_Classification'",
  "sys.indexes WHERE name='UX_Videos_Annotated_Session'",
  "VideoType IN(''INPUT_UPLOAD'',''ANNOTATED_OUTPUT'')",
];

const scripts = files.map((file) => {
  const sql = fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '');
  for (const fragment of required) {
    if (!sql.includes(fragment)) {
      throw new Error(`${path.relative(root, file)} thiếu guard/schema: ${fragment}`);
    }
  }
  if (!/BEGIN TRANSACTION;[\s\S]*COMMIT TRANSACTION;/i.test(sql)) {
    throw new Error(`${path.relative(root, file)} không bọc nâng cấp trong transaction`);
  }
  return sql.replace(/\r\n/g, '\n').trim();
});

if (scripts[0] !== scripts[1]) {
  throw new Error('Hai script nâng cấp SQL Server không đồng bộ.');
}

console.log('SCHEMA_UPGRADE_IDEMPOTENCE_GUARDS_OK');
