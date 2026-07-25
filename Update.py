"""
Albion Online - Tam Donusturucu (Cok Dilli Kusursuz Dumper + Stat Birlestirici)
================================================================================
KULLANIM:
    1. Eski statli items.json dosyasinin adini "items_stats.json" yap.
    2. Dumper'in urettigi 23 MB'lik dosyanin adini "items_dumper.json" yap.
    3. mobs.json ve localization.json (ham, COK DILLI TMX kaynagi) dosyalarini yanina koy.
    4. python Update.py

CIKTILAR (Helper/ klasorune):
    items.min.json              -> dilden bagimsiz, IP verisi
    localization_{DIL}.json     -> her dil icin islenmis lokalizasyon cache'i
    items_{DIL}.txt             -> Index:UniqueName:DisplayName:IP
    mobs_{DIL}_min.json         -> her dil icin cevrilmis mob verisi
    MobsID_{DIL}.txt            -> [index] : Cevrilmis Isim

NOT: CONFIG["languages"] listesi bos birakilirsa, localization.json icindeki
     TUM diller otomatik tespit edilip uretilir.
"""

import json, os, sys

# +====================================================+
# |  CONFIG - Buradan dosya yollarini ayarla            |
# +====================================================+
CONFIG = {
    "items_stats_json":    "items_stats.json",        # Statlarin (IP vb.) oldugu eski dosya
    "items_dumper_json":   "items_dumper.json",       # Dumper'in verdigi gercek ID dosyasi
    "mobs_json":           "mobs.json",
    "localization_json":   "localization.json",       # HAM, COK DILLI TMX kaynagi (tum diller burada)
    "output_dir":          "Helper",

    "items_min_json":      "items.min.json",          # dilden bagimsiz

    # Dil basina uretilecek dosya adi kaliplari ({SUFFIX} otomatik degisir, orn EN/RU/TR/ZH)
    "localization_cache_pattern": "localization_{SUFFIX}.json",
    "items_txt_pattern":          "items_{SUFFIX}.txt",
    "mobs_min_pattern":           "mobs_{SUFFIX}_min.json",
    "mobsid_txt_pattern":         "MobsID_{SUFFIX}.txt",

    "generate_items":      True,
    "generate_mobs":       True,
    "generate_items_txt":  True,
    "generate_mobs_txt":   True,

    # Uretilecek diller. TMX xml:lang kodu birebir string ("EN-US") ya da
    # {"tmx": "EN-US", "suffix": "EN"} seklinde ozel suffix de verilebilir.
    # BOS LISTE ([]) birakilirsa TMX icindeki TUM diller otomatik bulunur.
    "languages": ["EN-US", "RU-RU", "TR-TR", "ZH-CN"],

    "mobs_index_offset":   14,
    "enchant_ip_step":     100,
    "prototype_enc_base":  1200,
}


# +====================================================+
# |  Genel yardimcilar                                  |
# +====================================================+
def load_json(path, optional=False):
    if not os.path.exists(path):
        if optional: return None
        print(f"\n[HATA] Dosya bulunamadi: {os.path.abspath(path)}")
        sys.exit(1)
    size_mb = os.path.getsize(path) / (1024 * 1024)
    print(f"  Yukleniyor : {path} ({size_mb:.1f} MB) ...", end=" ", flush=True)
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    print("OK")
    return data

def save_json(data, path):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, separators=(",", ":"))
    size_kb = os.path.getsize(path) / 1024
    count = len(data) if isinstance(data, (list, dict)) else "-"
    print(f"  Kaydedildi : {path} ({count} kayit, {size_kb:.1f} KB)")

def write_lines_crlf(path, lines):
    """Dosyayi \\r\\n satir sonlariyla, son satirin ardinda fazladan bos satir olmadan yazar."""
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write("\r\n".join(lines))
    print(f"  Kaydedildi : {path} ({len(lines)} satir, {os.path.getsize(path)/1024:.1f} KB)")

def out(filename):
    d = CONFIG.get("output_dir", "").strip()
    if d:
        os.makedirs(d, exist_ok=True)
        return os.path.join(d, filename)
    return filename

def iter_item_type(items_root, item_type):
    entries = items_root.get(item_type, [])
    if isinstance(entries, dict): entries = [entries]
    return entries


# +====================================================+
# |  Dil listesi cozumleme                              |
# +====================================================+
def normalize_languages(languages_cfg):
    result = []
    for entry in languages_cfg:
        if isinstance(entry, dict):
            tmx = entry["tmx"]
            suffix = entry.get("suffix") or tmx.split("-")[0].upper()
        else:
            tmx = entry
            suffix = tmx.split("-")[0].upper()
        result.append((tmx, suffix))
    return result

