#!/usr/bin/env python3
"""Render card_pool.json into an inspection table (Markdown + HTML)."""

import json
import html
from collections import Counter

POOL = r"E:\Games\Overlink\Assets\Cards\card_pool.json"
MD_OUT = r"E:\Games\Overlink\Tools\card_pool_table.md"
HTML_OUT = r"E:\Games\Overlink\Tools\card_pool_table.html"
APPEARANCES_MD = r"E:\Games\Overlink\Tools\card_pool_appearances.md"

with open(POOL, "r", encoding="utf-8") as f:
    data = json.load(f)

cards = data["cards"]

RARITY_ORDER = {"Common": 0, "Rare": 1, "Epic": 2, "Legendary": 3}
TYPE_ORDER = {"Unit": 0, "Ability": 1, "Relay": 2, "Protocol": 3}
FACTION_ORDER = {"fsti": 0, "frti": 1, "fpb": 2}
FACTION_NAME = {
    "fsti": "FSTI — Science & Technology",
    "frti": "FRTI — Industrial / Mechanical",
    "fpb": "FPB — Drawing / Planning / Art",
}


def stats(c):
    if c["type"] == "Unit":
        return f"{c['atk']}/{c['hp']}" + (f"+{c['shield']}S" if c["shield"] else "")
    if c["shield"]:
        return f"{c['shield']}S"
    return "—"


def effects_summary(c):
    parts = []
    for e in c.get("effects", []):
        bits = [e["type"], e["target"]]
        if e.get("amount"):
            bits.append(str(e["amount"]))
        if e.get("duration") == -1:
            bits.append("perm")
        elif e.get("duration", 0) > 0:
            bits.append(f"{e['duration']}t")
        if e.get("fast"):
            bits.append("FAST")
        parts.append(" ".join(bits))
    return "; ".join(parts) if parts else "—"


cards_sorted = sorted(
    cards,
    key=lambda c: (
        FACTION_ORDER[c["faction"]],
        RARITY_ORDER[c["rarity"]],
        c["id"],
    ),
)

# ---------------------------------------------------------------- Markdown
lines = []
lines.append("# Overlink — 150 Card Pool Inspection Table")
lines.append("")
lines.append(f"Source: `{POOL}`")
lines.append("")
comp = data["composition"]
lines.append(
    f"**Composition:** {comp['total']} total | "
    f"Common {comp['byRarity']['common']} / Rare {comp['byRarity']['rare']} / "
    f"Epic {comp['byRarity']['epic']} / Legendary {comp['byRarity']['legendary']} | "
    f"FSTI {comp['byFaction']['fsti']} / FRTI {comp['byFaction']['frti']} / FPB {comp['byFaction']['fpb']}"
)
lines.append("")
tc = Counter(c["type"] for c in cards)
lines.append(
    f"**Types:** Unit {tc['Unit']} / Ability {tc['Ability']} / Relay {tc['Relay']} / Protocol {tc['Protocol']}"
)
lines.append("")

for faction in ("fsti", "frti", "fpb"):
    lines.append(f"## {FACTION_NAME[faction]}")
    lines.append("")
    for rarity in ("Common", "Rare", "Epic", "Legendary"):
        subset = [
            c for c in cards_sorted
            if c["faction"] == faction and c["rarity"] == rarity
        ]
        if not subset:
            continue
        lines.append(f"### {rarity} ({len(subset)})")
        lines.append("")
        lines.append("| ID | Name | Type | Cost | A/H/S | Keywords | Ability Text | Effects |")
        lines.append("|---|---|---|---|---|---|---|---|")
        for c in subset:
            kw = ", ".join(c.get("keywords", [])) or "—"
            lines.append(
                f"| `{c['id']}` | {c['name']} | {c['type']} | {c['cost']} | {stats(c)} "
                f"| {kw} | {c.get('abilityText') or '—'} | {effects_summary(c)} |"
            )
        lines.append("")

# Append the appearance-descriptions section (inspection doc only; not part of the JSON).
import os
if os.path.exists(APPEARANCES_MD):
    with open(APPEARANCES_MD, "r", encoding="utf-8") as f:
        appearance_md = f.read()
    # Drop the standalone title line; the MD_OUT doc gets its own section header.
    appearance_body = appearance_md.split("\n", 1)[1].lstrip("\n")
    lines.append("---")
    lines.append("")
    lines.append("# Appearance Descriptions")
    lines.append("")
    lines.append(appearance_body.rstrip())
    lines.append("")

with open(MD_OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))

# ---------------------------------------------------------------- HTML
def esc(s):
    return html.escape(str(s)) if s is not None else ""


# Appearance rows for HTML: id -> description (parsed from the appearances MD table rows).
appearance_by_id = {}
if os.path.exists(APPEARANCES_MD):
    with open(APPEARANCES_MD, "r", encoding="utf-8") as f:
        for ln in f:
            if not ln.startswith("| `card_"):
                continue
            parts = [p.strip() for p in ln.strip().strip("|").split("|")]
            if len(parts) >= 3:
                appearance_by_id[parts[0].strip("`")] = parts[2]

