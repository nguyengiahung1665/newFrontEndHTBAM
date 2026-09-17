from pathlib import Path
import json, py_compile, sys, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[1]
for p in root.rglob('*.py'):
    if 'node_modules' not in p.parts: py_compile.compile(str(p), doraise=True)
for p in root.rglob('*.json'):
    json.loads(p.read_text())
for p in root.rglob('*.csproj'):
    ET.parse(p)
# Lightweight delimiter scan for C#/TS/TSX; not a replacement for dotnet/tsc build.
def balanced(path):
    s=path.read_text()
    stack=[]; pairs={')':'(',']':'[','}':'{'}; opens=set(pairs.values())
    quote=None; esc=False; line_comment=False; block_comment=False; i=0
    while i<len(s):
        c=s[i]; n=s[i+1] if i+1<len(s) else ''
        if line_comment:
            if c=='\n': line_comment=False
            i+=1; continue
        if block_comment:
            if c=='*' and n=='/': block_comment=False; i+=2; continue
            i+=1; continue
        if quote:
            if esc: esc=False
            elif c=='\\': esc=True
            elif c==quote: quote=None
            i+=1; continue
        if c=='/' and n=='/': line_comment=True; i+=2; continue
        if c=='/' and n=='*': block_comment=True; i+=2; continue
        if c in "'\"`": quote=c; i+=1; continue
        if c in opens: stack.append(c)
        elif c in pairs:
            if not stack or stack.pop()!=pairs[c]: raise AssertionError(f'delimiter mismatch {path} at {i}')
        i+=1
    if stack: raise AssertionError(f'unclosed delimiter {path}: {stack[-10:]}')
for ext in ('*.cs','*.ts','*.tsx'):
    for p in root.rglob(ext): balanced(p)
print('STATIC_SYNTAX_OK')