def discover_languages(cfg):
    """TMX kaynagindaki TUM xml:lang kodlarini tespit eder (CONFIG['languages'] bos ise kullanilir)."""
    src = cfg["localization_json"]
    if not os.path.exists(src):
        print(f"\n[HATA] Diller otomatik tespit edilemiyor, '{src}' bulunamadi.")
        return []
    print(f"\n[Diller otomatik tespit ediliyor -> {src}]")
    data = load_json(src)
    found = set()

    def scan_tu(tu):
        tuv = tu.get("tuv", [])
        if isinstance(tuv, dict): tuv = [tuv]
        for t in tuv:
            xl = t.get("@xml:lang", "")
            if xl: found.add(xl.upper())

    try: tu_list = data["tmx"]["body"]["tu"]
    except Exception: tu_list = None

    if tu_list is not None:
        if isinstance(tu_list, dict): tu_list = [tu_list]
        for tu in tu_list: scan_tu(tu)
    else:
        def extract(obj):
            if isinstance(obj, dict):
                if "tuv" in obj: scan_tu(obj)
                else:
                    for v in obj.values():
                        if isinstance(v, (dict, list)): extract(v)
            elif isinstance(obj, list):
                for item in obj: extract(item)
        extract(data)

    langs = sorted(found)
    print(f"  Bulunan diller : {langs}")
    return [(code, code.split("-")[0].upper()) for code in langs]

def resolve_languages(cfg):
    languages_cfg = cfg.get("languages") or []
    if languages_cfg:
        return normalize_languages(languages_cfg)
    return discover_languages(cfg)


# +====================================================+
# |  Lokalizasyon (cok dilli, tek gecisli TMX okuma)     |
# +====================================================+
def load_or_build_localization_multi(cfg, languages):
    """
    languages: [(tmx_code, suffix), ...]
    Donus: {suffix: {tuid: text}}
    Cache'i diskte olan diller icin TMX'e hic dokunmaz.
    Eksik olanlarin hepsini TEK bir TMX gecisinde birlikte uretir.
    """
    print("\n[Lokalizasyon]")
    result = {}
    missing = []
    for tmx_code, suffix in languages:
        cache_path = out(cfg["localization_cache_pattern"].format(SUFFIX=suffix))
        if os.path.exists(cache_path):
            data = load_json(cache_path)
            print(f"  Cache'den  : {suffix} -> {len(data)} ceviri")
            result[suffix] = data
        else:
            missing.append((tmx_code, suffix, cache_path))

    if not missing:
        return result

    src_path = cfg["localization_json"]
    if not os.path.exists(src_path):
        print(f"  [HATA] '{src_path}' bulunamadi! Eksik dil cache'leri uretilemiyor: "
              f"{[s for _, s, _ in missing]}")
        for _, suffix, _ in missing:
            result[suffix] = {}
        return result

    print(f"  TMX kaynagi okunuyor (eksik diller: {[s for _, s, _ in missing]})")
    data = load_json(src_path)

    wanted = {tmx_code.upper(): suffix for tmx_code, suffix, _ in missing}
    buckets = {suffix: {} for _, suffix, _ in missing}
    counts  = {suffix: 0 for _, suffix, _ in missing}

    def process_tu(tu):
        tuid = tu.get("@tuid", "")
        if not tuid: return
        tuv = tu.get("tuv", [])
        if isinstance(tuv, dict): tuv = [tuv]
        for t in tuv:
            xml_lang = t.get("@xml:lang", "").upper()
            suf = wanted.get(xml_lang)
            if suf:
                seg = t.get("seg", "")
                if isinstance(seg, str) and seg:
                    buckets[suf][tuid.lstrip("@")] = seg
                    counts[suf] += 1

    try: tu_list = data["tmx"]["body"]["tu"]
    except Exception: tu_list = None

    if tu_list is not None:
        if isinstance(tu_list, dict): tu_list = [tu_list]
        for tu in tu_list: process_tu(tu)
    else:
        def extract(obj):
            if isinstance(obj, dict):
                if "@tuid" in obj: process_tu(obj)
                else:
                    for v in obj.values():
                        if isinstance(v, (dict, list)): extract(v)
            elif isinstance(obj, list):
                for item in obj: extract(item)
        extract(data)

    for tmx_code, suffix, cache_path in missing:
        print(f"  Bulunan ({suffix}): {counts[suffix]} ceviri")
        save_json(buckets[suffix], cache_path)
        result[suffix] = buckets[suffix]

    return result


# +====================================================+
# |  items.min.json (dilden bagimsiz)                   |
# +====================================================+
def get_enchant_levels(entry):
    tier = int(entry.get("@tier", 0) or 0)
    if tier < 4 or "DEBUG" in entry.get("@uniquename", ""): return []
    canbeovercharged  = entry.get("@canbeovercharged", "")
    slottype          = entry.get("@slottype", "")
    showinmarketplace = entry.get("@showinmarketplace", "")
    if canbeovercharged == "true": return [1, 2, 3] if showinmarketplace == "false" else [1, 2, 3, 4]
    if slottype in ("cape", "bag") and showinmarketplace != "false": return [1, 2, 3, 4]
    return []