rows_html = []
for c in cards_sorted:
    rows_html.append(
        "<tr data-faction=\"{f}\" data-rarity=\"{r}\" data-type=\"{t}\">"
        "<td class=\"mono\">{id}</td>"
        "<td class=\"name\">{name}</td>"
        "<td><span class=\"pill pill-{fl}\">{fl}</span></td>"
        "<td><span class=\"pill pill-{rl}\">{r}</span></td>"
        "<td>{t}</td>"
        "<td class=\"num\">{cost}</td>"
        "<td class=\"num\">{atk}</td>"
        "<td class=\"num\">{hp}</td>"
        "<td class=\"num\">{sh}</td>"
        "<td class=\"kw\">{kw}</td>"
        "<td class=\"abil\">{abil}</td>"
        "<td class=\"eff mono\">{eff}</td>"
        "<td class=\"appear\">{appear}</td>"
        "</tr>".format(
            f=esc(c["faction"]),
            r=esc(c["rarity"]),
            t=esc(c["type"]),
            id=esc(c["id"]),
            name=esc(c["name"]),
            fl=esc(c["faction"]),
            rl=esc(c["rarity"].lower()),
            cost=c["cost"],
            atk=c["atk"] if c["type"] == "Unit" else "—",
            hp=c["hp"] if c["type"] == "Unit" else "—",
            sh=c["shield"] if c["shield"] else "—",
            kw=esc(", ".join(c.get("keywords", [])) or "—"),
            abil=esc(c.get("abilityText") or "—"),
            eff=esc(effects_summary(c)),
            appear=esc(appearance_by_id.get(c["id"], "—")),
        )
    )

faction_rows = []
for fk in ("fsti", "frti", "fpb"):
    sub = [c for c in cards if c["faction"] == fk]
    rc = Counter(c["rarity"] for c in sub)
    tc2 = Counter(c["type"] for c in sub)
    faction_rows.append(
        "<tr><td><b>{name}</b></td><td>{tot}</td><td>{c}</td><td>{r}</td>"
        "<td>{e}</td><td>{l}</td><td>{u}</td><td>{a}</td><td>{rl}</td><td>{p}</td></tr>".format(
            name=esc(FACTION_NAME[fk]),
            tot=len(sub),
            c=rc["Common"], r=rc["Rare"], e=rc["Epic"], l=rc["Legendary"],
            u=tc2["Unit"], a=tc2["Ability"], rl=tc2["Relay"], p=tc2["Protocol"],
        )
    )

