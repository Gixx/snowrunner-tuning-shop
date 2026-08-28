import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
uitext = root / "src/SnowRunnerTuningShop/Localization/UiText.cs"
text = uitext.read_text(encoding="utf-8")
text = text.replace('StringResources.Get("UiText.', 'StringResources.Get("')
text = re.sub(r'StringResources\.Format\("UiText\.', 'StringResources.Format("', text)
uitext.write_text(text, encoding="utf-8")

en_path = root / "assets/localization/en.json"
data = json.loads(en_path.read_text(encoding="utf-8"))
fixed = {}
for k, v in data.items():
    nk = k[7:] if k.startswith("UiText.") else k
    fixed[nk] = v
en_path.write_text(
    json.dumps(dict(sorted(fixed.items())), ensure_ascii=False, indent=2) + "\n",
    encoding="utf-8",
)
print(f"Fixed {len(fixed)} keys")