def calc_enc_ip(base_ip, lvl, cfg):
    step = cfg["enchant_ip_step"]
    if base_ip >= 1600: return cfg["prototype_enc_base"] + (lvl - 1) * step
    return base_ip + lvl * step

def build_items_min(items_json_path, output_path, cfg):
    print("\n[items.min.json uretiliyor (dilden bagimsiz)]")
    data       = load_json(items_json_path)
    items_root = data.get("items", data)
    ITEM_TYPES = ["equipmentitem", "weapon", "mount", "trackingitem"]
    result, skipped = [], 0

    for itype in ITEM_TYPES:
        for entry in iter_item_type(items_root, itype):
            uniquename = entry.get("@uniquename", "")
            if not uniquename: skipped += 1; continue
            raw_ip = entry.get("@itempower", "")
            try: base_ip = int(float(raw_ip)) if raw_ip != "" else None
            except: base_ip = None
            if base_ip is None: skipped += 1; continue

            cat, slot = entry.get("@shopcategory", ""), entry.get("@slottype", "")
            rec  = {"n": uniquename, "p": base_ip, "t": itype, "cat": cat, "slot": slot}
            if entry.get("@twohanded") == "true": rec["h2"] = True
            result.append(rec)

            for lvl in get_enchant_levels(entry):
                enc = {"n": f"{uniquename}@{lvl}", "p": calc_enc_ip(base_ip, lvl, cfg), "t": itype, "cat": cat, "slot": slot}
                if entry.get("@twohanded") == "true": enc["h2"] = True
                result.append(enc)

    print(f"  Islendi   : {len(result)} kayit  |  Atlanan: {skipped}")
    save_json(result, output_path)
    return result


# +====================================================+
# |  Mob verisi (dilden bagimsiz baz + dil basina isim)  |
# +====================================================+
def build_mobs_base(mobs_json_path):
    """Mob verisini bir kere okur. Isim (n) ICERMEZ - sadece cozumlenecek ham '_tag' tutulur."""
    print("\n[mob verisi okunuyor (dilden bagimsiz)]")
    data = load_json(mobs_json_path)
    mob_list = data.get("Mobs", data).get("Mob", [])
    if isinstance(mob_list, dict): mob_list = [mob_list]
    result, skipped = [], 0

    for mob in mob_list:
        uniquename = mob.get("@uniquename", "")
        if not uniquename: skipped += 1; continue
        try:
            tier = int(mob.get("@tier", 0) or 0)
            fame = int(float(mob.get("@fame", 0) or 0))
            hp   = int(float(mob.get("@hitpointsmax", 0) or 0))
        except: skipped += 1; continue

        rec = {"u": uniquename, "t": tier}
        mob_c = mob.get("@mobtypecategory", "") or mob.get("@category", "")
        if mob_c: rec["c"] = mob_c
        rec["fame"], rec["hp"], rec["avatar"] = fame, hp, mob.get("@avatar", "")
        if mob.get("@dangerstate", ""): rec["danger"] = mob.get("@dangerstate", "")

        harvestable = mob.get("Loot", {}).get("Harvestable", {}) if isinstance(mob.get("Loot", {}), dict) else {}
        if harvestable and harvestable.get("@type", ""):
            rec["l"] = harvestable.get("@type", "")
            rec["lt"] = int(harvestable.get("@tier", tier))

        rec["_tag"] = mob.get("@namelocatag", "")  # cevrilecek ham etiket (ciktiya yazilmaz)
        result.append(rec)

    print(f"  Islendi   : {len(result)} kayit  |  Atlanan: {skipped}")
    return result

def resolve_mob_name(tag, loc):
    """
    Onceligi:
      1) loc[tag] varsa onu kullan (gercek cevrilmis isim)
      2) yoksa ham tag'i title-case yaparak fallback uret (TUM dillerde ayni kalir)
    """
    if not tag: return ""
    key = tag.lstrip("@")
    name = loc.get(key, "")
    if name: return name
    return key.replace("_", " ").title()

def build_mobs_min_for_lang(mobs_base, loc, output_path):
    result = []
    for rec in mobs_base:
        item = {"u": rec["u"], "t": rec["t"]}
        if "c" in rec: item["c"] = rec["c"]
        name = resolve_mob_name(rec.get("_tag", ""), loc)
        if name: item["n"] = name
        item["fame"], item["hp"], item["avatar"] = rec["fame"], rec["hp"], rec["avatar"]
        if "danger" in rec: item["danger"] = rec["danger"]
        if "l" in rec: item["l"] = rec["l"]
        if "lt" in rec: item["lt"] = rec["lt"]
        result.append(item)
    save_json(result, output_path)
    return result

