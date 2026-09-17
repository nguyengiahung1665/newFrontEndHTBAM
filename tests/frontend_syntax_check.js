const fs=require('fs'), path=require('path');
const ts=require(path.resolve(__dirname,'..','apps','web','node_modules','typescript'));
const root=path.resolve(__dirname,'..','apps','web','src');
const files=[];
function walk(d){for(const n of fs.readdirSync(d)){const p=path.join(d,n),s=fs.statSync(p);if(s.isDirectory())walk(p);else if(/\.(ts|tsx)$/.test(n)&&!n.endsWith('.d.ts'))files.push(p)}}
walk(root); let bad=[];
for(const f of files){const source=fs.readFileSync(f,'utf8');const r=ts.transpileModule(source,{fileName:f,reportDiagnostics:true,compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.ReactJSX}});for(const d of r.diagnostics||[]){if(d.category===ts.DiagnosticCategory.Error){bad.push(`${path.relative(root,f)}: TS${d.code} ${ts.flattenDiagnosticMessageText(d.messageText,' ')}`)}}}
if(bad.length){console.error(bad.join('\n'));process.exit(1)}
console.log(`FRONTEND_TS_SYNTAX_OK (${files.length} files)`);