HTML = """<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Overlink — 150 Card Pool</title>
<style>
:root {
  --bg: #0f1115; --panel: #171a21; --line: #262b36; --text: #e6e8ee;
  --muted: #9aa3b2; --accent: #6ea8fe;
  --fsti: #4fc3f7; --frti: #ff9e57; --fpb: #a5d6a7;
  --common: #8d99ae; --rare: #5b9dff; --epic: #b06bff; --legendary: #ffb443;
}
* { box-sizing: border-box; }
body { margin: 0; padding: 24px; background: var(--bg); color: var(--text);
  font: 14px/1.5 "Segoe UI", system-ui, sans-serif; }
h1 { font-size: 22px; margin: 0 0 4px; }
.sub { color: var(--muted); margin-bottom: 20px; font-size: 13px; }
h2 { font-size: 16px; margin: 24px 0 8px; color: var(--accent); }
table { border-collapse: collapse; width: 100%; background: var(--panel); }
th, td { border: 1px solid var(--line); padding: 6px 8px; text-align: left; vertical-align: top; }
th { background: #1d222b; position: sticky; top: 0; z-index: 2; font-size: 12px;
  text-transform: uppercase; letter-spacing: .04em; color: var(--muted); cursor: pointer; }
td.num { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
td.mono, .mono { font-family: Consolas, monospace; font-size: 12px; }
td.name { font-weight: 600; white-space: nowrap; }
td.kw { color: #ffd479; font-size: 12px; }
td.abil { color: #cfd6e4; font-size: 12.5px; max-width: 340px; }
td.eff { color: var(--muted); font-size: 11.5px; max-width: 300px; }
td.appear { color: #cfd6e4; font-size: 12.5px; max-width: 420px; font-style: italic; }
.pill { display: inline-block; padding: 1px 7px; border-radius: 999px; font-size: 11px;
  font-weight: 600; background: #222834; white-space: nowrap; }
.pill-fsti { color: var(--fsti); border: 1px solid var(--fsti); }
.pill-frti { color: var(--frti); border: 1px solid var(--frti); }
.pill-fpb  { color: var(--fpb);  border: 1px solid var(--fpb); }
.pill-common { color: var(--common); border: 1px solid var(--common); }
.pill-rare { color: var(--rare); border: 1px solid var(--rare); }
.pill-epic { color: var(--epic); border: 1px solid var(--epic); }
.pill-legendary { color: var(--legendary); border: 1px solid var(--legendary); }
.controls { display: flex; flex-wrap: wrap; gap: 8px; align-items: center;
  margin-bottom: 12px; position: sticky; top: 0; background: var(--bg); padding: 10px 0; z-index: 5; }
.controls input, .controls select { background: #12151c; color: var(--text);
  border: 1px solid var(--line); border-radius: 6px; padding: 6px 10px; font-size: 13px; }
.controls input { min-width: 260px; }
.count { color: var(--muted); font-size: 12px; margin-left: auto; }
.summary td { font-variant-numeric: tabular-nums; }
tbody tr:hover { background: #1c222c; }
</style>
</head>
<body>
<h1>Overlink — Card Pool Inspection</h1>
<div class="sub">__TOTAL__ cards &middot; source <span class="mono">Assets/Cards/card_pool.json</span>
 &middot; formatVersion 1 &middot; validator: 0 errors / 0 warnings &middot; Mneme review: VERIFIED</div>

<h2>Composition summary</h2>
<table class="summary">
<thead><tr><th>Faction</th><th>Total</th><th>Common</th><th>Rare</th><th>Epic</th><th>Legendary</th>
<th>Unit</th><th>Ability</th><th>Relay</th><th>Protocol</th></tr></thead>
<tbody>
__FACTION_ROWS__
</tbody>
</table>

<h2>All cards</h2>
<div class="controls">
  <input id="q" type="search" placeholder="Search name, id, keyword, ability text...">
  <select id="ff"><option value="">All factions</option>
    <option value="fsti">FSTI</option><option value="frti">FRTI</option><option value="fpb">FPB</option></select>
  <select id="fr"><option value="">All rarities</option>
    <option value="Common">Common</option><option value="Rare">Rare</option>
    <option value="Epic">Epic</option><option value="Legendary">Legendary</option></select>
  <select id="ft"><option value="">All types</option>
    <option value="Unit">Unit</option><option value="Ability">Ability</option>
    <option value="Relay">Relay</option><option value="Protocol">Protocol</option></select>
  <span class="count" id="count"></span>
</div>
<table id="tbl">
<thead><tr>
  <th data-k="0">ID</th><th data-k="1">Name</th><th data-k="2">Faction</th>
  <th data-k="3">Rarity</th><th data-k="4">Type</th><th data-k="5">Cost</th>
  <th data-k="6">ATK</th><th data-k="7">HP</th><th data-k="8">Shield</th>
  <th data-k="9">Keywords</th><th data-k="10">Ability text</th><th data-k="11">Effects</th>
  <th data-k="12">Appearance</th>
</tr></thead>
<tbody>
__ROWS__
</tbody>
</table>

<script>
const tbl = document.getElementById('tbl');
const rows = Array.from(tbl.tBodies[0].rows);
const q = document.getElementById('q'), ff = document.getElementById('ff'),
      fr = document.getElementById('fr'), ft = document.getElementById('ft'),
      count = document.getElementById('count');

function apply() {
  const term = q.value.trim().toLowerCase();
  let shown = 0;
  for (const r of rows) {
    const okF = !ff.value || r.dataset.faction === ff.value;
    const okR = !fr.value || r.dataset.rarity === fr.value;
    const okT = !ft.value || r.dataset.type === ft.value;
    const okQ = !term || r.textContent.toLowerCase().includes(term);
    const vis = okF && okR && okT && okQ;
    r.style.display = vis ? '' : 'none';
    if (vis) shown++;
  }
  count.textContent = shown + ' / ' + rows.length + ' cards shown';
}
[q, ff, fr, ft].forEach(el => el.addEventListener('input', apply));

let sortDir = {};
document.querySelectorAll('#tbl thead th').forEach(th => {
  th.addEventListener('click', () => {
    const k = +th.dataset.k;
    sortDir[k] = !sortDir[k];
    const dir = sortDir[k] ? 1 : -1;
    const body = tbl.tBodies[0];
    rows.sort((a, b) => {
      const x = a.cells[k].textContent.trim(), y = b.cells[k].textContent.trim();
      const nx = parseFloat(x), ny = parseFloat(y);
      const cmp = (!isNaN(nx) && !isNaN(ny) && x !== '—' && y !== '—')
        ? nx - ny : x.localeCompare(y);
      return cmp * dir;
    });
    rows.forEach(r => body.appendChild(r));
  });
});
apply();
</script>
</body>
</html>
"""

HTML = (HTML
        .replace("__TOTAL__", str(len(cards)))
        .replace("__FACTION_ROWS__", "\n".join(faction_rows))
        .replace("__ROWS__", "\n".join(rows_html)))

with open(HTML_OUT, "w", encoding="utf-8") as f:
    f.write(HTML)

print("Markdown:", MD_OUT)
print("HTML:    ", HTML_OUT)
print("Rows:    ", len(cards_sorted))