def build_mobs_txt(mobs_min, output_path, offset):
    lines = [f"[{i + offset + 1}] : {mob.get('n', '') or 'Unknown'}" for i, mob in enumerate(mobs_min)]
    write_lines_crlf(output_path, lines)


# +====================================================+
# |  items.txt (dil basina, Dumper ID + Stat IP birlesimi)|
# +====================================================+
def build_items_txt_for_lang(dumper_data, ip_map, loc, output_path):
    if not loc:
        print(f"  [items.txt] UYARI: lokalizasyon yok, atlaniyor -> {output_path}")
        return

    def get_display(uniquename):
        name = loc.get(f"ITEMS_{uniquename}", "")
        if name: return name
        if "@" in uniquename:
            base = uniquename.rsplit("@", 1)[0]
            name = loc.get(f"ITEMS_{base}", "")
            if name: return name
        return ""

    out_lines = []
    no_name = 0
    for item in dumper_data:
        idx_str = str(item.get("Index", ""))
        uniquename = item.get("UniqueName", "").strip()
        if not idx_str or not uniquename:
            continue

        display = get_display(uniquename).strip()
        if not display: no_name += 1

        ip = ip_map.get(uniquename, 0)
        if ip > 0: out_lines.append(f"{idx_str}:{uniquename}:{display}:{ip}")
        else: out_lines.append(f"{idx_str}:{uniquename}:{display}")

    write_lines_crlf(output_path, out_lines)
    print(f"  Isim bulunamayan: {no_name}")


# +====================================================+
# |  ANA AKIS                                            |
# +====================================================+
def main():
    print("=" * 60)
    print("   Albion Online JSON Donusturucu (Cok Dilli Nihai Birlestirici)")
    print("=" * 60)
    print(f"  Dizin : {os.getcwd()}")

    output_dir = CONFIG.get("output_dir", "").strip()
    if output_dir: print(f"  Cikti : {os.path.abspath(output_dir)}/")

    languages = resolve_languages(CONFIG)
    if not languages:
        print("\n[HATA] Islenecek dil bulunamadi, cikiliyor.")
        sys.exit(1)
    print(f"  Diller: {[f'{s} ({t})' for t, s in languages]}")

    # 1) Dilden bagimsiz veriler - HER BIRI TEK SEFER okunur
    items_min, ip_map = [], {}
    if CONFIG["generate_items"]:
        items_min = build_items_min(CONFIG["items_stats_json"], out(CONFIG["items_min_json"]), CONFIG)
        ip_map = {it["n"]: it.get("p", 0) for it in items_min}

    mobs_base = []
    if CONFIG["generate_mobs"]:
        mobs_base = build_mobs_base(CONFIG["mobs_json"])

    dumper_data = None
    if CONFIG["generate_items_txt"]:
        dumper_path = CONFIG["items_dumper_json"]
        if not os.path.exists(dumper_path):
            print(f"\n[KRITIK HATA] '{dumper_path}' bulunamadi! Dumper'in urettigi "
                  f"items.json'i bu isimle yanina koy mq.")
        else:
            print("\n[Dumper verisi okunuyor (dilden bagimsiz, tek seferlik)]")
            dumper_data = load_json(dumper_path)

    # 2) Lokalizasyon - eksik diller icin TEK GECISTE uretilir
    loc_by_lang = load_or_build_localization_multi(CONFIG, languages)

    # 3) Dil basina ciktilar
    for tmx_code, suffix in languages:
        print(f"\n{'-' * 60}\n  >> Dil isleniyor: {suffix}  (TMX: {tmx_code})\n{'-' * 60}")
        loc = loc_by_lang.get(suffix, {})
        if not loc:
            print(f"  [UYARI] '{suffix}' icin lokalizasyon verisi yok/bos.")

        mobs_min_lang = []
        if CONFIG["generate_mobs"] and mobs_base:
            mobs_path = out(CONFIG["mobs_min_pattern"].format(SUFFIX=suffix))
            mobs_min_lang = build_mobs_min_for_lang(mobs_base, loc, mobs_path)

        if CONFIG["generate_items_txt"] and dumper_data is not None:
            items_txt_path = out(CONFIG["items_txt_pattern"].format(SUFFIX=suffix))
            build_items_txt_for_lang(dumper_data, ip_map, loc, items_txt_path)

        if CONFIG["generate_mobs_txt"] and mobs_min_lang:
            mobsid_path = out(CONFIG["mobsid_txt_pattern"].format(SUFFIX=suffix))
            build_mobs_txt(mobs_min_lang, mobsid_path, CONFIG["mobs_index_offset"])

    print("\n" + "=" * 60)
    print("   Tamamlandi!")
    print("=" * 60)

if __name__ == "__main__":
    main()