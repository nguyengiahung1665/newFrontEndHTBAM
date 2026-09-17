from pathlib import Path

root = Path(__file__).resolve().parents[1]
frontend = "\n".join(p.read_text(encoding="utf-8") for p in (root / "apps/web/src").rglob("*.ts*"))
compose = (root / "infra/docker-compose.yml").read_text(encoding="utf-8")
gitignore = (root / ".gitignore").read_text(encoding="utf-8")
env_example = (root / "infra/.env.example").read_text(encoding="utf-8")

assert "GEMINI_API_KEY" not in frontend
assert 'GEMINI_API_KEY: "${GEMINI_API_KEY}"' in compose
assert 'Llm__Model: "${GEMINI_MODEL:-gemini-2.5-pro}"' in compose
assert "infra/.env" in gitignore and "**/.env" in gitignore
assert "GEMINI_API_KEY=PASTE_YOUR_REAL_GEMINI_API_KEY_HERE" in env_example
assert "AIza" not in env_example
print("LLM_SECURITY_OK")

