# -*- coding: utf-8 -*-
"""
NIGHTWATCH - TEK DOSYA GUNCELLEME  (Update.py)
==============================================
KULLANIM (tek komut):
    python Update.py

Ne yapar? Hepsini sirayla, otomatik:
    1) dump.cs bulunur (bu dosyanin yaninda veya Extra\\ icinde)
    2) Enum + parametre tablosu uretilir   -> Extra\\paket_semasi.json
    3) Semboller uretilir                  -> AlbionDataHandlersNET8\\Packets\\*.g.cs
       (Events.g.cs / Operations.g.cs / PacketMeta.g.cs)
    4) Kaynak proje varsa derlenir (yoksa atlanir)
    5) Helper .json lar uretilir           -> items / mobs / localization / zones / spells
    6) Veri Assets\\Helper'a kopyalanir, exe HEDEF.txt deki oyun klasorune kopyalanir

Icindeki gomulu araclar BELLEKTE calistirilir; diske ek dosya cikmaz.
"""

import base64
import os
import shutil
import subprocess
import sys
import time
import types

KOK = os.path.dirname(os.path.abspath(__file__))
EXTRA = os.path.join(KOK, "Extra")
SURUM = "2026-10-08c  (adim 2b: degisen paket karsilastirmasi dahil)"


def baslik(m):
    print()
    print("=" * 64)
    print("  " + m)
    print("=" * 64)


def ok(m):
    print("  [OK]   " + m)


def bilgi(m):
    print("  [..]   " + m)


def uyari(m):
    print("  [!]    " + m)


def hata(m):
    print("  [HATA] " + m)


# ----------------------------------------------------------------------
# 1) Gomulu kaynaklari coz + BELLEKTE modul olarak yukle
# ----------------------------------------------------------------------
def _bloklari_oku(dosya=None):
    """Gomulu bloklari (_BLOB string'i) cozer."""
    satirlar = _BLOB.splitlines()
    bloklar = {}
    for ad in ("ANALIZ", "UPDATER"):
        try:
            s = satirlar.index(":::B64:%s:START" % ad)
            e = satirlar.index(":::B64:%s:END" % ad)
        except ValueError:
            continue
        bloklar[ad] = base64.b64decode("".join(satirlar[s + 1:e])).decode("utf-8", "replace")
    return bloklar


def _modul_yukle(ad, kaynak, sahte_yol, ek_argv=None):
    mod = types.ModuleType(ad)
    mod.__file__ = sahte_yol
    eski_argv = sys.argv
    sys.argv = ek_argv if ek_argv else [sahte_yol]
    try:
        exec(compile(kaynak, sahte_yol, "exec"), mod.__dict__)
    finally:
        sys.argv = eski_argv
    return mod


ESKI_ADAYLAR = ("dump_eski.cs", "dump_eski.txt", "dump_old.cs", "dump_old.txt",
                "dump_onceki.cs", "dump_onceki.txt", "dump2.cs", "dump2.txt",
                "dumpeski.cs", "dumpeski.txt")


def _eski_dump_bul(yeni_dump):
    """Bu dosyanin yaninda / Extra icinde ESKI dump var mi? (yeni dump haric)"""
    for klasor in (EXTRA, KOK):
        for ad in ESKI_ADAYLAR:
            p = os.path.join(klasor, ad)
            if os.path.exists(p) and os.path.abspath(p) != os.path.abspath(yeni_dump):
                return p
    return None


def _sema_imza(an, variant):
    return (variant["field_count"],
            tuple(an.canon_type(f["type"]) for f in variant["fields"]),
            tuple(f["offset"] for f in variant["fields"]))


def karsilastir(an, eski_dump, yeni_dump):
    """Iki dump'u paket paket karsilastirir. (ozet, rapor_metni) dondurur."""
    an.DUMP = eski_dump
    _c1, _k1, sema_e, _u1 = an.load()
    an.DUMP = yeni_dump
    _c2, _k2, sema_n, _u2 = an.load()

    R = []
    R.append("=" * 74)
    R.append("  PAKET KARSILASTIRMA  (eski surum <-> yeni surum)")
    R.append("=" * 74)
    R.append("  Eski dump : %s" % eski_dump)
    R.append("  Yeni dump : %s" % yeni_dump)
    R.append("  Arac surum: %s" % SURUM)
    R.append("")

    ozet = {}
    detay = []

    for bolum, baslik in (("events", "EVENT"), ("operations", "OPERASYON")):
        e_all, n_all = sema_e.get(bolum, {}), sema_n.get(bolum, {})
        eklenen = sorted(set(n_all) - set(e_all), key=int)
        kaldirilan = sorted(set(e_all) - set(n_all), key=int)
        degisen = []
        for c in sorted(set(e_all) & set(n_all), key=int):
            es = sorted(_sema_imza(an, v) for v in e_all[c])
            ns = sorted(_sema_imza(an, v) for v in n_all[c])
            if es != ns:
                degisen.append(c)
        ozet[baslik] = (len(degisen), len(eklenen), len(kaldirilan))
        detay.append((baslik, e_all, n_all, degisen, eklenen, kaldirilan))

    R.append("  OZET")
    toplam = 0
    for baslik, (d, y, k) in ozet.items():
        toplam += d + y + k
        R.append("    %-11s : degisen %-4d  yeni %-4d  kaldirilan %-4d" % (baslik, d, y, k))
    R.append("    %-11s : %d paket" % ("TOPLAM", toplam))
    R.append("")

    for baslik, e_all, n_all, degisen, eklenen, kaldirilan in detay:
        R.append("=" * 74)
        R.append("  %s" % baslik)
        R.append("=" * 74)

        for c in eklenen:
            e = n_all[c][0]
            R.append("")
            R.append("  + YENI [%s] %s  (%d alan)" % (c, e["name"], e["field_count"]))
            for f in e["fields"][:12]:
                R.append("        [%s] %-22s %s" % (f["index"], f["type"], f["offset"]))

        for c in kaldirilan:
            e = e_all[c][0]
            R.append("")
            R.append("  - KALDIRILDI [%s] %s  (%d alan)" % (c, e["name"], e["field_count"]))

        for c in degisen:
            ev, nv = e_all[c], n_all[c]
            e0, n0 = ev[0], nv[0]
            adim = n0["field_count"] - e0["field_count"]
            R.append("")
            R.append("  ! DEGISTI [%s] %s   alan %d -> %d  (%+d)%s" % (
                c, n0["name"], e0["field_count"], n0["field_count"], adim,
                "" if len(nv) == 1 else "   [%d varyant]" % len(nv)))

            # Ayni kodun birden fazla varyanti olabilir -> varyantlari BOYUTA gore esle
            es_v = sorted(ev, key=lambda v: v["field_count"])
            ns_v = sorted(nv, key=lambda v: v["field_count"])
            for vi in range(max(len(es_v), len(ns_v))):
                a = es_v[vi] if vi < len(es_v) else None
                b = ns_v[vi] if vi < len(ns_v) else None
                if len(ns_v) > 1:
                    R.append("      --- varyant %d ---" % (vi + 1))
                if a is None:
                    R.append("        + yeni varyant (%d alan)" % b["field_count"])
                    continue
                if b is None:
                    R.append("        - kalkan varyant (%d alan)" % a["field_count"])
                    continue
                ei = {f["index"]: f for f in a["fields"]}
                ni = {f["index"]: f for f in b["fields"]}
                for idx in sorted(set(ni) - set(ei)):
                    f = ni[idx]
                    R.append("        + yeni alan [%s]  %s  %s" % (idx, f["type"], f["offset"]))
                for idx in sorted(set(ei) - set(ni)):
                    f = ei[idx]
                    R.append("        - kalkan alan [%s]  %s" % (idx, f["type"]))
                for idx in sorted(set(ei) & set(ni)):
                    x, y = ei[idx], ni[idx]
                    tx, ty = an.canon_type(x["type"]), an.canon_type(y["type"])
                    if tx != ty or x["offset"] != y["offset"]:
                        R.append("        ~ [%s]  %s (%s)  ->  %s (%s)" % (
                            idx, x["type"], x["offset"], y["type"], y["offset"]))

    R.append("")
    R.append("=" * 74)
    R.append("  NOT: Obfuscated sinif adlari (a7k -> a7l gibi) YOK SAYILIR;")
    R.append("       yalnizca GERCEK yapi degisikligi (alan sayisi/tip/offset) gosterilir.")
    R.append("=" * 74)

    return ozet, toplam, "\n".join(R)


ANLAM_EN = {
    "metin (isim / unique name)": "text (name / unique name)",
    "tam sayi": "integer",
    "entity / nesne ID": "entity / object ID",
    "dogru/yanlis": "true/false",
    "uzun tam sayi (genelde ID)": "long integer (usually an ID)",
    "zaman damgasi (ticks)": "timestamp (ticks)",
    "GUID (16 bayt)": "GUID (16 bytes)",
    "entity / nesne ID olabilir": "entity / object ID (may be)",
    "tam sayi dizisi": "integer array",
    "sayi (oyun enum'u)": "number (game enum)",
    "ondalik sayi (oran/mesafe)": "decimal (ratio/distance)",
    "kisa tam sayi": "short integer",
    "bayt (0-255)": "byte (0-255)",
    "KONUM (x, y)": "POSITION (x, y)",
    "ID dizisi": "ID array",
    "bayt dizisi": "byte array",
    "PvP bayragi": "PvP flag",
    "sayi dizisi": "number array",
    "kaynak ID": "resource ID",
    "zaman damgasi": "timestamp",
    "konum (Vector2)": "position (Vector2)",
    "oyuncu ID": "player ID",
    "OYUNCU ADI (nickname)": "PLAYER NAME (nickname)",
    "mevcut HP": "current HP",
    "maks HP": "max HP",
    "chest ID": "chest ID",
    "GUID (bos olabilir)": "GUID (may be empty)",
    "zaman": "time",
    "binek item tip ID (ikon/tier icin)": "mount item type ID (for icon/tier)",
    "? (int; -1 olabilir)": "? (int; may be -1)",
    "rotasyon (derece)": "rotation (degrees)",
    "premium mi": "is premium",
    "sayi": "number",
    "kuyruk sirasi": "queue position",
    "kalan sure": "remaining time",
    "kaynak tipi": "resource type",
    "kalan dayaniklilik": "remaining durability",
    "adet/size": "amount/size",
    "? (float olcu)": "? (float measure)",
    "kisa LC ismi (or. MD_UNDEAD_SOLO_REWARD)": "short LC name (e.g. MD_UNDEAD_SOLO_REWARD)",
    "tam unique name (or. MISTS_GREEN_LOOTCHEST_...)": "full unique name (e.g. MISTS_GREEN_LOOTCHEST_...)",
    "rarity (byte; 1 / 2 / 4 / 6 goruldu)": "rarity (byte; 1/2/4/6 observed)",
    "zaman damgasi (kopya)": "timestamp (copy)",
    "long.MinValue (bos)": "long.MinValue (empty)",
    "10000 (sabit?)": "10000 (constant?)",
    "kisa isim": "short name",
    "int (genelde -1)": "int (usually -1)",
    "rarity kopyasi (5 ile ayni)": "rarity copy (same as 5)",
    "mob tip ID": "mob type ID",
    "FlaggingStatus - TIER DEGIL": "FlaggingStatus - NOT TIER",
    "isim (bos)": "name (empty)",
    "konum": "position",
    "ikinci konum": "second position",
    "zamanlayici": "timer",
    "mevcut (enerji?)": "current (energy?)",
    "maks (enerji?)": "max (energy?)",
    "? (hep 4)": "? (always 4)",
    "tip (gozlemde hep 3)": "type (always 3 observed)",
    "canli HP %": "live HP %",
    "binek item tip ID": "mount item type ID",
    "? (int; maks HP olabilir)": "? (int; may be max HP)",
    "canli HP": "live HP",
    "HP (kucuk olcek, or. 11,95)": "HP (small scale, e.g. 11.95)",
    "rotasyon (312 ile ayni cikti)": "rotation (same output as 312)",
    "mevcut HP (alternatif)": "current HP (alternative)",
    "maks HP (alternatif)": "max HP (alternative)",
    "guild adi": "guild name",
    "ekipman ID dizisi (alternatif)": "equipment ID array (alternative)",
    "ekipman ID dizisi": "equipment ID array",
    "alliance adi": "alliance name",
}


def harita_html(an, schema, yol):
    """Tum paketlerin alan haritasini ARANABILIR tek HTML dosyasi olarak yazar."""
    import html as H

    say_event = sum(1 for _ in schema.get("events", {}))
    say_op = sum(1 for _ in schema.get("operations", {}))
    alan_toplam = 0
    canli_toplam = 0
    kartlar = []

    for bolum, etiket in (("events", "EVENT"), ("operations", "OPERASYON")):
        parca = ['<div class="bolum" id="%s"><h2 data-tr="%s" data-en="%s">%s <small>(%d <span data-tr="kod" data-en="codes">kod</span>)</small></h2>'
                 % (bolum, etiket, ("OPERATIONS" if bolum == "operations" else etiket),
                    etiket, len(schema.get(bolum, {})))]
        for code in sorted(schema.get(bolum, {}), key=int):
            variants = sorted(schema[bolum][code], key=lambda e: e["class"])
            satirlar = []
            arama = [str(code), variants[0]["name"].lower()]
            for vi, e in enumerate(variants, 1):
                if len(variants) > 1:
                    satirlar.append('<tr class="vhead"><td colspan="5">varyant %d/%d &nbsp;·&nbsp; %s &nbsp;·&nbsp; %d alan</td></tr>'
                                    % (vi, len(variants), H.escape(e["class"]), e["field_count"]))
                for f in e["fields"]:
                    anlam, dogrulandi = an.field_meaning(code, f)
                    anlam = str(anlam)
                    alan_toplam += 1
                    if dogrulandi:
                        canli_toplam += 1
                    arama.append(str(f["name"]).lower())
                    arama.append(anlam.lower())
                    anlam_en = ANLAM_EN.get(anlam, anlam)
                    rozet = '<span class="badge" data-tr="CANLI" data-en="LIVE">CANLI</span>' if dogrulandi else ""
                    satirlar.append(
                        '<tr class="%s"><td class="ix">%s</td><td class="fn">%s</td>'
                        '<td class="ty">%s</td><td class="mn">'
                        '<span class="mtx" data-tr="%s" data-en="%s">%s</span> %s</td>'
                        '<td class="of">%s</td></tr>'
                        % ("ver" if dogrulandi else "", f["index"], H.escape(str(f["name"])),
                           H.escape(str(f["type"])), H.escape(anlam), H.escape(anlam_en),
                           H.escape(anlam), rozet, H.escape(str(f["offset"]))))

            kartlar.append(
                '<div class="pkt" data-s="%s">'
                '<div class="ph" onclick="ac(this)">'
                '<span class="code">%s</span><span class="name">%s</span>'
                '<span class="meta"><span data-tr="%d alan" data-en="%d fields">%d alan</span>%s</span>'
                '<span class="chev">+</span></div>'
                '<div class="pb"><table><thead><tr>'
                '<th data-tr="#" data-en="#">#</th>'
                '<th data-tr="alan" data-en="field">alan</th>'
                '<th data-tr="tip" data-en="type">tip</th>'
                '<th data-tr="anlam" data-en="meaning">anlam</th>'
                '<th data-tr="offset" data-en="offset">offset</th>'
                '</tr></thead><tbody>%s</tbody></table></div></div>'
                % (H.escape(" ".join(arama)), code, H.escape(variants[0]["name"]),
                   sum(v["field_count"] for v in variants),
                   sum(v["field_count"] for v in variants),
                   sum(v["field_count"] for v in variants),
                   ("" if len(variants) == 1 else
                    ' <span data-tr="· %d varyant" data-en="· %d variants">· %d varyant</span>'
                    % (len(variants), len(variants), len(variants))),
                   "".join(satirlar)))
        parca.append("</div>")
        kartlar.append("".join(parca))

    css = """
*{box-sizing:border-box}
body{margin:0;background:#0a0a0a;color:#f1f3f5;font:13px/1.45 "Segoe UI",Tahoma,sans-serif}
header{position:sticky;top:0;z-index:9;background:#0e0e0ef2;border-bottom:1px solid #1e293b;
       padding:12px 18px;backdrop-filter:blur(4px)}
h1{margin:0 0 6px;font-size:17px}
h1 small{color:#868e96;font-weight:400;font-size:12px}
.stats{display:flex;gap:14px;flex-wrap:wrap;color:#868e96;font-size:12.5px;margin-bottom:9px}
.stats b{color:#a29bfe}
.bar{display:flex;gap:8px;flex-wrap:wrap;align-items:center}
input#q{flex:1;min-width:220px;padding:8px 12px;border-radius:10px;background:#161616;
        border:1px solid #1e293b;color:#f1f3f5;font-size:13px;outline:none}
input#q:focus{border-color:#6c5ce7}
button{padding:7px 12px;border-radius:10px;background:#161616;border:1px solid #1e293b;
       color:#f1f3f5;font-size:12.5px;cursor:pointer}
button:hover{background:#1c1c1c;border-color:#6c5ce7}
button.on{background:#6c5ce7;border-color:#6c5ce7;color:#fff}
main{max-width:1150px;margin:14px auto;padding:0 14px 60px}
.bolum h2{font-size:14px;color:#a29bfe;border-bottom:1px solid #1e293b;padding-bottom:5px;margin:22px 0 8px}
.bolum h2 small{color:#495057}
.pkt{background:#0e0e0e;border:1px solid #1e293b;border-radius:12px;margin:6px 0;overflow:hidden}
.ph{display:flex;gap:10px;align-items:center;padding:9px 13px;cursor:pointer;user-select:none}
.ph:hover{background:#121212}
.code{background:#6c5ce7;color:#fff;border-radius:7px;padding:1px 8px;font-size:12px;min-width:44px;text-align:center}
.name{font-weight:600}
.meta{color:#868e96;font-size:12px;margin-left:auto}
.chev{color:#6c5ce7;font-weight:700;width:14px;text-align:center}
.pb{display:none;border-top:1px solid #1e293b;padding:4px 8px 8px}
.pkt.acik .pb{display:block}
table{width:100%;border-collapse:collapse;font-size:12.5px}
th{text-align:left;color:#868e96;font-weight:500;padding:5px 8px;border-bottom:1px solid #1e293b}
td{padding:4px 8px;border-bottom:1px solid #141414;vertical-align:top}
td.ix{color:#868e96;width:44px}
td.fn{color:#f1f3f5;font-family:Consolas,monospace}
td.ty{color:#00cec9;font-family:Consolas,monospace;width:150px}
td.mn{color:#adb5bd}
td.of{color:#495057;font-family:Consolas,monospace;width:70px}
tr.ver td.mn{color:#51cf66}
.badge{background:#51cf66;color:#08210f;border-radius:6px;padding:0 5px;font-size:10.5px;font-weight:700;margin-left:5px}
tr.vhead td{background:#121212;color:#a29bfe;font-size:12px;padding:4px 8px}
#sayac{color:#868e96;font-size:12.5px}
.gizli{display:none!important}
"""
    js = """
var q=document.getElementById('q');
var sadeceCanli=false, bolum='hepsi', DIL='tr';
var M={tr:{paket:'paket'},en:{paket:'packets'}};
function ac(el){ el.parentNode.classList.toggle('acik'); }
function dil(l){
  DIL=l;
  document.getElementById('bTR').classList.toggle('on',l==='tr');
  document.getElementById('bEN').classList.toggle('on',l==='en');
  document.documentElement.lang=l;
  document.querySelectorAll('[data-tr][data-en]').forEach(function(e){
    e.textContent = (l==='tr') ? e.getAttribute('data-tr') : e.getAttribute('data-en');
  });
  var inp=document.getElementById('q');
  inp.placeholder = (l==='tr') ? inp.getAttribute('data-tr') : inp.getAttribute('data-en');
  try{ localStorage.setItem('nw_dil',l); }catch(e){}
  uygula();
}
function uygula(){
  var s=(q.value||'').toLowerCase().trim();
  var n=0, gorunen=0;
  document.querySelectorAll('.pkt').forEach(function(p){
    var okS = !s || (p.dataset.s||'').indexOf(s)>=0;
    var okC = !sadeceCanli || p.querySelector('tr.ver');
    var okB = (bolum==='hepsi') || (p.closest('.bolum') && p.closest('.bolum').id===bolum);
    if(okS&&okC&&okB){ p.classList.remove('gizli'); gorunen++; } else { p.classList.add('gizli'); }
    n++;
  });
  document.getElementById('sayac').textContent = gorunen + ' / ' + n + ' ' + M[DIL].paket;
}
q.addEventListener('input',uygula);
function hepsi(ac_){ document.querySelectorAll('.pkt').forEach(function(p){ p.classList.toggle('acik',ac_); }); }
function canli(){ sadeceCanli=!sadeceCanli; document.getElementById('bCanli').classList.toggle('on',sadeceCanli); uygula(); }
function bolumSec(b,btn){ bolum=b;
  document.querySelectorAll('.bbtn').forEach(function(x){x.classList.remove('on')});
  btn.classList.add('on'); uygula(); }
window.addEventListener('DOMContentLoaded',function(){
  var kayit='tr';
  try{ kayit = localStorage.getItem('nw_dil') || 'tr'; }catch(e){}
  dil(kayit);
});
"""
    belge = []
    belge.append("<!DOCTYPE html><html lang='tr'><head><meta charset='utf-8'>")
    belge.append("<meta name='viewport' content='width=device-width,initial-scale=1'>")
    belge.append("<title>Nightwatch - Alan Haritasi</title><style>%s</style></head><body>" % css)
    belge.append("<header><h1>Nightwatch - <span data-tr='Alan Haritasi' data-en='Field Map'>Alan Haritasi</span> "
                 "<small><span data-tr=\"dump.cs'ten otomatik uretildi\" data-en='auto-generated from dump.cs'>dump.cs'ten otomatik uretildi</span> · %s</small></h1>"
                 % SURUM)
    belge.append("<div class='stats'>"
                 "<span>EVENT <b>%d</b></span><span data-tr='OPERASYON' data-en='OPERATIONS'>OPERASYON</span> <b>%d</b></span>"
                 "<span><span data-tr='TOPLAM ALAN' data-en='TOTAL FIELDS'>TOPLAM ALAN</span> <b>%d</b></span>"
                 "<span><span data-tr='CANLI DOGRULANMIS' data-en='LIVE-VERIFIED'>CANLI DOGRULANMIS</span> <b>%d</b></span>"
                 "<span id='sayac'></span></div>" % (say_event, say_op, alan_toplam, canli_toplam))
    belge.append("<div class='bar'>"
                 "<input id='q' data-tr='ara: kod (526), isim (NewMists), alan (F0), anlam (konum) ...' "
                 "data-en='search: code (526), name (NewMists), field (F0), meaning (position) ...' "
                 "placeholder='ara: kod (526), isim (NewMists), alan (F0), anlam (konum) ...'>"
                 "<button class='bbtn on' onclick=\"bolumSec('hepsi',this)\"><span data-tr='Hepsi' data-en='All'>Hepsi</span></button>"
                 "<button class='bbtn' onclick=\"bolumSec('events',this)\">EVENT</button>"
                 "<button class='bbtn' onclick=\"bolumSec('operations',this)\">OPERASYON</button>"
                 "<button id='bCanli' onclick='canli()'><span data-tr='Sadece CANLI' data-en='Verified only'>Sadece CANLI</span></button>"
                 "<button onclick='hepsi(true)'><span data-tr='Tumunu Ac' data-en='Expand all'>Tumunu Ac</span></button>"
                 "<button onclick='hepsi(false)'><span data-tr='Tumunu Kapat' data-en='Collapse all'>Tumunu Kapat</span></button>"
                 "<button id='bTR' class='on' onclick=\"dil('tr')\">TR</button>"
                 "<button id='bEN' onclick=\"dil('en')\">EN</button></div></header>")
    belge.append("<main>%s</main>" % "".join(kartlar))
    belge.append("<script>%s</script></body></html>" % js)

    with open(yol, "w", encoding="utf-8") as f:
        f.write("".join(belge))

    return say_event, say_op, alan_toplam, canli_toplam


def main():
    t0 = time.time()
    print()
    print("  NIGHTWATCH - TEK DOSYA GUNCELLEME")
    print("  Klasor : " + KOK)
    print("  Surum  : " + SURUM)

    bloklar = _bloklari_oku()
    if "ANALIZ" not in bloklar or "UPDATER" not in bloklar:
        hata("Gomulu bloklar okunamadi (dosya bozulmus olabilir).")
        return 1
    bilgi("Gomulu araclar okundu (analiz + veri guncelleyici)")

    # ---------------------------------------------------------------- 1) dump
    baslik("1) dump.cs ARANIYOR")
    dump = None
    for klasor in (EXTRA, KOK):
        for ad in ("dump.cs", "dump.txt", "dump.cs.txt"):
            p = os.path.join(klasor, ad)
            if os.path.exists(p):
                dump = p
                break
        if dump:
            break
    if not dump:
        hata("dump.cs bulunamadi!")
        print("         Bu dosyanin yanina ya da Extra\\ icine koy: dump.cs")
        return 1
    ok("dump : %s  (%.1f MB)" % (dump, os.path.getsize(dump) / 1024.0 / 1024.0))

    # analiz.py dump'i <kok>\Extra\ icinde arar -> orada yoksa kopyala
    hedef_dump = os.path.join(EXTRA, os.path.basename(dump))
    if os.path.abspath(dump) != os.path.abspath(hedef_dump):
        if not os.path.isdir(EXTRA):
            os.makedirs(EXTRA)
        shutil.copy2(dump, hedef_dump)
        bilgi("dump Extra\\ icine kopyalandi")
        dump = hedef_dump

    # ---------------------------------------------------------------- 2) analiz
    try:
        an = _modul_yukle("analiz_gomulu", bloklar["ANALIZ"], os.path.join(EXTRA, "analiz.py"))
    except Exception as ex:
        hata("analiz modulu yuklenemedi: %s" % ex)
        return 1

    tablo = os.path.join(EXTRA, "paket_semasi.json")
    eski = os.path.join(EXTRA, "paket_semasi_eski.json")
    if os.path.exists(tablo):
        shutil.copy2(tablo, eski)

    baslik("2) FARK (mevcut tablo <-> yeni dump)")
    if os.path.exists(eski):
        try:
            an.cmd_fark()
        except SystemExit:
            pass
        except Exception as ex:
            uyari("fark atlandi: %s" % ex)
    else:
        bilgi("ilk calistirma -> karsilastirilacak eski tablo yok, fark atlandi")

    # ------------------------------------------------ 2b) DUMP KARSILASTIRMA
    baslik("2b) DEGISEN PAKETLER (eski dump <-> yeni dump)")
    argv = sys.argv[1:]
    eski_dump = None
    if "--eski" in argv:
        i = argv.index("--eski")
        if i + 1 < len(argv):
            eski_dump = os.path.abspath(argv[i + 1])
    if not eski_dump:
        eski_dump = _eski_dump_bul(dump)

    if not eski_dump or not os.path.exists(eski_dump):
        bilgi("eski dump bulunamadi -> karsilastirma atlandi")
        bilgi("Kullanmak icin:  python Update.py --eski C:\\yol\\eski_dump.cs")
        bilgi("Veya eski dump'i  Extra\\dump_eski.cs  adiyla koy.")
    else:
        bilgi("eski dump : %s" % eski_dump)
        bilgi("yeni dump : %s" % dump)
        try:
            ozet, toplam, rapor = karsilastir(an, eski_dump, dump)
            rapor_yolu = os.path.join(KOK, "DEGISEN-PAKETLER.txt")
            with open(rapor_yolu, "w", encoding="utf-8") as fh:
                fh.write(rapor)
            for baslik_ad, (d, y, k) in ozet.items():
                print("  %-11s : degisen %-4d  yeni %-4d  kaldirilan %-4d" % (baslik_ad, d, y, k))
            if toplam == 0:
                ok("iki surum ARASINDA fark yok (yapi ayni)")
            else:
                uyari("%d paket farkli -> detay: %s" % (toplam, rapor_yolu))
                # ekrana ilk birkac degisikligi de bas
                satirlar = rapor.splitlines()
                goster = [s for s in satirlar if s.strip().startswith("! DEGISTI") or s.strip().startswith("+ YENI") or s.strip().startswith("- KALDIRILDI")]
                for s in goster[:15]:
                    print("     " + s.strip())
                if len(goster) > 15:
                    print("     ... (+%d paket daha, tamami rapor dosyasinda)" % (len(goster) - 15))
        except Exception as ex:
            uyari("karsilastirma yapilamadi: %s" % ex)

    baslik("3) ENUM + PARAMETRE TABLOSU (paket_semasi.json)")
    try:
        an.cmd_uret()
    except SystemExit:
        pass
    except Exception as ex:
        hata("tablo uretilemedi: %s" % ex)
        return 1

    baslik("4) SEMBOLLER (Events.g.cs / Operations.g.cs / PacketMeta.g.cs)")
    try:
        an.cmd_csharp()
    except SystemExit:
        pass
    except Exception as ex:
        hata("semboller uretilemedi: %s" % ex)
        return 1

    # ------------------------------------------------ 4b) ALAN HARITASI (HTML)
    baslik("4b) ALAN HARITASI (aranabilir HTML)")
    try:
        *_, sema, _un = an.load()
        harita_yolu = os.path.join(KOK, "ALAN-HARITASI.html")
        se, so, sa, sc = harita_html(an, sema, harita_yolu)
        ok("EVENT %d | OPERASYON %d | alan %d | CANLI dogrulanmis %d" % (se, so, sa, sc))
        ok("yazildi: %s  (%.0f KB)" % (harita_yolu, os.path.getsize(harita_yolu) / 1024.0))
        bilgi("tarayicida ac, arama kutusuna kod/isim/alan yaz (or. 526, NewMists, konum)")
    except Exception as ex:
        uyari("alan haritasi uretilemedi: %s" % ex)

    # ---------------------------------------------------------------- 3) derleme
    proje = os.path.join(KOK, "Nightwatch", "Nightwatch.csproj")
    baslik("5) DERLEME")
    if os.path.exists(proje):
        bilgi("dotnet publish calisiyor (1-3 dakika)...")
        cikti = subprocess.run(
            ["dotnet", "publish", proje, "-c", "Release", "-r", "win-x64",
             "--self-contained", "true", "-p:PublishSingleFile=true",
             "-p:IncludeNativeLibrariesForSelfExtract=false",
             "-o", os.path.join(KOK, "ReleaseBuildClean")],
            capture_output=True, text=True)
        if cikti.returncode == 0:
            ok("derleme tamam")
        else:
            uyari("derleme basarisiz (kod yine de uretildi)")
            for satir in (cikti.stdout or "").splitlines():
                if "error" in satir.lower():
                    print("         " + satir.strip()[:150])
    else:
        bilgi("kaynak proje yok -> derleme atlandi (sembol + veri uretildi)")

    # ---------------------------------------------------------------- 4) veri
    baslik("6) HELPER .JSON LAR (items / mobs / localization / zones / spells)")
    upd_dir = os.path.join(EXTRA, "UpdateHelperV1.4")
    if not os.path.isdir(upd_dir):
        os.makedirs(upd_dir)
    eski_cwd = os.getcwd()
    try:
        os.chdir(upd_dir)
        up = _modul_yukle("updater_gomulu", bloklar["UPDATER"], os.path.join(upd_dir, "Update.py"))
        up.main()
    except SystemExit:
        pass
    except Exception as ex:
        uyari("veri guncellemesi kismen atlandi: %s" % ex)
    finally:
        os.chdir(eski_cwd)

    helper = os.path.join(upd_dir, "Helper")
    app_helper = os.path.join(KOK, "Nightwatch", "Assets", "Helper")
    if os.path.isdir(helper):
        if not os.path.isdir(app_helper):
            os.makedirs(app_helper)
        n = 0
        for ad in os.listdir(helper):
            kaynak = os.path.join(helper, ad)
            if os.path.isfile(kaynak):
                shutil.copy2(kaynak, os.path.join(app_helper, ad))
                n += 1
        ok("veri uygulamaya kopyalandi -> Nightwatch\\Assets\\Helper  (%d dosya)" % n)

    # ---------------------------------------------------------------- 5) exe
    baslik("7) EXE KOPYALAMA (HEDEF.txt)")
    hedef_txt = os.path.join(KOK, "HEDEF.txt")
    exe = os.path.join(KOK, "ReleaseBuildClean", "Nightwatch.exe")
    if not os.path.exists(hedef_txt):
        with open(hedef_txt, "w", encoding="ascii") as f:
            f.write("C:\\Albion\\Nightwatch")
        bilgi("HEDEF.txt olusturuldu (icine oyun klasorunu yaz)")
    else:
        hedef = ""
        with open(hedef_txt, "r", encoding="utf-8", errors="replace") as f:
            hedef = f.read().strip()
        if not hedef:
            bilgi("HEDEF.txt bos -> kopyalama atlandi")
        elif not os.path.isdir(hedef):
            uyari("oyun klasoru yok: %s" % hedef)
        elif not os.path.exists(exe):
            uyari("derlenmis exe yok -> kopyalama atlandi")
        else:
            shutil.copy2(exe, os.path.join(hedef, "Nightwatch.exe"))
            ok("exe kopyalandi -> %s" % hedef)

    baslik("BITTI  (%.0f saniye)" % (time.time() - t0))
    print("  Uretilenler:")
    for p in (os.path.join(EXTRA, "paket_semasi.json"),
              os.path.join(KOK, "AlbionDataHandlersNET8", "Packets", "Events.g.cs"),
              os.path.join(KOK, "AlbionDataHandlersNET8", "Packets", "Operations.g.cs"),
              os.path.join(KOK, "AlbionDataHandlersNET8", "Packets", "PacketMeta.g.cs"),
              app_helper):
        if os.path.exists(p):
            ok(p.replace(KOK, "."))
    print()
    return 0


_BLOB = r"""
:::B64:ANALIZ:START
IyEvdXNyL2Jpbi9lbnYgcHl0aG9uMwojIC0qLSBjb2Rpbmc6IHV0Zi04IC0qLQoiIiIKTklHSFRX
QVRDSCAtIFRFSyBET1NZQSBQQUtFVCBBTkFMSVogQVJBQ0kKPT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT0KCkJ1IGRvc3lhLCBlc2tpZGVuIGF5cmkgYXlyaSBkdXJhbiB1
YyBzY3JpcHRpbiBCSVJMRVNNSVMgaGFsaWRpcjoKICAgIHBha2V0X3NlbWFzaS5weSAgKyAga29k
X2RlbmV0aW0ucHkgICsgIGthcnNpbGFzdGlyLnB5CgpLVUxMQU5JTSAoYnUgZG9zeWFuaW4gYnVs
dW5kdWd1IGtsYXNvcmRlbik6CiAgICBweXRob24gYW5hbGl6LnB5IC0teWFyZGltCgogIFVSRVRJ
TSAvIERPS1VNQU4KICAgIC0tdXJldCAgICAgICAgICAgICAgICAgZHVtcC5jcyAtPiBwYWtldF9z
ZW1hc2kuanNvbiAgKHRhYmxvKQogICAgLS1tZCAgICAgICAgICAgICAgICAgICBkdW1wLmNzIC0+
IEFMQU4tSEFSSVRBU0ktVEFNLm1kICAodHVtIGFsYW4gc2lyYXNpKQogICAgLS1zb3psdWsgICAg
ICAgICAgICAgICBUVU0ga29kbGFyIGljaW4gcGFyYW1ldHJlIHNvemx1Z3UgKFBBUkFNRVRSRUxF
Ui5tZCArIC50c3YpCgogIEtBUlNJTEFTVElSTUEgLyBET0dSVUxBTUEKICAgIC0tZmFyayAgICAg
ICAgICAgICAgICAgZXNraSB0YWJsbyAocGFrZXRfc2VtYXNpX2Vza2kuanNvbikgPC0+IHllbmkg
ZHVtcAogICAgLS1kb2dydWxhIFtMT0ddICAgICAgICBjYW5saSB5YWthbGFtYSBsb2d1IDwtPiB0
YWJsbyAgKHZhcnNheWlsYW46IGxvZy9ldmVudF93YXRjaC5sb2cpCiAgICAtLWVudW0gICAgICAg
ICAgICAgICAgIGR1bXAgZW51bSA8LT4gQyMgZW51bSBkb3N5YWxhcmkKICAgIC0tbW9iZGIgICAg
ICAgICAgICAgICAgbW9ic19FTl9taW4uanNvbiA8LT4gVFIvUlUvWkggKGluZGV4IGtheW1hc2kp
CiAgICAtLWtvZCAgICAgICAgICAgICAgICAgIGtvZCA8LT4gdGFibG8gZGVuZXRpbWkgKGhhbmdp
IG1ldG90IHlhbmxpcyBpbmRla3Mgb2t1eW9yKQogICAgLS1rb2QtaGVwc2kgICAgICAgICAgICBk
ZW5ldGltZGUgVVlVU0FOIG9rdW1hbGFyaSBkYSBnb3N0ZXIKICAgIC0tbWV0b3QgICAgICAgICAg
ICAgICAgbWV0b3QgLT4gZXZlbnQgZXNsZW1lc2luaSB5YXpkaXIKCiAgQVJBTUEKICAgIC0tYXJh
IEtFTElNRSAgICAgICAgICAga29kIGFkaW5kYS9zaW5pZiBhZGluZGEgYXJhCgogIEJJTEdJCiAg
ICAtLWJpbGdpICAgICAgICAgICAgICAgIGJpbGlubWVzaSBnZXJla2VubGVyICgzIGthdG1hbiwg
dGVsIGZvcm1hdGksCiAgICAgICAgICAgICAgICAgICAgICAgICAgIGRvZ3J1bGFubWlzIGtvZGxh
ciwgZ3VuY2VsbGVtZSBhZGltbGFyaSwgZG9zeWFsYXIpCgogIEtPRCBVUkVUSU1JCiAgICAtLWNz
aGFycCAgICAgICAgICAgICAgIHBha2V0IG1vZGVsbGVyaW5pIEMjIG9sYXJhayB1cmV0CiAgICAg
ICAgICAgICAgICAgICAgICAgICAgIChBbGJpb25EYXRhSGFuZGxlcnNORVQ4XFxQYWNrZXRzXFxF
dmVudHMuZy5jcyArIE9wZXJhdGlvbnMuZy5jcykKICAgIC0tdWkgICAgICAgICAgICAgICAgICAg
SW1HdWkgYXJheXV6dW51IEhUTUwnZSBjZXZpciAoRXh0cmFcXGd1aS5odG1sKQogICAgICAgICAg
ICAgICAgICAgICAgICAgICBVSSBrb2R1bnUgZGVnaXN0aXJkaWt0ZW4gc29ucmEgY2FsaXN0aXIg
LT4gdGFyYXlpY2lkYSBGNQoKICBUT1BMVQogICAgLS1oZXBzaSAgICAgICAgICAgICAgICB1cmV0
ICsgbWQgKyBzb3psdWsgKyBkb2dydWxhICsgZW51bSArIGtvZAoKTk9UTEFSCiAgLSBDaWt0aWxh
ciBidSBkb3N5YW5pbiB5YW5pbmEgeWF6aWxpciAoRXh0cmEga2xhc29ydSkuCiAgLSBkdW1wIGRv
c3lhc2kgJ2R1bXAudHh0JywgJ2R1bXAuY3MudHh0JyB2ZXlhICdkdW1wLmNzJyBhZGl5bGEgYnVs
dW51ci4KICAtICctLScgaWxlIGJhc2xhbWF5YW4gZXNraSBrb211dGxhciBkYSBjYWxpc2lyICh1
cmV0LCBtZCwgZmFyaywgc296bHVrLAogICAgZG9ncnVsYSwgYXJhLCBlbnVtLCBtb2JkYiwga29k
LCBvcm5laywgaGVwc2kpLgoiIiIKCmltcG9ydCBkYXRldGltZQppbXBvcnQganNvbgppbXBvcnQg
b3MKaW1wb3J0IHJlCmltcG9ydCBzeXMKCiMgPT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PQojIFlPTExBUgojID09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT0KU0NSSVBUX0RJUiA9IG9zLnBhdGguZGlybmFtZShvcy5wYXRoLmFic3BhdGgoX19m
aWxlX18pKSAgICMgLi4uXEFcRXh0cmEKUk9PVCA9IG9zLnBhdGguZGlybmFtZShTQ1JJUFRfRElS
KSAgICAgICAgICAgICAgICAgICAgICAgICMgLi4uXEEKCgpkZWYgZmluZF9kdW1wKCk6CiAgICAi
IiJkdW1wIGRvc3lhc2luaSBidWwuIE5PVDogLmNzIHV6YW50aWxpIGRvc3lhIHByb2plbGVyIHRh
cmFmaW5kYW4KICAgIGRlcmxlbm1leWUgY2FsaXNpbGlwIGJ1aWxkJ2kgcGF0bGF0YWJpbGlyIC0+
ICdkdW1wLnR4dCcgdGVyY2loIGVkaWxpci4iIiIKICAgIGZvciBuYW1lIGluICgiZHVtcC50eHQi
LCAiZHVtcC5jcy50eHQiLCAiZHVtcC5jcyIpOgogICAgICAgIHAgPSBvcy5wYXRoLmpvaW4oUk9P
VCwgIkV4dHJhIiwgbmFtZSkKICAgICAgICBpZiBvcy5wYXRoLmV4aXN0cyhwKToKICAgICAgICAg
ICAgcmV0dXJuIHAKICAgIHJldHVybiBvcy5wYXRoLmpvaW4oUk9PVCwgIkV4dHJhIiwgImR1bXAu
dHh0IikKCgpEVU1QID0gZmluZF9kdW1wKCkKT1VUID0gb3MucGF0aC5qb2luKFNDUklQVF9ESVIs
ICJwYWtldF9zZW1hc2kuanNvbiIpCk1EID0gb3MucGF0aC5qb2luKFNDUklQVF9ESVIsICJBTEFO
LUhBUklUQVNJLVRBTS5tZCIpCkhFTFBFUiA9IG9zLnBhdGguam9pbihST09ULCAiTmlnaHR3YXRj
aCIsICJBc3NldHMiLCAiSGVscGVyIikKS09EX0tPSyA9IG9zLnBhdGguam9pbihST09ULCAiQWxi
aW9uRGF0YUhhbmRsZXJzTkVUOCIpCkVOVU1fRElSID0gb3MucGF0aC5qb2luKEtPRF9LT0ssICJF
bnVtcyIpCgpDU19FTlVNX0ZJTEVTID0gWwogICAgb3MucGF0aC5qb2luKEVOVU1fRElSLCAiRXZl
bnRDb2Rlcy5jcyIpLAogICAgb3MucGF0aC5qb2luKEVOVU1fRElSLCAiUmVxdWVzdENvZGVzLmNz
IiksCiAgICBvcy5wYXRoLmpvaW4oRU5VTV9ESVIsICJSZXNwb25zZUNvZGVzLmNzIiksCiAgICBv
cy5wYXRoLmpvaW4oUk9PVCwgIkFPU25pZmZlck5FVCIsICJBbGJpb25QYWNrZXRzIiwgIk9wZXJh
dGlvbkNvZGVzLmNzIiksCl0KCiMgUmVxdWVzdENvZGVzIC8gUmVzcG9uc2VDb2RlcyBiaWxlcmVr
IEFMVCBLVU1FIG9sYXJhayB0YXNhcmxhbm1pc3RpcjoKIyBpY2xlcmluZGUgb3l1bnVuIDU1NSBv
cGVyYXN5b251bnVuIHRhbWFtaSBvbG1hayB6b3J1bmRhIGRlZ2lsLgpTVUJTRVRfRU5VTVMgPSAo
IlJlcXVlc3RDb2RlcyIsICJSZXNwb25zZUNvZGVzIikKCiMgQmlsZXJlayBmYXJrbGkgaXNpbSBr
b3lkdWd1bXV6IHRha21hIGFkbGFyIChDIyBhZGkgLT4gb3l1bmRha2kgYWQpCk5BTUVfQUxJQVNF
UyA9IHsKICAgICJSZXNwb25zZUNvZGVzIjogewogICAgICAgICJQbGF5ZXJKb2luaW5nTWFwIjog
IkpvaW4iLCAgICAgICAgICAgICAgIyBveXVuOiBKb2luID0gMgogICAgICAgICJQbGF5ZXJDaGFu
Z2VDbHVzdGVyIjogIkNoYW5nZUNsdXN0ZXIiLCAgIyBveXVuOiBDaGFuZ2VDbHVzdGVyID0gNDEK
ICAgIH0sCn0KCiMgPT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PQojIFNPWkxVS0xFUgojID09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0KIyBH
ZXJjZWsgb3l1biBkb2t1bXUgKyBDQU5MSSBveXVuIGlsZSBET0dSVUxBTk1JUyBhbmxhbWxhcgoj
IChldmVudCBrb2R1IC0+IGluZGVrcyAtPiBhbmxhbSkKS05PV04gPSB7CiAgICAiNDAiOiB7IjAi
OiAia2F5bmFrIElEIiwgIjUiOiAia2F5bmFrIHRpcGkiLCAiNyI6ICJ0aWVyIiwgIjgiOiAia29u
dW0gKFZlY3RvcjIpIiwKICAgICAgICAgICAiOSI6ICJrYWxhbiBkYXlhbmlrbGlsaWsiLCAiMTAi
OiAiYWRldC9zaXplIiwgIjExIjogImVuY2hhbnQifSwKICAgICI0NyI6IHsiMCI6ICJlbnRpdHkg
SUQiLCAiMSI6ICJlbmNoYW50In0sCiAgICAiNjEiOiB7IjAiOiAia2F5bmFrIElEIn0sCiAgICAi
MTIzIjogeyIwIjogImVudGl0eSBJRCIsICIxIjogIm1vYiB0aXAgSUQiLCAiMiI6ICJGbGFnZ2lu
Z1N0YXR1cyAtIFRJRVIgREVHSUwiLAogICAgICAgICAgICAiNiI6ICJpc2ltIChib3MpIiwgIjci
OiAia29udW0iLCAiOCI6ICJpa2luY2kga29udW0iLCAiOSI6ICJ6YW1hbiIsCiAgICAgICAgICAg
ICIxMyI6ICJtZXZjdXQgSFAiLCAiMTQiOiAibWFrcyBIUCIsICIxNyI6ICJ6YW1hbmxheWljaSIs
CiAgICAgICAgICAgICIxOCI6ICJtZXZjdXQgKGVuZXJqaT8pIiwgIjE5IjogIm1ha3MgKGVuZXJq
aT8pIiwgIjIxIjogIj8gKGhlcCA0KSIsCiAgICAgICAgICAgICIyMiI6ICJ6YW1hbiIsICIzMCI6
ICI/IiwgIjM0IjogIj8gKHJhcml0eT8pIn0sCiAgICAiMzI2IjogeyIwIjogInByZW1pdW0gbWki
LCAiMSI6ICJzYXlpIiwgIjIiOiAia3V5cnVrIHNpcmFzaSIsICIzIjogImthbGFuIHN1cmUifSwK
ICAgICMgTmV3TG9vdENoZXN0IC0gQ0FOTEkgb3l1bmRhIGRvZ3J1bGFuZGkgKG1pc3RzIHNhbmRp
a2xhcmksIDI1Mj0zOTMpCiAgICAiMzkzIjogeyIwIjogImNoZXN0IElEIiwgIjEiOiAia29udW0g
KFZlY3RvcjIpIiwgIjIiOiAiPyAoZmxvYXQgb2xjdSkiLAogICAgICAgICAgICAiMyI6ICJraXNh
IExDIGlzbWkgKG9yLiBNRF9VTkRFQURfU09MT19SRVdBUkQpIiwKICAgICAgICAgICAgIjQiOiAi
dGFtIHVuaXF1ZSBuYW1lIChvci4gTUlTVFNfR1JFRU5fTE9PVENIRVNUXy4uLikiLAogICAgICAg
ICAgICAiNSI6ICJyYXJpdHkgKGJ5dGU7IDEgLyAyIC8gNCAvIDYgZ29ydWxkdSkiLCAiNiI6ICJ6
YW1hbiBkYW1nYXNpIiwKICAgICAgICAgICAgIjciOiAiemFtYW4gZGFtZ2FzaSAoa29weWEpIiwg
IjgiOiAiR1VJRCAoYm9zIG9sYWJpbGlyKSIsCiAgICAgICAgICAgICIxMCI6ICJHVUlEIChib3Mg
b2xhYmlsaXIpIiwgIjEzIjogImxvbmcuTWluVmFsdWUgKGJvcykiLAogICAgICAgICAgICAiMTYi
OiAiMTAwMDAgKHNhYml0PykiLCAiMTciOiAiYm9vbCIsICIxOCI6ICJraXNhIGlzaW0iLAogICAg
ICAgICAgICAiMjAiOiAiR1VJRCAoc3RyaW5nKSIsICIyMSI6ICJpbnQgKGdlbmVsZGUgLTEpIiwK
ICAgICAgICAgICAgIjIzIjogInJhcml0eSBrb3B5YXNpICg1IGlsZSBheW5pKSIsICIyNiI6ICJi
b29sIiwgIjI4IjogImJvb2wiLAogICAgICAgICAgICAiMjkiOiAic2hvcnQiLCAiMzEiOiAiRmxh
Z2dpbmdTdGF0dXMifSwKICAgICMgTmV3TW91bnRPYmplY3QgLSBDQU5MSSBkb2dydWxhbmRpIChi
aW5la2xlcjsgc2FoaXBzaXogYmluZWsgPSB5YWtpbmRhIG95dW5jdSB2YXIpCiAgICAiMzEyIjog
eyIwIjogImVudGl0eSBJRCIsICIxIjogImJpbmVrIGl0ZW0gdGlwIElEIChpa29uL3RpZXIgaWNp
bikiLAogICAgICAgICAgICAiMiI6ICI/IChpbnQ7IC0xIG9sYWJpbGlyKSIsICIzIjogImtvbnVt
IChWZWN0b3IyKSIsCiAgICAgICAgICAgICI0IjogInJvdGFzeW9uIChkZXJlY2UpIiwgIjUiOiAi
R1VJRCIsICI2IjogIkZsYWdnaW5nU3RhdHVzIiwKICAgICAgICAgICAgIjciOiAidGlwIChnb3ps
ZW1kZSBoZXAgMykiLCAiOCI6ICI/IChpbnQpIiwgIjkiOiAiR1VJRCIsICIxMCI6ICJHVUlEIiwK
ICAgICAgICAgICAgIjExIjogIj8gKGludCkiLCAiMTIiOiAiPyAoaW50KSIsICIxMyI6ICI/IChp
bnQpIiwKICAgICAgICAgICAgIjE0IjogImNhbmxpIEhQICUiLCAiMTUiOiAiPyAoaW50KSIsICIx
NiI6ICI/IChmbG9hdCkiLCAiMTciOiAiPyAoZmxvYXQpIn0sCiAgICAjIE1vdW50ZWQgLSBDQU5M
SSBkb2dydWxhbmRpIChveXVuY3VudW4gYmluZGlnaSBiaW5laykKICAgICIyMTEiOiB7IjAiOiAi
b3l1bmN1IElEIiwgIjEiOiAiemFtYW4gZGFtZ2FzaSIsICIyIjogImJpbmVrIGl0ZW0gdGlwIElE
IiwKICAgICAgICAgICAgIjMiOiAiPyAoaW50OyBtYWtzIEhQIG9sYWJpbGlyKSIsICI0IjogImNh
bmxpIEhQIiwKICAgICAgICAgICAgIjUiOiAiSFAgKGt1Y3VrIG9sY2VrLCBvci4gMTEsOTUpIiwg
IjYiOiAiemFtYW4gZGFtZ2FzaSIsCiAgICAgICAgICAgICI3IjogImJvb2wiLCAiOCI6ICJib29s
IiwgIjEwIjogInJvdGFzeW9uICgzMTIgaWxlIGF5bmkgY2lrdGkpIn0sCiAgICAjIE5ld0NoYXJh
Y3RlciAtIE9ZVU5DVSBwYWtldGkuIEluZGVrc2xlciBDQUxJU0FOIFBsYXllcnNIYW5kbGVyJ2Rh
biBhbGluZGkKICAgICIyOSI6IHsiMCI6ICJlbnRpdHkgSUQiLCAiMSI6ICJPWVVOQ1UgQURJIChu
aWNrbmFtZSkiLAogICAgICAgICAgICIyIjogIm1ldmN1dCBIUCAoYWx0ZXJuYXRpZikiLCAiMyI6
ICJtYWtzIEhQIChhbHRlcm5hdGlmKSIsCiAgICAgICAgICAgIjgiOiAiZ3VpbGQgYWRpIiwgIjE5
IjogInNwYXduIFgiLCAiMjIiOiAibWV2Y3V0IEhQIiwgIjIzIjogIm1ha3MgSFAiLAogICAgICAg
ICAgICIyNSI6ICJzcGF3biBZIiwgIjM4IjogImVraXBtYW4gSUQgZGl6aXNpIChhbHRlcm5hdGlm
KSIsCiAgICAgICAgICAgIjQwIjogImVraXBtYW4gSUQgZGl6aXNpIiwgIjUxIjogImFsbGlhbmNl
IGFkaSIsICI1MyI6ICJmYWN0aW9uIChpbnQpIn0sCn0KCiMgQ0FOTEkgZ296bGVtbGVyOiBkdW1w
J3Rha2kgeWFwaXlsYSBVWVVTTUFZQU4ga29kbGFyCkxJVkVfTk9URVMgPSB7CiAgICAiMyI6ICJD
QU5MSTogcGFrZXQgU0FERUNFIDIgcGFyYW1ldHJlIGdlbGl5b3IgLT4gWzBdIGxvbmcgKGVudGl0
eSBJRCksICIKICAgICAgICAgIlsxXSBieXRlWzI2LTMwXSAoaGFyZWtldCB2ZXJpc2kgYmxvYiku
IER1bXAndGFraSA5IGFsYW5saSB5YXBpICIKICAgICAgICAgIihWZWN0b3IyIGtvbnVtLCBmbG9h
dCdsYXIuLi4pIHRlbCB1emVyaW5kZSBCVSBTRUtJTERFIEdFTE1JWU9SLiAiCiAgICAgICAgICJQ
bGF5ZXJzSGFuZGxlci5IYW5kbGVNb3ZlJ3VuIG9rdWR1Z3UgWzFdL1szXS9bNF0vWzVdIGluZGVr
c2xlcmkgY2FubGkgIgogICAgICAgICAicGFrZXR0ZSBZT0sgLT4ga29udW0ga29kdSBvbHUga29k
IChhbWEgJ3NvbiBnb3J1bG1lJyBkYW1nYWxhcmkgY2FsaXNpeW9yKS4iLAp9CgojIFRpcCAtPiBh
bmxhbSBpcHVjdSAoY2FubGkgZG9ncnVsYW5taXMgYW5sYW0geW9rc2Ega3VsbGFuaWxpcikKVFlQ
RV9ISU5UID0gewogICAgImxvbmciOiAidXp1biB0YW0gc2F5aSAoZ2VuZWxkZSBJRCkiLAogICAg
ImludCI6ICJ0YW0gc2F5aSIsCiAgICAic2hvcnQiOiAia2lzYSB0YW0gc2F5aSIsCiAgICAiYnl0
ZSI6ICJiYXl0ICgwLTI1NSkiLAogICAgImJvb2wiOiAiZG9ncnUveWFubGlzIiwKICAgICJmbG9h
dCI6ICJvbmRhbGlrIHNheWkgKG9yYW4vbWVzYWZlKSIsCiAgICAiVmVjdG9yMiI6ICJLT05VTSAo
eCwgeSkiLAogICAgIlZlY3RvcjMiOiAiS09OVU0gKHgsIHksIHopIiwKICAgICJHdWlkIjogIkdV
SUQgKDE2IGJheXQpIiwKICAgICJHYW1lVGltZVN0YW1wIjogInphbWFuIGRhbWdhc2kgKHRpY2tz
KSIsCiAgICAic3RyaW5nIjogIm1ldGluIChpc2ltIC8gdW5pcXVlIG5hbWUpIiwKICAgICJGbGFn
Z2luZ1N0YXR1cyI6ICJQdlAgYmF5cmFnaSIsCiAgICAibG9uZ1tdIjogIklEIGRpemlzaSIsCiAg
ICAiaW50W10iOiAidGFtIHNheWkgZGl6aXNpIiwKICAgICJmbG9hdFtdIjogInNheWkgZGl6aXNp
IiwKICAgICJieXRlW10iOiAiYmF5dCBkaXppc2kiLAp9CgojIENhbmxpIGxvZ2Rha2kgLk5FVCB0
aXBsZXJpIC0+IHRhYmxvZGFraSB0aXBsZXIgKC0tZG9ncnVsYSBpY2luKQpDT01QQVQgPSB7CiAg
ICAibG9uZyI6ICAgICAgICAgICB7IkludDY0In0sCiAgICAiaW50IjogICAgICAgICAgICB7Iklu
dDMyIiwgIkludDE2IiwgIkJ5dGUiLCAiU0J5dGUifSwKICAgICJzaG9ydCI6ICAgICAgICAgIHsi
SW50MTYiLCAiSW50MzIiLCAiQnl0ZSJ9LAogICAgImJ5dGUiOiAgICAgICAgICAgeyJCeXRlIiwg
IlNCeXRlIiwgIkludDE2IiwgIkludDMyIn0sCiAgICAiYm9vbCI6ICAgICAgICAgICB7IkJvb2xl
YW4ifSwKICAgICJmbG9hdCI6ICAgICAgICAgIHsiU2luZ2xlIiwgIkRvdWJsZSJ9LAogICAgInN0
cmluZyI6ICAgICAgICAgeyJTdHJpbmcifSwKICAgICJWZWN0b3IyIjogICAgICAgIHsiU2luZ2xl
W10iLCAiZmxvYXRbXSJ9LAogICAgIlZlY3RvcjMiOiAgICAgICAgeyJTaW5nbGVbXSIsICJmbG9h
dFtdIn0sCiAgICAiR3VpZCI6ICAgICAgICAgICB7IkJ5dGVbXSIsICJHdWlkIn0sCiAgICAiR2Ft
ZVRpbWVTdGFtcCI6ICB7IkludDY0IiwgIkludDMyIn0sCiAgICAiRmxhZ2dpbmdTdGF0dXMiOiB7
IkJ5dGUiLCAiU0J5dGUiLCAiSW50MTYiLCAiSW50MzIifSwKfQoKIyAtLWtvZCBkZW5ldGltaSBp
Y2luIHRpcCBhaWxlbGVyaQpOVU1FUklDID0geyJieXRlIiwgInNieXRlIiwgInNob3J0IiwgInVz
aG9ydCIsICJpbnQiLCAidWludCIsICJsb25nIiwgInVsb25nIiwKICAgICAgICAgICAiZmxvYXQi
LCAiZG91YmxlIiwgImRlY2ltYWwifQpBUlJBWUlTSCA9IHsiaW50W10iLCAiYnl0ZVtdIiwgInNo
b3J0W10iLCAiZmxvYXRbXSIsICJzdHJpbmdbXSIsICJsb25nW10iLCAiYm9vbFtdIiwgIlZlY3Rv
cjIifQoKVFlQRV9BTElBUyA9IHsiYzQiOiAiVGltZVNwYW4ifQpBVFRSX0tJTkQgPSB7ImVxIjog
ImV2ZW50IiwgImVwIjogIm9wZXJhdGlvbiJ9CgojID09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0KIyBEVU1QLkNTIEFZ
UklTVElSTUEKIyA9PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09ClJFX0NMQVNTID0gcmUuY29tcGlsZShyInB1YmxpYyAo
PzooPzpzZWFsZWR8YWJzdHJhY3R8c3RhdGljKSApKig/OmNsYXNzfHN0cnVjdCkgKFx3KykoPzpc
cyo6XHMqKFtcd1wuXSspKT8iKQpSRV9GSUVMRCA9IHJlLmNvbXBpbGUociJeXHMqcHVibGljXHMr
KD8hc3RhdGljXGIpKFteOz1dKz8pXHMrKFx3Kylccyo7XHMqLy9ccyooMHhbMC05QS1GYS1mXSsp
XHMqJCIpClJFX0VOVU1fQ09OU1QgPSByZS5jb21waWxlKHIicHVibGljIGNvbnN0IChFdmVudENv
ZGVzfE9wZXJhdGlvbkNvZGVzKSAoXHcrKSA9ICgtP1xkKyk7IikKUkVfQVRUUiA9IHJlLmNvbXBp
bGUociJeXFsoZXF8ZXApXCgoXGQrKVwpXF0iKQoKCmRlZiBwYXJzZV9kdW1wKHBhdGgpOgogICAg
Y29kZXMgPSB7ImV2ZW50Ijoge30sICJvcGVyYXRpb24iOiB7fX0KICAgIGNsYXNzZXMgPSB7fQog
ICAgY3VyID0gTm9uZQogICAgZW51bV9jdHggPSBOb25lCiAgICBwZW5kaW5nID0gTm9uZQoKICAg
IGlmIG5vdCBvcy5wYXRoLmV4aXN0cyhwYXRoKToKICAgICAgICBwcmludCgiW0hBVEFdIGR1bXAg
YnVsdW5hbWFkaTogJXMiICUgcGF0aCkKICAgICAgICByZXR1cm4gY29kZXMsIGNsYXNzZXMKCiAg
ICB3aXRoIG9wZW4ocGF0aCwgInIiLCBlbmNvZGluZz0idXRmLTgiLCBlcnJvcnM9InJlcGxhY2Ui
KSBhcyBmOgogICAgICAgIGZvciBsaW5lIGluIGY6CiAgICAgICAgICAgIHMgPSBsaW5lLnN0cmlw
KCkKCiAgICAgICAgICAgIG0gPSByZS5tYXRjaChyInB1YmxpYyBlbnVtIChFdmVudENvZGVzfE9w
ZXJhdGlvbkNvZGVzKSIsIHMpCiAgICAgICAgICAgIGlmIG06CiAgICAgICAgICAgICAgICBlbnVt
X2N0eCA9IG0uZ3JvdXAoMSkKICAgICAgICAgICAgICAgIGNvbnRpbnVlCiAgICAgICAgICAgIGlm
IGVudW1fY3R4OgogICAgICAgICAgICAgICAgbTIgPSBSRV9FTlVNX0NPTlNULm1hdGNoKHMpCiAg
ICAgICAgICAgICAgICBpZiBtMjoKICAgICAgICAgICAgICAgICAgICBraW5kID0gImV2ZW50IiBp
ZiBtMi5ncm91cCgxKSA9PSAiRXZlbnRDb2RlcyIgZWxzZSAib3BlcmF0aW9uIgogICAgICAgICAg
ICAgICAgICAgIGNvZGVzW2tpbmRdW20yLmdyb3VwKDIpXSA9IGludChtMi5ncm91cCgzKSkKICAg
ICAgICAgICAgICAgICAgICBjb250aW51ZQogICAgICAgICAgICAgICAgaWYgcy5zdGFydHN3aXRo
KCJ9Iik6CiAgICAgICAgICAgICAgICAgICAgZW51bV9jdHggPSBOb25lCiAgICAgICAgICAgICAg
ICBjb250aW51ZQoKICAgICAgICAgICAgbSA9IFJFX0FUVFIubWF0Y2gocykKICAgICAgICAgICAg
aWYgbToKICAgICAgICAgICAgICAgIHBlbmRpbmcgPSAoQVRUUl9LSU5EW20uZ3JvdXAoMSldLCBp
bnQobS5ncm91cCgyKSkpCiAgICAgICAgICAgICAgICBjb250aW51ZQoKICAgICAgICAgICAgbSA9
IFJFX0NMQVNTLm1hdGNoKHMpCiAgICAgICAgICAgIGlmIG06CiAgICAgICAgICAgICAgICBpZiBj
dXIgaXMgbm90IE5vbmU6CiAgICAgICAgICAgICAgICAgICAgY2xhc3Nlc1tjdXJbIm5hbWUiXV0g
PSBjdXIKICAgICAgICAgICAgICAgIGN1ciA9IHsibmFtZSI6IG0uZ3JvdXAoMSksICJiYXNlIjog
bS5ncm91cCgyKSwgImZpZWxkcyI6IFtdLAogICAgICAgICAgICAgICAgICAgICAgICJraW5kIjog
cGVuZGluZ1swXSBpZiBwZW5kaW5nIGVsc2UgTm9uZSwKICAgICAgICAgICAgICAgICAgICAgICAi
Y29kZSI6IHBlbmRpbmdbMV0gaWYgcGVuZGluZyBlbHNlIE5vbmV9CiAgICAgICAgICAgICAgICBw
ZW5kaW5nID0gTm9uZQogICAgICAgICAgICAgICAgY29udGludWUKCiAgICAgICAgICAgIGlmIGN1
ciBpcyBub3QgTm9uZSBhbmQgcyA9PSAifSI6CiAgICAgICAgICAgICAgICBjbGFzc2VzW2N1clsi
bmFtZSJdXSA9IGN1cgogICAgICAgICAgICAgICAgY3VyID0gTm9uZQogICAgICAgICAgICAgICAg
Y29udGludWUKCiAgICAgICAgICAgIGlmIGN1ciBpcyBub3QgTm9uZToKICAgICAgICAgICAgICAg
IG1mID0gUkVfRklFTEQubWF0Y2gobGluZS5yc3RyaXAoIlxuIikpCiAgICAgICAgICAgICAgICBp
ZiBtZjoKICAgICAgICAgICAgICAgICAgICBpZiBtZi5ncm91cCgzKS5sb3dlcigpID09ICIweDAi
OgogICAgICAgICAgICAgICAgICAgICAgICBjb250aW51ZQogICAgICAgICAgICAgICAgICAgIGN1
clsiZmllbGRzIl0uYXBwZW5kKHsidHlwZSI6IG1mLmdyb3VwKDEpLnN0cmlwKCksCiAgICAgICAg
ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICJuYW1lIjogbWYuZ3JvdXAoMiksCiAg
ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICJvZmZzZXQiOiBtZi5ncm91
cCgzKX0pCgogICAgcmV0dXJuIGNvZGVzLCBjbGFzc2VzCgoKZGVmIHJlc29sdmVfZmllbGRzKGNs
YXNzZXMsIGNsYXNzX25hbWUpOgogICAgIiIiU2luaWZpbiB2ZSBURU1FTCBzaW5pZmxhcmluaW4g
aW5zdGFuY2UgYWxhbmxhcmluaSBvZmZzZXQgc2lyYXNpbmEgZGl6ZXIuIiIiCiAgICBjaGFpbiwg
c2VlbiwgbWlzc2luZyA9IFtdLCBzZXQoKSwgTm9uZQogICAgbiA9IGNsYXNzX25hbWUKICAgIHdo
aWxlIG46CiAgICAgICAgaWYgbiBpbiBzZWVuOgogICAgICAgICAgICBicmVhawogICAgICAgIHNl
ZW4uYWRkKG4pCiAgICAgICAgaW5mbyA9IGNsYXNzZXMuZ2V0KG4pCiAgICAgICAgaWYgaW5mbyBp
cyBOb25lOgogICAgICAgICAgICBtaXNzaW5nID0gbgogICAgICAgICAgICBicmVhawogICAgICAg
IGNoYWluLmFwcGVuZChuKQogICAgICAgIG4gPSBpbmZvWyJiYXNlIl0KCiAgICBmaWVsZHMgPSBb
XQogICAgZm9yIGNuYW1lIGluIHJldmVyc2VkKGNoYWluKToKICAgICAgICBmb3IgZiBpbiBjbGFz
c2VzW2NuYW1lXVsiZmllbGRzIl06CiAgICAgICAgICAgIGZpZWxkcy5hcHBlbmQoeyoqZiwgImRl
Y2xhcmVkX2luIjogY25hbWV9KQogICAgZmllbGRzLnNvcnQoa2V5PWxhbWJkYSB4OiBpbnQoeFsi
b2Zmc2V0Il0sIDE2KSkKICAgIHJldHVybiBjaGFpbiwgZmllbGRzLCBtaXNzaW5nCgoKZGVmIGJ1
aWxkKGNvZGVzLCBjbGFzc2VzKToKICAgIHJldiA9IHtraW5kOiB7djogayBmb3IgaywgdiBpbiB0
YWJsZS5pdGVtcygpfSBmb3Iga2luZCwgdGFibGUgaW4gY29kZXMuaXRlbXMoKX0KICAgIG91dCA9
IHsiZXZlbnRzIjoge30sICJvcGVyYXRpb25zIjoge319CiAgICB1bm1hcHBlZCA9IFtdCgogICAg
Zm9yIG5hbWUsIGluZm8gaW4gY2xhc3Nlcy5pdGVtcygpOgogICAgICAgIGtpbmQsIGNvZGUgPSBp
bmZvWyJraW5kIl0sIGluZm9bImNvZGUiXQogICAgICAgIGlmIGtpbmQgaXMgTm9uZToKICAgICAg
ICAgICAgY29udGludWUKICAgICAgICBwcmV0dHkgPSByZXZba2luZF0uZ2V0KGNvZGUpCiAgICAg
ICAgaWYgcHJldHR5IGlzIE5vbmU6CiAgICAgICAgICAgIHVubWFwcGVkLmFwcGVuZCgoa2luZCwg
Y29kZSwgbmFtZSkpCiAgICAgICAgICAgIGNvbnRpbnVlCiAgICAgICAgY2hhaW4sIGZpZWxkcywg
bWlzc2luZyA9IHJlc29sdmVfZmllbGRzKGNsYXNzZXMsIG5hbWUpCiAgICAgICAgZW50cnkgPSB7
CiAgICAgICAgICAgICJuYW1lIjogcHJldHR5LAogICAgICAgICAgICAiY2xhc3MiOiBuYW1lLAog
ICAgICAgICAgICAiYmFzZV9jaGFpbiI6IGNoYWluLAogICAgICAgICAgICAibWlzc2luZ19iYXNl
IjogbWlzc2luZywKICAgICAgICAgICAgImZpZWxkX2NvdW50IjogbGVuKGZpZWxkcyksCiAgICAg
ICAgICAgICJmaWVsZHMiOiBbCiAgICAgICAgICAgICAgICB7ImluZGV4IjogaSwgIm5hbWUiOiBm
WyJuYW1lIl0sCiAgICAgICAgICAgICAgICAgInR5cGUiOiBUWVBFX0FMSUFTLmdldChmWyJ0eXBl
Il0sIGZbInR5cGUiXSksICJyYXdfdHlwZSI6IGZbInR5cGUiXSwKICAgICAgICAgICAgICAgICAi
b2Zmc2V0IjogZlsib2Zmc2V0Il0sICJkZWNsYXJlZF9pbiI6IGZbImRlY2xhcmVkX2luIl19CiAg
ICAgICAgICAgICAgICBmb3IgaSwgZiBpbiBlbnVtZXJhdGUoZmllbGRzKQogICAgICAgICAgICBd
LAogICAgICAgIH0KICAgICAgICBzZWN0aW9uID0gImV2ZW50cyIgaWYga2luZCA9PSAiZXZlbnQi
IGVsc2UgIm9wZXJhdGlvbnMiCiAgICAgICAgb3V0W3NlY3Rpb25dLnNldGRlZmF1bHQoc3RyKGNv
ZGUpLCBbXSkuYXBwZW5kKGVudHJ5KQoKICAgIHJldHVybiBvdXQsIHVubWFwcGVkCgoKZGVmIGxv
YWQoKToKICAgIGNvZGVzLCBjbGFzc2VzID0gcGFyc2VfZHVtcChEVU1QKQogICAgc2NoZW1hLCB1
bm1hcHBlZCA9IGJ1aWxkKGNvZGVzLCBjbGFzc2VzKQogICAgcmV0dXJuIGNvZGVzLCBjbGFzc2Vz
LCBzY2hlbWEsIHVubWFwcGVkCgoKZGVmIGNvdW50X3ZhcmlhbnRzKHNjaGVtYSwgc2VjdGlvbik6
CiAgICByZXR1cm4gc3VtKGxlbih2KSBmb3IgdiBpbiBzY2hlbWFbc2VjdGlvbl0udmFsdWVzKCkp
CgoKIyA9PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09CiMgRU5VTSBPS1VNQSAoZHVtcCArIEMjKQojID09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT0KZGVmIHBhcnNlX2R1bXBfZW51bXMocGF0aCwgd2FudGVkPSgiRXZlbnRDb2RlcyIsICJPcGVy
YXRpb25Db2RlcyIpKToKICAgICIiImR1bXAuY3MgaWNpbmRla2kgJ3B1YmxpYyBlbnVtIFgnIGJs
b2tsYXJpbmRhbiB7aXNpbTogZGVnZXJ9IHVyZXRpci4iIiIKICAgIHJlc3VsdCA9IHt3OiB7fSBm
b3IgdyBpbiB3YW50ZWR9CiAgICBpZiBub3Qgb3MucGF0aC5leGlzdHMocGF0aCk6CiAgICAgICAg
cHJpbnQoIltIQVRBXSBkdW1wIGJ1bHVuYW1hZGk6ICVzIiAlIHBhdGgpCiAgICAgICAgcmV0dXJu
IHJlc3VsdAoKICAgIGN1cnJlbnQgPSBOb25lCiAgICB3aXRoIG9wZW4ocGF0aCwgInIiLCBlbmNv
ZGluZz0idXRmLTgiLCBlcnJvcnM9InJlcGxhY2UiKSBhcyBmOgogICAgICAgIGZvciBsaW5lIGlu
IGY6CiAgICAgICAgICAgIG0gPSByZS5tYXRjaChyIlxzKnB1YmxpYyBlbnVtIChcdyspIiwgbGlu
ZSkKICAgICAgICAgICAgaWYgbToKICAgICAgICAgICAgICAgIGN1cnJlbnQgPSBtLmdyb3VwKDEp
IGlmIG0uZ3JvdXAoMSkgaW4gd2FudGVkIGVsc2UgTm9uZQogICAgICAgICAgICAgICAgY29udGlu
dWUKICAgICAgICAgICAgaWYgY3VycmVudCBpcyBOb25lOgogICAgICAgICAgICAgICAgY29udGlu
dWUKICAgICAgICAgICAgaWYgbGluZS5zdHJpcCgpLnN0YXJ0c3dpdGgoIn0iKToKICAgICAgICAg
ICAgICAgIGN1cnJlbnQgPSBOb25lCiAgICAgICAgICAgICAgICBjb250aW51ZQogICAgICAgICAg
ICBtMiA9IHJlLnNlYXJjaChyInB1YmxpYyBjb25zdCAlcyAoXHcrKSA9ICgtP1xkKyk7IiAlIGN1
cnJlbnQsIGxpbmUpCiAgICAgICAgICAgIGlmIG0yOgogICAgICAgICAgICAgICAgcmVzdWx0W2N1
cnJlbnRdW20yLmdyb3VwKDEpXSA9IGludChtMi5ncm91cCgyKSkKICAgIHJldHVybiByZXN1bHQK
CgpkZWYgc3RyaXBfY29tbWVudHModGV4dCk6CiAgICAiIiJZb3J1bWxhcmkgdGVtaXpsZS4gT05F
TUxJOiB5b3J1bWxhcmRha2kgVHVya2NlIGtlbGltZWxlci92aXJndWxsZXIKICAgIHlva3NhIGVu
dW0gdXllc2kgc2FuaWxpcCB5YW5saXMgYWxhcm0gdXJldGl5b3IuIiIiCiAgICB0ZXh0ID0gcmUu
c3ViKHIiL1wqLio/XCovIiwgIiIsIHRleHQsIGZsYWdzPXJlLlMpCiAgICByZXR1cm4gcmUuc3Vi
KHIiLy9bXlxuXSoiLCAiIiwgdGV4dCkKCgpkZWYgcGFyc2VfY3NfZW51bXMocGF0aCk6CiAgICAi
IiJCaXIgLmNzIGRvc3lhc2luZGFraSB0dW0gZW51bSdsYXJpIHtlbnVtQWRpOiB7aXNpbTogZGVn
ZXJ9fSBvbGFyYWsgb2t1ci4iIiIKICAgIG91dCA9IHt9CiAgICBpZiBub3Qgb3MucGF0aC5leGlz
dHMocGF0aCk6CiAgICAgICAgcmV0dXJuIG91dAoKICAgIHdpdGggb3BlbihwYXRoLCAiciIsIGVu
Y29kaW5nPSJ1dGYtOCIsIGVycm9ycz0icmVwbGFjZSIpIGFzIGY6CiAgICAgICAgdGV4dCA9IHN0
cmlwX2NvbW1lbnRzKGYucmVhZCgpKQoKICAgIGZvciBtIGluIHJlLmZpbmRpdGVyKHIiZW51bVxz
KyhcdyspXHMqKD86OlxzKlx3K1xzKik/XHsoLio/KVx9IiwgdGV4dCwgcmUuUyk6CiAgICAgICAg
bmFtZSA9IG0uZ3JvdXAoMSkKICAgICAgICBib2R5ID0gbS5ncm91cCgyKQogICAgICAgIHZhbHVl
cyA9IHt9CiAgICAgICAgbmV4dF92YWx1ZSA9IDAKICAgICAgICBmb3IgbGluZSBpbiBib2R5LnNw
bGl0KCIsIik6CiAgICAgICAgICAgIGxpbmUgPSBsaW5lLnN0cmlwKCkKICAgICAgICAgICAgaWYg
bm90IGxpbmUgb3IgbGluZS5zdGFydHN3aXRoKCIvLyIpOgogICAgICAgICAgICAgICAgY29udGlu
dWUKICAgICAgICAgICAgbW0gPSByZS5tYXRjaChyIihcdyspXHMqKD1ccyooMHhbMC05QS1GYS1m
XSt8XGQrKSk/IiwgbGluZSkKICAgICAgICAgICAgaWYgbm90IG1tOgogICAgICAgICAgICAgICAg
Y29udGludWUKICAgICAgICAgICAga2V5ID0gbW0uZ3JvdXAoMSkKICAgICAgICAgICAgaWYgbW0u
Z3JvdXAoMyk6CiAgICAgICAgICAgICAgICByYXcgPSBtbS5ncm91cCgzKQogICAgICAgICAgICAg
ICAgbmV4dF92YWx1ZSA9IGludChyYXcsIDE2KSBpZiByYXcubG93ZXIoKS5zdGFydHN3aXRoKCIw
eCIpIGVsc2UgaW50KHJhdykKICAgICAgICAgICAgdmFsdWVzW2tleV0gPSBuZXh0X3ZhbHVlCiAg
ICAgICAgICAgIG5leHRfdmFsdWUgKz0gMQogICAgICAgIG91dFtuYW1lXSA9IHZhbHVlcwogICAg
cmV0dXJuIG91dAoKCmRlZiBsb2FkX2NzX2VudW1fY29kZXMoKToKICAgICIiIkVudW0gZG9zeWFs
YXJpbmRhbiAnRW51bUFkaS5VeWUnIC0+IGRlZ2VyIHNvemx1Z3UgdXJldGlyICgtLWtvZCBpY2lu
KS4iIiIKICAgIG5hbWVzID0ge30KICAgIGlmIG5vdCBvcy5wYXRoLmlzZGlyKEVOVU1fRElSKToK
ICAgICAgICByZXR1cm4gbmFtZXMKICAgIGZvciBmbmFtZSBpbiBvcy5saXN0ZGlyKEVOVU1fRElS
KToKICAgICAgICBpZiBub3QgZm5hbWUuZW5kc3dpdGgoIi5jcyIpOgogICAgICAgICAgICBjb250
aW51ZQogICAgICAgIHdpdGggb3Blbihvcy5wYXRoLmpvaW4oRU5VTV9ESVIsIGZuYW1lKSwgInIi
LCBlbmNvZGluZz0idXRmLTgiLCBlcnJvcnM9InJlcGxhY2UiKSBhcyBmOgogICAgICAgICAgICB0
ZXh0ID0gc3RyaXBfY29tbWVudHMoZi5yZWFkKCkpCiAgICAgICAgZm9yIG0gaW4gcmUuZmluZGl0
ZXIociJlbnVtXHMrKFx3KylccyooPzo6XHMqXHcrXHMqKT9ceyguKj8pXH0iLCB0ZXh0LCByZS5T
KToKICAgICAgICAgICAgZW5hbWUsIGJvZHkgPSBtLmdyb3VwKDEpLCBtLmdyb3VwKDIpCiAgICAg
ICAgICAgIG54dCA9IDAKICAgICAgICAgICAgZm9yIHBhcnQgaW4gYm9keS5zcGxpdCgiLCIpOgog
ICAgICAgICAgICAgICAgcGFydCA9IHBhcnQuc3RyaXAoKQogICAgICAgICAgICAgICAgaWYgbm90
IHBhcnQ6CiAgICAgICAgICAgICAgICAgICAgY29udGludWUKICAgICAgICAgICAgICAgIG1tID0g
cmUubWF0Y2gociIoXHcrKVxzKig/Oj1ccyooLT9cZCspKT8iLCBwYXJ0KQogICAgICAgICAgICAg
ICAgaWYgbm90IG1tOgogICAgICAgICAgICAgICAgICAgIGNvbnRpbnVlCiAgICAgICAgICAgICAg
ICBpZiBtbS5ncm91cCgyKSBpcyBub3QgTm9uZToKICAgICAgICAgICAgICAgICAgICBueHQgPSBp
bnQobW0uZ3JvdXAoMikpCiAgICAgICAgICAgICAgICBuYW1lc1siJXMuJXMiICUgKGVuYW1lLCBt
bS5ncm91cCgxKSldID0gbnh0CiAgICAgICAgICAgICAgICBueHQgKz0gMQogICAgcmV0dXJuIG5h
bWVzCgoKZGVmIGxvYWRfc2NoZW1hKCk6CiAgICBpZiBub3Qgb3MucGF0aC5leGlzdHMoT1VUKToK
ICAgICAgICBwcmludCgiW0hBVEFdICVzIHlvay4gT25jZTogcHl0aG9uIGFuYWxpei5weSAtLXVy
ZXQiICUgT1VUKQogICAgICAgIHN5cy5leGl0KDEpCiAgICB3aXRoIG9wZW4oT1VULCAiciIsIGVu
Y29kaW5nPSJ1dGYtOCIpIGFzIGY6CiAgICAgICAgcmV0dXJuIGpzb24ubG9hZChmKQoKCiMgPT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PQojIE9SVEFLIFlBUkRJTUNJTEFSCiMgPT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PQpkZWYgY2Fub25f
dHlwZSh0KToKICAgICIiIlRpcCBhZGluaSBLQVJTSUxBU1RJUk1BIGljaW4gc2FkZWxlc3Rpcmly
ICgwOC4xMC4yMDI2KS4KICAgIE5FREVOOiBvYmZ1c2NhdG9yIGhlciBndW5jZWxsZW1lZGUgc2lu
aWYgYWRsYXJpbmkga2F5ZGlyaXIgKGE3ayAtPiBhN2wsIGNmciAtPiBjZnMpLgogICAgQWRsYXIg
ZGVnaXN0aWdpIGljaW4gJ2ZhcmsnIHJhcG9ydSB5dXpsZXJjZSBTQUhURSBkZWdpc2lrbGlrIGdv
c3Rlcml5b3JkdS4KICAgIEFydGlrOiBiaWxpbmVuIHRlbWVsIHRpcGxlciBheW5lbiBrYWxpciwg
QklMSU5NRVlFTiAob2JmdXNjYXRlZCkgdGlwbGVyICcjVCcgb2x1cjsKICAgIGRpemkgYm95dXRs
YXJpIChbXSAvIFtdW10pIGtvcnVudXIgLT4geWFwaSBkZWdpc2lrbGlnaSB5aW5lIHlha2FsYW5p
ci4iIiIKICAgIGNvcmUgPSAoImxvbmciLCAidWxvbmciLCAiaW50IiwgInVpbnQiLCAic2hvcnQi
LCAidXNob3J0IiwgImJ5dGUiLCAic2J5dGUiLAogICAgICAgICAgICAiZmxvYXQiLCAiZG91Ymxl
IiwgImJvb2wiLCAic3RyaW5nIiwgIkd1aWQiLCAiR2FtZVRpbWVTdGFtcCIsICJkZWNpbWFsIiwg
ImNoYXIiKQogICAgbSA9IHJlLm1hdGNoKHIiXihbXlxbXF1dKykoKFxbXF0pKikkIiwgdCBvciAi
IikKICAgIGlmIG5vdCBtOgogICAgICAgIHJldHVybiB0CiAgICBiYXNlLCBkaW1zID0gbS5ncm91
cCgxKSwgbS5ncm91cCgyKQogICAgcmV0dXJuIChiYXNlIGlmIGJhc2UgaW4gY29yZSBlbHNlICIj
VCIpICsgZGltcwoKCmRlZiBjb2RlX3NpZyhlbnRyeSk6CiAgICAiIiJLYXJzaWxhc3Rpcm1hIGlj
aW4gTk9STUFMSVpFIGltemEuCiAgICBPTkVNTEk6IG9iZnVzY2F0b3IgaGVyIGd1bmNlbGxlbWVk
ZSBzaW5pZiB2ZSBhbGFuIElTSU1MRVJJTkkga2F5ZGlyaXIuCiAgICBCdSB5dXpkZW4gaXNpbWxl
ciBERUdJTCwgYWxhbiBzYXlpc2kgKyB0aXBsZXIgKyBvZmZzZXRsZXIga2Fyc2lsYXN0aXJpbGly
LgogICAgRUsgKDA4LjEwLjIwMjYpOiBvYmZ1c2NhdGVkIFRJUCBhZGxhcmkgZGEgYWQgZGVnaXNp
bWluZGVuIGV0a2lsZW5peW9yZHUgLT4KICAgIGNhbm9uX3R5cGUoKSBpbGUgJyNUJyB5YXBpbGly
OyBib3lsZWNlIHlhbG5pemNhIEdFUkNFSyB5YXBpIGRlZ2lzaWtsaWdpIGdvcnVudXIuIiIiCiAg
ICByZXR1cm4gKAogICAgICAgIGVudHJ5WyJmaWVsZF9jb3VudCJdLAogICAgICAgIHR1cGxlKGNh
bm9uX3R5cGUoZlsidHlwZSJdKSBmb3IgZiBpbiBlbnRyeVsiZmllbGRzIl0pLAogICAgICAgIHR1
cGxlKGZbIm9mZnNldCJdIGZvciBmIGluIGVudHJ5WyJmaWVsZHMiXSksCiAgICApCgoKZGVmIHR5
cGVfbGlzdChlbnRyeSwgbGltaXQ9MjQpOgogICAgdCA9IFtmWyJ0eXBlIl0gZm9yIGYgaW4gZW50
cnlbImZpZWxkcyJdXQogICAgaWYgbGVuKHQpID4gbGltaXQ6CiAgICAgICAgcmV0dXJuICIsICIu
am9pbih0WzpsaW1pdF0pICsgIiwgLi4uICgrJWQpIiAlIChsZW4odCkgLSBsaW1pdCkKICAgIHJl
dHVybiAiLCAiLmpvaW4odCkKCgpkZWYgZmllbGRfbWVhbmluZyhjb2RlLCBmKToKICAgICIiIk9u
Y2UgY2FubGkgZG9ncnVsYW5taXMgYW5sYW0gKEtOT1dOKSwgeW9rc2EgdGlwdGVuIGNpa2FyaW0u
IiIiCiAgICBrbm93biA9IEtOT1dOLmdldChzdHIoY29kZSksIHt9KS5nZXQoc3RyKGZbImluZGV4
Il0pLCAiIikKICAgIGlmIGtub3duOgogICAgICAgIHJldHVybiBrbm93biwgVHJ1ZQoKICAgIHQg
PSBmWyJ0eXBlIl0KICAgIGlmIGZbImluZGV4Il0gPT0gMCBhbmQgdCA9PSAibG9uZyI6CiAgICAg
ICAgcmV0dXJuICJlbnRpdHkgLyBuZXNuZSBJRCIsIEZhbHNlCiAgICBpZiBmWyJpbmRleCJdID09
IDAgYW5kIHQgaW4gKCJpbnQiLCAic2hvcnQiKToKICAgICAgICByZXR1cm4gImVudGl0eSAvIG5l
c25lIElEIG9sYWJpbGlyIiwgRmFsc2UKICAgIGhpbnQgPSBUWVBFX0hJTlQuZ2V0KHQpCiAgICBp
ZiBoaW50OgogICAgICAgIHJldHVybiBoaW50LCBGYWxzZQogICAgIyBPQkZVU0NBVEVEIE9ZVU4g
VElQSSAoa3VsbGFuaWNpIGlzdGVnaSAwOC4xMC4yMDI2KToKICAgICMgInI4IiwgImE3bCIsICJh
Nm0iIGdpYmkga2FyaXN0aXJpbG1pcyBhZGxhciBoZXIgb3l1biBndW5jZWxsZW1lc2luZGUgREVH
SVNJWU9SIHZlCiAgICAjIEluc3BlY3RvcidkYSBva3V5dWN1eWEgaGljYmlyIHNleSBhbmxhdG1p
eW9yIC0+IG5vdHIgZXRpa2V0IGJhcy4KICAgICMgTm9rdGEgaWNlcmVuIGFkbGFyZGEgKG9ybi4g
ImJjZi5DbHVzdGVyUG9zaXRpb25TdGF0ZSIpIG9rdW5hYmlsaXIgc29uIHBhcmNheWkga3VsbGFu
LgogICAgaWYgIi4iIGluIHQ6CiAgICAgICAgdGFpbCA9IHQuc3BsaXQoIi4iKVstMV0uc3RyaXAo
KQogICAgICAgIGlmIHRhaWwgYW5kIG5vdCByZS5mdWxsbWF0Y2gociJbYS16MC05XXsxLDR9Iiwg
dGFpbCk6CiAgICAgICAgICAgIHJldHVybiB0YWlsLCBGYWxzZQogICAgaWYgcmUuZnVsbG1hdGNo
KHIiW0EtWmEtejAtOV9dezEsNn0oXC5bQS1aYS16MC05X117MSw2fSk/IiwgdCk6CiAgICAgICAg
cmV0dXJuICJzYXlpIChveXVuIGVudW0ndSkiLCBGYWxzZQogICAgcmV0dXJuIHQsIEZhbHNlCgoK
ZGVmIHR5cGVfb2soc2NoZW1hX3R5cGUsIGxpdmVfcmF3KToKICAgIGFsbG93ZWQgPSBDT01QQVQu
Z2V0KHNjaGVtYV90eXBlKQogICAgaWYgbm90IGFsbG93ZWQ6CiAgICAgICAgcmV0dXJuIFRydWUg
ICAjIGJpbGlubWV5ZW4gdGlwIC0+IGthcmFyIHZlcm1lCiAgICByZXR1cm4gbGl2ZV9yYXcgaW4g
YWxsb3dlZAoKCmRlZiBjb21wYXRpYmxlKHJlYWRfdHlwZSwgdGFibGVfdHlwZSk6CiAgICAiIiIt
LWtvZCBkZW5ldGltaSBpY2luIHRpcCB1eXVtdS4iIiIKICAgIHIsIHQgPSAocmVhZF90eXBlIG9y
ICIiKS5zdHJpcCgpLCAodGFibGVfdHlwZSBvciAiIikuc3RyaXAoKQogICAgaWYgbm90IHI6CiAg
ICAgICAgcmV0dXJuICJvayIsICIiCiAgICBpZiByID09IHQ6CiAgICAgICAgcmV0dXJuICJvayIs
ICIiCgogICAgaWYgciBpbiBBUlJBWUlTSCBvciB0IGluIEFSUkFZSVNIOgogICAgICAgIGlmIHIg
aW4gQVJSQVlJU0ggYW5kIHQgaW4gQVJSQVlJU0g6CiAgICAgICAgICAgIHJldHVybiAib2siLCAi
IgogICAgICAgIGlmIHQgPT0gIlZlY3RvcjIiIGFuZCByIGluICgiZmxvYXRbXSIsICJTaW5nbGVb
XSIpOgogICAgICAgICAgICByZXR1cm4gIm9rIiwgImtvbnVtIgogICAgICAgIHJldHVybiAiYmFk
IiwgImRpemkgdXl1c21hemxpZ2kgKCVzIHZzICVzKSIgJSAociwgdCkKCiAgICBkZWYga2luZCh4
KToKICAgICAgICBpZiB4IGluICgic3RyaW5nIiwgImNoYXIiLCAib2JqZWN0Iik6CiAgICAgICAg
ICAgIHJldHVybiAidGV4dCIKICAgICAgICBpZiB4ID09ICJib29sIjoKICAgICAgICAgICAgcmV0
dXJuICJib29sIgogICAgICAgIGlmIHggPT0gIkd1aWQiOgogICAgICAgICAgICByZXR1cm4gImd1
aWQiCiAgICAgICAgaWYgeCBpbiBOVU1FUklDOgogICAgICAgICAgICByZXR1cm4gIm51bSIKICAg
ICAgICByZXR1cm4gIm90aGVyIiAgICAgICMgZW51bSAvIHN0cnVjdCAoRmxhZ2dpbmdTdGF0dXMs
IEdhbWVUaW1lU3RhbXAuLi4pIC0+IHNheWkgdXl1bWx1CgogICAga3IsIGt0ID0ga2luZChyKSwg
a2luZCh0KQoKICAgIGlmIGtyID09IGt0OgogICAgICAgIHJldHVybiAib2siLCAiIgogICAgaWYg
Im90aGVyIiBpbiAoa3IsIGt0KSBhbmQgIm51bSIgaW4gKGtyLCBrdCk6CiAgICAgICAgcmV0dXJu
ICJvayIsICJlbnVtL3N0cnVjdCIKICAgIGlmIGtyID09ICJudW0iIGFuZCBrdCA9PSAibnVtIjoK
ICAgICAgICByZXR1cm4gIm9rIiwgIiIKICAgIHJldHVybiAiYmFkIiwgIiVzIG9rdW51eW9yLCB0
YWJsb2RhICVzIiAlIChyLCB0KQoKCiMgPT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PQojIEtPTVVUOiAtLXVyZXQKIyA9
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09CmRlZiBjbWRfdXJldCgpOgogICAgY29kZXMsIGNsYXNzZXMsIHNjaGVtYSwg
dW5tYXBwZWQgPSBsb2FkKCkKICAgIHdpdGggb3BlbihPVVQsICJ3IiwgZW5jb2Rpbmc9InV0Zi04
IikgYXMgZjoKICAgICAgICBqc29uLmR1bXAoc2NoZW1hLCBmLCBlbnN1cmVfYXNjaWk9RmFsc2Us
IGluZGVudD0xKQogICAgcHJpbnQoIkV2ZW50Q29kZXM6ICVkICAgT3BlcmF0aW9uQ29kZXM6ICVk
IiAlIChsZW4oY29kZXNbImV2ZW50Il0pLCBsZW4oY29kZXNbIm9wZXJhdGlvbiJdKSkpCiAgICBw
cmludCgiRXZlbnQga29kdTogJWQgKCVkIHNpbmlmKSAgIE9wZXJhc3lvbiBrb2R1OiAlZCAoJWQg
c2luaWYpIiAlICgKICAgICAgICBsZW4oc2NoZW1hWyJldmVudHMiXSksIGNvdW50X3ZhcmlhbnRz
KHNjaGVtYSwgImV2ZW50cyIpLAogICAgICAgIGxlbihzY2hlbWFbIm9wZXJhdGlvbnMiXSksIGNv
dW50X3ZhcmlhbnRzKHNjaGVtYSwgIm9wZXJhdGlvbnMiKSkpCiAgICBwcmludCgiWWF6aWxkaTog
JXMiICUgT1VUKQoKCiMgPT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PQojIEtPTVVUOiAtLW1kCiMgPT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PQpkZWYgY21kX21kKCk6CiAgICBjb2RlcywgY2xhc3Nlcywgc2NoZW1hLCB1bm1hcHBlZCA9IGxv
YWQoKQogICAgZHVwX2V2ZW50cyA9IFtjIGZvciBjLCB2IGluIHNjaGVtYVsiZXZlbnRzIl0uaXRl
bXMoKSBpZiBsZW4odikgPiAxXQogICAgZHVwX29wcyA9IFtjIGZvciBjLCB2IGluIHNjaGVtYVsi
b3BlcmF0aW9ucyJdLml0ZW1zKCkgaWYgbGVuKHYpID4gMV0KCiAgICBMID0gW10KICAgIEwuYXBw
ZW5kKCIjIE5pZ2h0d2F0Y2ggLSBUVU0gUEFLRVRMRVJJTiBBTEFOIFNJUkFTSSIpCiAgICBMLmFw
cGVuZCgiIikKICAgIEwuYXBwZW5kKCJVcmV0aW06ICVzICAgfCAgIEtheW5hazogYEV4dHJhL2R1
bXAuY3NgIiAlIGRhdGV0aW1lLmRhdGV0aW1lLm5vdygpLnN0cmZ0aW1lKCIlWS0lbS0lZCAlSDol
TSIpKQogICAgTC5hcHBlbmQoIiIpCiAgICBMLmFwcGVuZCgifCB8IEtvZCB8IFZhcnlhbnQgKHNp
bmlmKSB8IikKICAgIEwuYXBwZW5kKCJ8LS0tfC0tLXwtLS18IikKICAgIEwuYXBwZW5kKCJ8IEV2
ZW50IGBbZXEoTildYCB8ICVkIHwgJWQgfCIgJSAobGVuKHNjaGVtYVsiZXZlbnRzIl0pLCBjb3Vu
dF92YXJpYW50cyhzY2hlbWEsICJldmVudHMiKSkpCiAgICBMLmFwcGVuZCgifCBPcGVyYXN5b24g
YFtlcChOKV1gIHwgJWQgfCAlZCB8IiAlIChsZW4oc2NoZW1hWyJvcGVyYXRpb25zIl0pLCBjb3Vu
dF92YXJpYW50cyhzY2hlbWEsICJvcGVyYXRpb25zIikpKQogICAgTC5hcHBlbmQoIiIpCiAgICBM
LmFwcGVuZCgiKipWYXJ5YW50KiogPSBheW5pIGtvZCBpY2luIGJpcmRlbiBmYXpsYSBzaW5pZiAo
Z2VuZWxkZSBpc3RlayArIHlhbml0KS4iKQogICAgTC5hcHBlbmQoIi0gQ29rIHZhcnlhbnRsaSBl
dmVudCBrb2R1OiAlZCAlcyIgJSAobGVuKGR1cF9ldmVudHMpLCBkdXBfZXZlbnRzWzoxNV0pKQog
ICAgTC5hcHBlbmQoIi0gQ29rIHZhcnlhbnRsaSBvcGVyYXN5b24ga29kdTogJWQgJXMiICUgKGxl
bihkdXBfb3BzKSwgZHVwX29wc1s6MTVdKSkKICAgIEwuYXBwZW5kKCIiKQogICAgTC5hcHBlbmQo
IioqS3VyYWw6KiogaW5kZWtzID0gc2luaWZpbiB2ZSB0ZW1lbCBzaW5pZmxhcmluaW4gaW5zdGFu
Y2UgYWxhbmxhcmluaW4gb2Zmc2V0IHNpcmFzaS4iKQogICAgTC5hcHBlbmQoIlBob3RvbiBudWxs
IGFsYW5sYXJpIEdPTkRFUk1FWiAtPiBva3Vya2VuIGluZGVrcyBudW1hcmFzaW5hIGdvcmUgb2t1
eXVuLiIpCiAgICBMLmFwcGVuZCgiIikKICAgIEwuYXBwZW5kKCItLS0iKQogICAgTC5hcHBlbmQo
IiIpCgogICAgZm9yIHNlY3Rpb24sIHRpdGxlIGluICgoImV2ZW50cyIsICJFVkVOVCBQQUtFVExF
UkkiKSwgKCJvcGVyYXRpb25zIiwgIk9QRVJBU1lPTiBQQUtFVExFUkkgKGlzdGVrL3lhbml0KSIp
KToKICAgICAgICB0YWJsZSA9IHNjaGVtYVtzZWN0aW9uXQogICAgICAgIEwuYXBwZW5kKCIjICVz
ICAoJWQga29kKSIgJSAodGl0bGUsIGxlbih0YWJsZSkpKQogICAgICAgIEwuYXBwZW5kKCIiKQog
ICAgICAgIGZvciBjb2RlIGluIHNvcnRlZCh0YWJsZSwga2V5PWludCk6CiAgICAgICAgICAgIHZh
cmlhbnRzID0gc29ydGVkKHRhYmxlW2NvZGVdLCBrZXk9bGFtYmRhIGU6IGVbImNsYXNzIl0pCiAg
ICAgICAgICAgIGZvciB2aSwgZSBpbiBlbnVtZXJhdGUodmFyaWFudHMsIDEpOgogICAgICAgICAg
ICAgICAgbWFyayA9ICJPSyAiIGlmIHNlY3Rpb24gPT0gImV2ZW50cyIgYW5kIGNvZGUgaW4gS05P
V04gYW5kIHZpID09IDEgZWxzZSAiIgogICAgICAgICAgICAgICAgc3VmZml4ID0gIiAgLS0gIFZh
cnlhbnQgJWQvJWQiICUgKHZpLCBsZW4odmFyaWFudHMpKSBpZiBsZW4odmFyaWFudHMpID4gMSBl
bHNlICIiCiAgICAgICAgICAgICAgICBMLmFwcGVuZCgiIyMgJXNbJXNdICVzJXMiICUgKG1hcmss
IGNvZGUsIGVbIm5hbWUiXSwgc3VmZml4KSkKICAgICAgICAgICAgICAgIEwuYXBwZW5kKCJgJXNg
ICAgemluY2lyOiBgJXNgJXMgICAoJWQgYWxhbikiICUgKAogICAgICAgICAgICAgICAgICAgIGVb
ImNsYXNzIl0sICIgLT4gIi5qb2luKGVbImJhc2VfY2hhaW4iXSksCiAgICAgICAgICAgICAgICAg
ICAgIiIgaWYgbm90IGUuZ2V0KCJtaXNzaW5nX2Jhc2UiKSBlbHNlICIgICAodGVtZWwgc2luaWYg
eW9rOiAlcykiICUgZVsibWlzc2luZ19iYXNlIl0sCiAgICAgICAgICAgICAgICAgICAgZVsiZmll
bGRfY291bnQiXSkpCiAgICAgICAgICAgICAgICBpZiBzdHIoY29kZSkgaW4gTElWRV9OT1RFUzoK
ICAgICAgICAgICAgICAgICAgICBMLmFwcGVuZCgiIikKICAgICAgICAgICAgICAgICAgICBMLmFw
cGVuZCgiPiBVWUFSSSAoY2FubGkpOiAlcyIgJSBMSVZFX05PVEVTW3N0cihjb2RlKV0pCiAgICAg
ICAgICAgICAgICBMLmFwcGVuZCgiIikKICAgICAgICAgICAgICAgIEwuYXBwZW5kKCJ8IGluZGVr
cyB8IGFsYW4gfCB0aXAgfCBvZmZzZXQgfCBzaW5pZiB8IGFubGFtIHwiKQogICAgICAgICAgICAg
ICAgTC5hcHBlbmQoInwtLS18LS0tfC0tLXwtLS18LS0tfC0tLXwiKQogICAgICAgICAgICAgICAg
Zm9yIGYgaW4gZVsiZmllbGRzIl06CiAgICAgICAgICAgICAgICAgICAgbWVhbiA9IEtOT1dOLmdl
dChjb2RlLCB7fSkuZ2V0KHN0cihmWyJpbmRleCJdKSwgIiIpIGlmIHNlY3Rpb24gPT0gImV2ZW50
cyIgZWxzZSAiIgogICAgICAgICAgICAgICAgICAgIEwuYXBwZW5kKCJ8ICVkIHwgYCVzYCB8ICVz
IHwgJXMgfCAlcyB8ICVzIHwiICUgKAogICAgICAgICAgICAgICAgICAgICAgICBmWyJpbmRleCJd
LCBmWyJuYW1lIl0sIGZbInR5cGUiXSwgZlsib2Zmc2V0Il0sIGZbImRlY2xhcmVkX2luIl0sIG1l
YW4pKQogICAgICAgICAgICAgICAgTC5hcHBlbmQoIiIpCgogICAgd2l0aCBvcGVuKE1ELCAidyIs
IGVuY29kaW5nPSJ1dGYtOCIpIGFzIGY6CiAgICAgICAgZi53cml0ZSgiXG4iLmpvaW4oTCkpCgog
ICAgcHJpbnQoIkVWRU5UIGtvZHUgICAgICAgICAgOiAlZCAgICh2YXJ5YW50L3NpbmlmOiAlZCki
ICUgKGxlbihzY2hlbWFbImV2ZW50cyJdKSwgY291bnRfdmFyaWFudHMoc2NoZW1hLCAiZXZlbnRz
IikpKQogICAgcHJpbnQoIk9QRVJBU1lPTiBrb2R1ICAgICAgOiAlZCAgICh2YXJ5YW50L3Npbmlm
OiAlZCkiICUgKGxlbihzY2hlbWFbIm9wZXJhdGlvbnMiXSksIGNvdW50X3ZhcmlhbnRzKHNjaGVt
YSwgIm9wZXJhdGlvbnMiKSkpCiAgICBwcmludCgiQ29rIHZhcnlhbnRsaSBrb2QgICA6IGV2ZW50
ICVkLCBvcGVyYXN5b24gJWQiICUgKGxlbihkdXBfZXZlbnRzKSwgbGVuKGR1cF9vcHMpKSkKICAg
IHByaW50KCJEb2dydWxhbm1pcyBldmVudCAgIDogJWQgICglcykiICUgKGxlbihLTk9XTiksICIs
ICIuam9pbihzb3J0ZWQoS05PV04sIGtleT1pbnQpKSkpCiAgICBwcmludCgiRXNsZXNtZXllbiBh
dHRyaWJ1dGU6ICVkICVzIiAlIChsZW4odW5tYXBwZWQpLCBbKHVbMF0sIHVbMV0pIGZvciB1IGlu
IHVubWFwcGVkWzoxMF1dKSkKICAgIHByaW50KCJUb3BsYW0gYWxhbiBzYXlpc2kgIDogJWQiICUg
c3VtKGZbImZpZWxkX2NvdW50Il0gZm9yIHQgaW4gc2NoZW1hLnZhbHVlcygpIGZvciB2IGluIHQu
dmFsdWVzKCkgZm9yIGYgaW4gdikpCiAgICBwcmludCgiWWF6aWxkaSAgICAgICAgICAgICA6ICVz
IiAlIE1EKQoKCiMgPT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PQojIEtPTVVUOiAtLWZhcmsKIyA9PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
CmRlZiBjbWRfZmFyaygpOgogICAgb2xkX3BhdGggPSBvcy5wYXRoLmpvaW4oU0NSSVBUX0RJUiwg
InBha2V0X3NlbWFzaV9lc2tpLmpzb24iKQogICAgaWYgbm90IG9zLnBhdGguZXhpc3RzKG9sZF9w
YXRoKToKICAgICAgICBwcmludCgiW0hBVEFdIEVza2kgdGFibG8geW9rOiAlcyIgJSBvbGRfcGF0
aCkKICAgICAgICBwcmludCgiICAgICAgIEd1bmNlbGxlbWUgb25jZXNpICdwYWtldF9zZW1hc2ku
anNvbicgZG9zeWFzaW5pIGJ1IGFkbGEga29weWFsYXlpbi4iKQogICAgICAgIHJldHVybgoKICAg
IHdpdGggb3BlbihvbGRfcGF0aCwgInIiLCBlbmNvZGluZz0idXRmLTgiKSBhcyBmOgogICAgICAg
IG9sZCA9IGpzb24ubG9hZChmKQogICAgKl8sIG5ldywgdW5tYXBwZWQgPSBsb2FkKCkKCiAgICBw
cmludCgiPSIgKiA3NCkKICAgIHByaW50KCJQQUtFVCBTRU1BU0kgRkFSS0kgIChlc2tpIHRhYmxv
IDwtPiB5ZW5pIGR1bXAuY3MpIikKICAgIHByaW50KCJLYXJzaWxhc3Rpcm1hOiBhbGFuIHNheWlz
aSArIHRpcGxlciArIG9mZnNldGxlciAgKGlzaW1sZXIgeW9rIHNheWlsaXIpIikKICAgIHByaW50
KCI9IiAqIDc0KQoKICAgIHRvdGFsID0gMAogICAgZm9yIHNlY3Rpb24sIHRpdGxlIGluICgoImV2
ZW50cyIsICJFVkVOVCIpLCAoIm9wZXJhdGlvbnMiLCAiT1BFUkFTWU9OIikpOgogICAgICAgIG9f
YWxsLCBuX2FsbCA9IG9sZC5nZXQoc2VjdGlvbiwge30pLCBuZXcuZ2V0KHNlY3Rpb24sIHt9KQog
ICAgICAgIHJlbW92ZWQgPSBzb3J0ZWQoc2V0KG9fYWxsKSAtIHNldChuX2FsbCksIGtleT1pbnQp
CiAgICAgICAgYWRkZWQgPSBzb3J0ZWQoc2V0KG5fYWxsKSAtIHNldChvX2FsbCksIGtleT1pbnQp
CiAgICAgICAgY2hhbmdlZCA9IFtdCgogICAgICAgIGZvciBjIGluIHNvcnRlZChzZXQob19hbGwp
ICYgc2V0KG5fYWxsKSwga2V5PWludCk6CiAgICAgICAgICAgIG9zXyA9IHNvcnRlZChjb2RlX3Np
ZyhlKSBmb3IgZSBpbiBvX2FsbFtjXSkKICAgICAgICAgICAgbnNfID0gc29ydGVkKGNvZGVfc2ln
KGUpIGZvciBlIGluIG5fYWxsW2NdKQogICAgICAgICAgICBpZiBvc18gIT0gbnNfOgogICAgICAg
ICAgICAgICAgY2hhbmdlZC5hcHBlbmQoYykKCiAgICAgICAgcHJpbnQoIlxuLS0tICVzIC0tLSIg
JSB0aXRsZSkKICAgICAgICBwcmludCgiICBrb2Q6ICVkIC0+ICVkICAgc2luaWY6ICVkIC0+ICVk
ICAgREVHSVNFTjogJWQiICUgKAogICAgICAgICAgICBsZW4ob19hbGwpLCBsZW4obl9hbGwpLAog
ICAgICAgICAgICBzdW0obGVuKHYpIGZvciB2IGluIG9fYWxsLnZhbHVlcygpKSwgc3VtKGxlbih2
KSBmb3IgdiBpbiBuX2FsbC52YWx1ZXMoKSksCiAgICAgICAgICAgIGxlbihjaGFuZ2VkKSArIGxl
bihhZGRlZCkgKyBsZW4ocmVtb3ZlZCkpKQoKICAgICAgICBmb3IgYyBpbiByZW1vdmVkOgogICAg
ICAgICAgICBlID0gb19hbGxbY10KICAgICAgICAgICAgcHJpbnQoIlxuICAtIEtBTERJUklMREkg
WyVzXSAlcyAoJWQgYWxhbikiICUgKGMsIGVbMF0uZ2V0KCJuYW1lIiwgIj8iKSwgZVswXVsiZmll
bGRfY291bnQiXSkpCiAgICAgICAgICAgIHRvdGFsICs9IDEKCiAgICAgICAgZm9yIGMgaW4gYWRk
ZWQ6CiAgICAgICAgICAgIGUgPSBuX2FsbFtjXQogICAgICAgICAgICBwcmludCgiXG4gICsgWUVO
SSBbJXNdICVzICglZCBhbGFuKSIgJSAoYywgZVswXS5nZXQoIm5hbWUiLCAiPyIpLCBlWzBdWyJm
aWVsZF9jb3VudCJdKSkKICAgICAgICAgICAgcHJpbnQoIiAgICAgICB0aXBsZXI6ICVzIiAlIHR5
cGVfbGlzdChlWzBdKSkKICAgICAgICAgICAgdG90YWwgKz0gMQoKICAgICAgICBmb3IgYyBpbiBj
aGFuZ2VkOgogICAgICAgICAgICBvdiwgbnYgPSBvX2FsbFtjXSwgbl9hbGxbY10KICAgICAgICAg
ICAgcHJpbnQoIlxuICAhIERFR0lTVEkgWyVzXSAlcyAgIChlc2tpICVkIHNpbmlmIC0+IHllbmkg
JWQgc2luaWYpIiAlICgKICAgICAgICAgICAgICAgIGMsIG52WzBdLmdldCgibmFtZSIsICI/Iiks
IGxlbihvdiksIGxlbihudikpKQogICAgICAgICAgICBmb3IgZSBpbiBzb3J0ZWQob3YsIGtleT1s
YW1iZGEgeDogeFsiZmllbGRfY291bnQiXSk6CiAgICAgICAgICAgICAgICBwcmludCgiICAgICAg
IGVza2kgKCUyZCBhbGFuKTogJXMiICUgKGVbImZpZWxkX2NvdW50Il0sIHR5cGVfbGlzdChlKSkp
CiAgICAgICAgICAgIGZvciBlIGluIHNvcnRlZChudiwga2V5PWxhbWJkYSB4OiB4WyJmaWVsZF9j
b3VudCJdKToKICAgICAgICAgICAgICAgIHByaW50KCIgICAgICAgeWVuaSAoJTJkIGFsYW4pOiAl
cyIgJSAoZVsiZmllbGRfY291bnQiXSwgdHlwZV9saXN0KGUpKSkKICAgICAgICAgICAgdG90YWwg
Kz0gMQoKICAgIGlmIHRvdGFsID09IDA6CiAgICAgICAgcHJpbnQoIlxuICBGYXJrIHlvayAtIHNl
bWEgYXluaSAoaXNpbWxlciBkaXNpbmRhKS4iKQogICAgcHJpbnQoIlxuPj4+IEdlcmNlayBkZWdp
c2lrbGlrOiAlZCBwYWtldCIgJSB0b3RhbCkKCgojID09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0KIyBLT01VVDogLS1v
cm5lawojID09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT0KZGVmIGNtZF9vcm5laygpOgogICAgKl8sIHNjaGVtYSwgX3Ug
PSBsb2FkKCkKICAgIGZvciBjb2RlIGluICgiNDAiLCAiNDciLCAiNjEiLCAiMTIzIiwgIjMyNiIs
ICIzOTMiLCAiMzEyIiwgIjIxMSIpOgogICAgICAgIGZvciBlIGluIHNjaGVtYVsiZXZlbnRzIl0u
Z2V0KGNvZGUsIFtdKToKICAgICAgICAgICAgcHJpbnQoIlxuW2V2ZW50ICVzXSAlcyAgICglcykg
ICVkIGFsYW4iICUgKGNvZGUsIGVbIm5hbWUiXSwgIiAtPiAiLmpvaW4oZVsiYmFzZV9jaGFpbiJd
KSwgZVsiZmllbGRfY291bnQiXSkpCiAgICAgICAgICAgIGZvciBmIGluIGVbImZpZWxkcyJdWzox
NF06CiAgICAgICAgICAgICAgICBwcmludCgiICAgIGluZGVrcyAlLTJkICAlLThzICUtMTZzICUt
NnMgJXMiICUgKAogICAgICAgICAgICAgICAgICAgIGZbImluZGV4Il0sIGZbIm5hbWUiXSwgZlsi
dHlwZSJdLCBmWyJvZmZzZXQiXSwgS05PV04uZ2V0KGNvZGUsIHt9KS5nZXQoc3RyKGZbImluZGV4
Il0pLCAiIikpKQogICAgZm9yIGNvZGUgaW4gKCIyIiwgIjQxIiwgIjE5NyIpOgogICAgICAgIGZv
ciBlIGluIHNjaGVtYVsib3BlcmF0aW9ucyJdLmdldChjb2RlLCBbXSk6CiAgICAgICAgICAgIHBy
aW50KCJcbltvcGVyYXN5b24gJXNdICVzICAgKCVzKSAgJWQgYWxhbiIgJSAoY29kZSwgZVsibmFt
ZSJdLCAiIC0+ICIuam9pbihlWyJiYXNlX2NoYWluIl0pLCBlWyJmaWVsZF9jb3VudCJdKSkKICAg
ICAgICAgICAgZm9yIGYgaW4gZVsiZmllbGRzIl1bOjEyXToKICAgICAgICAgICAgICAgIHByaW50
KCIgICAgaW5kZWtzICUtMmQgICUtOHMgJS0xNnMgJS02cyIgJSAoZlsiaW5kZXgiXSwgZlsibmFt
ZSJdLCBmWyJ0eXBlIl0sIGZbIm9mZnNldCJdKSkKCgojID09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0KIyBLT01VVDog
LS1zb3psdWsKIyA9PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09CmRlZiBjbWRfc296bHVrKCk6CiAgICAiIiJUVU0ga29k
bGFyIGljaW4gcGFyYW1ldHJlIHNvemx1Z3UgdXJldGlyIChNRCArIFRTVikuIiIiCiAgICAqXywg
c2NoZW1hLCBfdSA9IGxvYWQoKQoKICAgIG1kX3BhdGggPSBvcy5wYXRoLmpvaW4oU0NSSVBUX0RJ
UiwgIlBBUkFNRVRSRUxFUi5tZCIpCiAgICB0c3ZfcGF0aCA9IG9zLnBhdGguam9pbihTQ1JJUFRf
RElSLCAiUEFSQU1FVFJFTEVSLnRzdiIpCgogICAgbWQgPSBbXQogICAgdHN2ID0gWyJib2x1bVx0
a29kXHRhZFx0YWxhbl9zYXlpc2lcdGluZGVrc1x0YWxhblx0dGlwXHRhbmxhbVx0ZG9ncnVsYW5t
aXMiXQoKICAgIG1kLmFwcGVuZCgiIyBOaWdodHdhdGNoIC0gUGFrZXQgUGFyYW1ldHJlIFNvemx1
Z3UiKQogICAgbWQuYXBwZW5kKCIiKQogICAgbWQuYXBwZW5kKCI+IE9UT01BVElLIHVyZXRpbGly
OiBgcHl0aG9uIGFuYWxpei5weSAtLXNvemx1a2AiKQogICAgbWQuYXBwZW5kKCI+IEtheW5hazog
YGR1bXAuY3NgIChveXVudW4ga2VuZGkga29kdSkgKyBjYW5saSBveXVuIGRvZ3J1bGFtYWxhcmki
KQogICAgbWQuYXBwZW5kKCI+IikKICAgIG1kLmFwcGVuZCgiPiAqKmlzYXJldCoqOiBgRVZFVGAg
PSBjYW5saSBveXVuZGEgZG9ncnVsYW5taXMgYW5sYW0gLSBgaGF5aXJgID0gdGlwdGVuIGNpa2Fy
aWxtaXMgdGFobWluIikKICAgIG1kLmFwcGVuZCgiPiIpCiAgICBtZC5hcHBlbmQoIj4gUGFyYW1l
dHJlbGVyIFBob3RvbidkYSAqKm51bGwgaXNlIGhpYyBnb25kZXJpbG1leioqOyBidSB5dXpkZW4g
aW5kZWtzbGVyIGF0bGFuYWJpbGlyLiIpCiAgICBtZC5hcHBlbmQoIj4gT2t1cmtlbiAqKmluZGVr
cyBudW1hcmFzaW5hKiogZ29yZSBva3V5dW4sIGxpc3RlZGVraSBzaXJheWEgZ29yZSBkZWdpbC4i
KQogICAgbWQuYXBwZW5kKCIiKQoKICAgIGZvciBzZWN0aW9uLCB0aXRsZSBpbiAoKCJldmVudHMi
LCAiRVZFTlQiKSwgKCJvcGVyYXRpb25zIiwgIk9QRVJBU1lPTiIpKToKICAgICAgICBtZC5hcHBl
bmQoIiIpCiAgICAgICAgbWQuYXBwZW5kKCIjID09PT09ICVzID09PT09IiAlIHRpdGxlKQogICAg
ICAgIGZvciBjb2RlIGluIHNvcnRlZChzY2hlbWFbc2VjdGlvbl0sIGtleT1pbnQpOgogICAgICAg
ICAgICB2YXJpYW50cyA9IHNjaGVtYVtzZWN0aW9uXVtjb2RlXQogICAgICAgICAgICBmb3Igdmks
IGUgaW4gZW51bWVyYXRlKHZhcmlhbnRzLCAxKToKICAgICAgICAgICAgICAgIG5hbWUgPSBlLmdl
dCgibmFtZSIsICI/IikKICAgICAgICAgICAgICAgIHN1ZmZpeCA9ICIiIGlmIGxlbih2YXJpYW50
cykgPT0gMSBlbHNlICIgICh2YXJ5YW50ICVkLyVkKSIgJSAodmksIGxlbih2YXJpYW50cykpCiAg
ICAgICAgICAgICAgICBtZC5hcHBlbmQoIiIpCiAgICAgICAgICAgICAgICBtZC5hcHBlbmQoIiMj
IFslc10gJXMlcyAgLS0gICVkIGFsYW4iICUgKGNvZGUsIG5hbWUsIHN1ZmZpeCwgZVsiZmllbGRf
Y291bnQiXSkpCiAgICAgICAgICAgICAgICBpZiBzdHIoY29kZSkgaW4gTElWRV9OT1RFUzoKICAg
ICAgICAgICAgICAgICAgICBtZC5hcHBlbmQoIiIpCiAgICAgICAgICAgICAgICAgICAgbWQuYXBw
ZW5kKCI+IFVZQVJJOiAlcyIgJSBMSVZFX05PVEVTW3N0cihjb2RlKV0pCiAgICAgICAgICAgICAg
ICBtZC5hcHBlbmQoIiIpCiAgICAgICAgICAgICAgICBtZC5hcHBlbmQoInwgaW5kZWtzIHwgYWxh
biB8IHRpcCB8IGFubGFtIHwgZG9ncnVsYW5kaSB8IikKICAgICAgICAgICAgICAgIG1kLmFwcGVu
ZCgifC0tLXwtLS18LS0tfC0tLXwtLS18IikKICAgICAgICAgICAgICAgIGZvciBmIGluIGVbImZp
ZWxkcyJdOgogICAgICAgICAgICAgICAgICAgIG1lYW5pbmcsIHZlcmlmaWVkID0gZmllbGRfbWVh
bmluZyhjb2RlLCBmKQogICAgICAgICAgICAgICAgICAgIG1hcmsgPSAiRVZFVCIgaWYgdmVyaWZp
ZWQgZWxzZSAiaGF5aXIiCiAgICAgICAgICAgICAgICAgICAgbWQuYXBwZW5kKCJ8ICVzIHwgYCVz
YCB8ICVzIHwgJXMgfCAlcyB8IiAlICgKICAgICAgICAgICAgICAgICAgICAgICAgZlsiaW5kZXgi
XSwgZlsibmFtZSJdLCBmWyJ0eXBlIl0sIG1lYW5pbmcsIG1hcmspKQogICAgICAgICAgICAgICAg
ICAgIHRzdi5hcHBlbmQoIlx0Ii5qb2luKFsKICAgICAgICAgICAgICAgICAgICAgICAgc2VjdGlv
biwgc3RyKGNvZGUpLCBuYW1lLCBzdHIoZVsiZmllbGRfY291bnQiXSksCiAgICAgICAgICAgICAg
ICAgICAgICAgIHN0cihmWyJpbmRleCJdKSwgZlsibmFtZSJdLCBmWyJ0eXBlIl0sIG1lYW5pbmcs
IG1hcmssCiAgICAgICAgICAgICAgICAgICAgXSkpCgogICAgd2l0aCBvcGVuKG1kX3BhdGgsICJ3
IiwgZW5jb2Rpbmc9InV0Zi04IikgYXMgZmg6CiAgICAgICAgZmgud3JpdGUoIlxuIi5qb2luKG1k
KSArICJcbiIpCiAgICB3aXRoIG9wZW4odHN2X3BhdGgsICJ3IiwgZW5jb2Rpbmc9InV0Zi04Iikg
YXMgZmg6CiAgICAgICAgZmgud3JpdGUoIlxuIi5qb2luKHRzdikgKyAiXG4iKQoKICAgIHRvdGFs
X2NvZGVzID0gbGVuKHNjaGVtYVsiZXZlbnRzIl0pICsgbGVuKHNjaGVtYVsib3BlcmF0aW9ucyJd
KQogICAgdG90YWxfZmllbGRzID0gc3VtKAogICAgICAgIGxlbihlWyJmaWVsZHMiXSkKICAgICAg
ICBmb3IgcyBpbiAoImV2ZW50cyIsICJvcGVyYXRpb25zIikKICAgICAgICBmb3IgdiBpbiBzY2hl
bWFbc10udmFsdWVzKCkKICAgICAgICBmb3IgZSBpbiB2CiAgICApCiAgICBwcmludCgiS29kIHNh
eWlzaSAgOiAlZCIgJSB0b3RhbF9jb2RlcykKICAgIHByaW50KCJBbGFuIHNheWlzaSA6ICVkIiAl
IHRvdGFsX2ZpZWxkcykKICAgIHByaW50KCJZYXppbGRpICAgICA6ICVzIiAlIG1kX3BhdGgpCiAg
ICBwcmludCgiWWF6aWxkaSAgICAgOiAlcyIgJSB0c3ZfcGF0aCkKCgojID09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0K
IyBLT01VVDogLS1kb2dydWxhCiMgPT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PQpkZWYgY21kX2RvZ3J1bGEobG9nbmFt
ZT0iZXZlbnRfd2F0Y2gubG9nIik6CiAgICAiIiJDYW5saSB5YWthbGFtYSBsb2d1bnUgdGFibG95
bGEga2Fyc2lsYXN0aXJpci4iIiIKICAgIHBhdGggPSBsb2duYW1lCiAgICBpZiBub3Qgb3MucGF0
aC5pc2FicyhwYXRoKToKICAgICAgICBjYW5kcyA9IFtvcy5wYXRoLmpvaW4oUk9PVCwgImxvZyIs
IGxvZ25hbWUpLCBvcy5wYXRoLmpvaW4oU0NSSVBUX0RJUiwgbG9nbmFtZSldCiAgICAgICAgcGF0
aCA9IG5leHQoKGMgZm9yIGMgaW4gY2FuZHMgaWYgb3MucGF0aC5leGlzdHMoYykpLCBjYW5kc1sw
XSkKCiAgICBpZiBub3Qgb3MucGF0aC5leGlzdHMocGF0aCk6CiAgICAgICAgcHJpbnQoIltIQVRB
XSBDYW5saSBsb2cgYnVsdW5hbWFkaTogJXMiICUgcGF0aCkKICAgICAgICBwcmludCgiICAgICAg
IFV5Z3VsYW1hIGNhbGlzaXJrZW4gb2x1c2FuIGV2ZW50X3dhdGNoLmxvZyBkb3N5YXNpbmkgYmVr
bGl5b3J1bS4iKQogICAgICAgIHJldHVybgoKICAgICpfLCBzY2hlbWEsIF91ID0gbG9hZCgpCgog
ICAgcmVfZXYgPSByZS5jb21waWxlKHIiXlxbW1xkOl0rXF1ccysoRVZFTlR8T1AtWUFOSVR8T1At
SVNURUspXHMrKFxkKylccys9IikKICAgIHJlX3ByID0gcmUuY29tcGlsZShyIl5ccytcWyhcZCsp
XF1ccys9XHMrLio/PChbXj5dKyk+XHMqJCIpCgogICAgY3VyID0gTm9uZQogICAgY2hlY2tlZCA9
IDAKICAgIG9rID0gMAogICAgYmFkID0gW10KICAgIGNvZGVzID0ge30KCiAgICB3aXRoIG9wZW4o
cGF0aCwgInIiLCBlbmNvZGluZz0idXRmLTgiLCBlcnJvcnM9InJlcGxhY2UiKSBhcyBmaDoKICAg
ICAgICBmb3IgbGluZSBpbiBmaDoKICAgICAgICAgICAgbSA9IHJlX2V2Lm1hdGNoKGxpbmUpCiAg
ICAgICAgICAgIGlmIG06CiAgICAgICAgICAgICAgICBjdXIgPSBtLmdyb3VwKDIpCiAgICAgICAg
ICAgICAgICBjb2Rlc1tjdXJdID0gY29kZXMuZ2V0KGN1ciwgMCkgKyAxCiAgICAgICAgICAgICAg
ICBjb250aW51ZQoKICAgICAgICAgICAgbSA9IHJlX3ByLm1hdGNoKGxpbmUpCiAgICAgICAgICAg
IGlmIG5vdCBtIG9yIGN1ciBpcyBOb25lOgogICAgICAgICAgICAgICAgY29udGludWUKCiAgICAg
ICAgICAgIGlkeCA9IGludChtLmdyb3VwKDEpKQogICAgICAgICAgICByYXcgPSBtLmdyb3VwKDIp
LnN0cmlwKCkKICAgICAgICAgICAgaWYgaWR4IGluICgyNTIsIDI1MywgMjU0KToKICAgICAgICAg
ICAgICAgIGNvbnRpbnVlICAjIHByb3Rva29sIGFsYW5sYXJpCgogICAgICAgICAgICB2YXJpYW50
cyA9IHNjaGVtYVsiZXZlbnRzIl0uZ2V0KGN1cikgb3Igc2NoZW1hWyJvcGVyYXRpb25zIl0uZ2V0
KGN1cikKICAgICAgICAgICAgaWYgbm90IHZhcmlhbnRzOgogICAgICAgICAgICAgICAgYmFkLmFw
cGVuZCgoY3VyLCBpZHgsICIodGFibG9kYSB5b2spIiwgcmF3KSkKICAgICAgICAgICAgICAgIGNv
bnRpbnVlCgogICAgICAgICAgICBzX3R5cGUgPSBOb25lCiAgICAgICAgICAgIGZvciBlIGluIHZh
cmlhbnRzOgogICAgICAgICAgICAgICAgZm9yIGYgaW4gZVsiZmllbGRzIl06CiAgICAgICAgICAg
ICAgICAgICAgaWYgZlsiaW5kZXgiXSA9PSBpZHg6CiAgICAgICAgICAgICAgICAgICAgICAgIHNf
dHlwZSA9IGZbInR5cGUiXQogICAgICAgICAgICAgICAgICAgICAgICBicmVhawogICAgICAgICAg
ICAgICAgaWYgc190eXBlOgogICAgICAgICAgICAgICAgICAgIGJyZWFrCgogICAgICAgICAgICBp
ZiBzX3R5cGUgaXMgTm9uZToKICAgICAgICAgICAgICAgIGJhZC5hcHBlbmQoKGN1ciwgaWR4LCAi
KGluZGVrcyB5b2spIiwgcmF3KSkKICAgICAgICAgICAgICAgIGNvbnRpbnVlCgogICAgICAgICAg
ICBjaGVja2VkICs9IDEKICAgICAgICAgICAgaWYgdHlwZV9vayhzX3R5cGUsIHJhdyk6CiAgICAg
ICAgICAgICAgICBvayArPSAxCiAgICAgICAgICAgIGVsc2U6CiAgICAgICAgICAgICAgICBiYWQu
YXBwZW5kKChjdXIsIGlkeCwgc190eXBlLCByYXcpKQoKICAgIHByaW50KCI9IiAqIDcwKQogICAg
cHJpbnQoIkNBTkxJIExPRyAgPC0+ICBUQUJMTyAgRE9HUlVMQU1BU0kiKQogICAgcHJpbnQoIkth
eW5hayA6ICVzIiAlIHBhdGgpCiAgICBwcmludCgiPSIgKiA3MCkKICAgIHByaW50KCJHb3J1bGVu
IHBha2V0ICA6ICVkIiAlIGxlbihjb2RlcykpCiAgICBwcmludCgiS29udHJvbCBlZGlsZW4gOiAl
ZCBhbGFuIiAlIGNoZWNrZWQpCiAgICBwcmludCgiVXl1c2FuICAgICAgICAgOiAlZCIgJSBvaykK
ICAgIHByaW50KCJVWVVTTUFZQU4gICAgICA6ICVkIiAlIGxlbihiYWQpKQoKICAgIGlmIGJhZDoK
ICAgICAgICBwcmludCgiIikKICAgICAgICBmb3IgYywgaSwgcywgbCBpbiBiYWRbOjQwXToKICAg
ICAgICAgICAgcHJpbnQoIiAgWyVzXSBpbmRla3MgJS0zZCAgdGFibG89JS0xNHMgIGNhbmxpPSVz
IiAlIChjLCBpLCBzLCBsKSkKICAgICAgICBpZiBsZW4oYmFkKSA+IDQwOgogICAgICAgICAgICBw
cmludCgiICAuLi4gKCslZCBzYXRpciBkYWhhKSIgJSAobGVuKGJhZCkgLSA0MCkpCiAgICAgICAg
cHJpbnQoIlxuU29udWM6IEZBUksgVkFSIC0geXVrYXJpZGFraSBpbmRla3NsZXJlIGJhayIpCiAg
ICBlbHNlOgogICAgICAgIHByaW50KCJcblNvbnVjOiBUQUJMTyBHVU5DRUwgKGNhbmxpIHZlcml5
bGUgdGFtIHV5dW1sdSkiKQoKCiMgPT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PQojIEtPTVVUOiAtLWFyYQojID09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT0KZGVmIGNtZF9hcmEoa2VsaW1lKToKICAgICpfLCBzY2hlbWEsIF91ID0gbG9hZCgp
CiAgICBrID0ga2VsaW1lLmxvd2VyKCkKICAgIGhpdHMgPSAwCiAgICBmb3Igc2VjdGlvbiBpbiAo
ImV2ZW50cyIsICJvcGVyYXRpb25zIik6CiAgICAgICAgZm9yIGNvZGUgaW4gc29ydGVkKHNjaGVt
YVtzZWN0aW9uXSwga2V5PWludCk6CiAgICAgICAgICAgIGZvciBlIGluIHNjaGVtYVtzZWN0aW9u
XVtjb2RlXToKICAgICAgICAgICAgICAgIGlmIGsgaW4gZVsibmFtZSJdLmxvd2VyKCkgb3IgayBp
biBlWyJjbGFzcyJdLmxvd2VyKCk6CiAgICAgICAgICAgICAgICAgICAgaGl0cyArPSAxCiAgICAg
ICAgICAgICAgICAgICAgcHJpbnQoIlslLTlzICVzXSAlcyAgKCVzKSAgJWQgYWxhbiIgJSAoc2Vj
dGlvbls6LTFdLCBjb2RlLCBlWyJuYW1lIl0sIGVbImNsYXNzIl0sIGVbImZpZWxkX2NvdW50Il0p
KQogICAgcHJpbnQoIlxuJWQgc29udWMiICUgaGl0cykKCgojID09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0KIyBLT01V
VDogLS1lbnVtCiMgPT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PQpkZWYgY21kX2VudW0oKToKICAgIGR1bXAgPSBwYXJz
ZV9kdW1wX2VudW1zKERVTVApCgogICAgcHJpbnQoIj0iICogNzApCiAgICBwcmludCgiRU5VTSBL
QVJTSUxBU1RJUk1BU0kiKQogICAgcHJpbnQoIj0iICogNzApCgogICAgY3MgPSB7fQogICAgZm9y
IHBhdGggaW4gQ1NfRU5VTV9GSUxFUzoKICAgICAgICBmb3IgbmFtZSwgdmFsdWVzIGluIHBhcnNl
X2NzX2VudW1zKHBhdGgpLml0ZW1zKCk6CiAgICAgICAgICAgIGlmIG5hbWUgbm90IGluIGNzOgog
ICAgICAgICAgICAgICAgY3NbbmFtZV0gPSB2YWx1ZXMKCiAgICBwYWlycyA9IFsKICAgICAgICAo
IkV2ZW50Q29kZXMiLCAiRXZlbnRDb2RlcyIpLAogICAgICAgICgiT3BlcmF0aW9uQ29kZXMiLCAi
T3BlcmF0aW9uQ29kZXMiKSwKICAgICAgICAoIk9wZXJhdGlvbkNvZGVzIiwgIlJlcXVlc3RDb2Rl
cyIpLAogICAgICAgICgiT3BlcmF0aW9uQ29kZXMiLCAiUmVzcG9uc2VDb2RlcyIpLAogICAgXQoK
ICAgIGZvciBkdW1wX25hbWUsIGNzX25hbWUgaW4gcGFpcnM6CiAgICAgICAgZCA9IGR1bXAuZ2V0
KGR1bXBfbmFtZSkgb3Ige30KICAgICAgICBjID0gY3MuZ2V0KGNzX25hbWUpIG9yIHt9CiAgICAg
ICAgaWYgbm90IGQgYW5kIG5vdCBjOgogICAgICAgICAgICBjb250aW51ZQogICAgICAgIGlmIG5v
dCBjOgogICAgICAgICAgICAjIEMjIHRhcmFmaW5kYSBidSBlbnVtIHlvayAob3IuIEFPU25pZmZl
ck5FVCBhcnNpdmUgYWxpbmRpKSAtPiB5YW5saXMgYWxhcm0gdmVybWUKICAgICAgICAgICAgcHJp
bnQoIlxuLS0tICVzIDwtPiAlcyAtLS0gICBbQyMgdGFyYWZpbmRhIGJ1IGVudW0gWU9LIC0+IGF0
bGFuZGldIiAlIChkdW1wX25hbWUsIGNzX25hbWUpKQogICAgICAgICAgICBjb250aW51ZQogICAg
ICAgIHByaW50KCJcbi0tLSAlcyAoZHVtcDogJWQga2F5aXQpICA8LT4gICVzIChDIzogJWQga2F5
aXQpIC0tLSIKICAgICAgICAgICAgICAlIChkdW1wX25hbWUsIGxlbihkKSwgY3NfbmFtZSwgbGVu
KGMpKSkKCiAgICAgICAgbWlzc2luZyA9IHNvcnRlZChzZXQoZCkgLSBzZXQoYykpCiAgICAgICAg
ZXh0cmEgPSBzb3J0ZWQoc2V0KGMpIC0gc2V0KGQpKQogICAgICAgIG1pc21hdGNoID0gc29ydGVk
KGsgZm9yIGsgaW4gKHNldChkKSAmIHNldChjKSkgaWYgZFtrXSAhPSBjW2tdKQoKICAgICAgICBp
ZiBjc19uYW1lIGluIFNVQlNFVF9FTlVNUzoKICAgICAgICAgICAgcHJpbnQoIiAgQyMgdGFyYWZp
bmRhIEVLU0lLIChkdW1wJ3RhIHZhcik6ICVkICAgW0FMVCBLVU1FIG9sYXJhayB0YXNhcmxhbm1p
cyAtPiBub3JtYWxdIgogICAgICAgICAgICAgICAgICAlIGxlbihtaXNzaW5nKSkKICAgICAgICBl
bHNlOgogICAgICAgICAgICBwcmludCgiICBDIyB0YXJhZmluZGEgRUtTSUsgKGR1bXAndGEgdmFy
KTogJWQiICUgbGVuKG1pc3NpbmcpKQogICAgICAgICAgICBmb3IgayBpbiBtaXNzaW5nWzoyNV06
CiAgICAgICAgICAgICAgICBwcmludCgiICAgICArICVzID0gJXMiICUgKGssIGRba10pKQogICAg
ICAgICAgICBpZiBsZW4obWlzc2luZykgPiAyNToKICAgICAgICAgICAgICAgIHByaW50KCIgICAg
IC4uLiAoJWQgdGFuZSBkYWhhKSIgJSAobGVuKG1pc3NpbmcpIC0gMjUpKQoKICAgICAgICBhbGlh
c2VzID0gTkFNRV9BTElBU0VTLmdldChjc19uYW1lLCB7fSkKICAgICAgICByZWFsX2V4dHJhID0g
W2sgZm9yIGsgaW4gZXh0cmEgaWYgayBub3QgaW4gYWxpYXNlc10KICAgICAgICBhbGlhc19leHRy
YSA9IFtrIGZvciBrIGluIGV4dHJhIGlmIGsgaW4gYWxpYXNlc10KCiAgICAgICAgcHJpbnQoIiAg
QyMgdGFyYWZpbmRhIEZBWkxBIChkdW1wJ3RhIHlvayk6ICVkIiAlIGxlbihyZWFsX2V4dHJhKSkK
ICAgICAgICBmb3IgayBpbiByZWFsX2V4dHJhWzoyNV06CiAgICAgICAgICAgIHByaW50KCIgICAg
IC0gJXMgPSAlcyIgJSAoaywgY1trXSkpCiAgICAgICAgaWYgbGVuKHJlYWxfZXh0cmEpID4gMjU6
CiAgICAgICAgICAgIHByaW50KCIgICAgIC4uLiAoJWQgdGFuZSBkYWhhKSIgJSAobGVuKHJlYWxf
ZXh0cmEpIC0gMjUpKQoKICAgICAgICBpZiBhbGlhc19leHRyYToKICAgICAgICAgICAgcHJpbnQo
IiAgVEFLTUEgQUQgKGJpbGVyZWsgZmFya2xpIGlzaW07IG95dW5kYWtpIGthcnNpbGlnaXlsYSBk
b2dydWxhbmlyKTogJWQiICUgbGVuKGFsaWFzX2V4dHJhKSkKICAgICAgICAgICAgZm9yIGsgaW4g
YWxpYXNfZXh0cmE6CiAgICAgICAgICAgICAgICB0YXJnZXQgPSBhbGlhc2VzW2tdCiAgICAgICAg
ICAgICAgICBpZiB0YXJnZXQgaW4gZDoKICAgICAgICAgICAgICAgICAgICBtYXJrID0gIk9LIGF5
bmkgZGVnZXIiIGlmIGRbdGFyZ2V0XSA9PSBjW2tdIGVsc2UgIiEhIERFR0VSIEZBUktMSSIKICAg
ICAgICAgICAgICAgICAgICBwcmludCgiICAgICB+ICVzID0gJXMgIC0+ICBveXVuOiAlcyA9ICVz
ICAgWyVzXSIgJSAoaywgY1trXSwgdGFyZ2V0LCBkW3RhcmdldF0sIG1hcmspKQogICAgICAgICAg
ICAgICAgZWxzZToKICAgICAgICAgICAgICAgICAgICBwcmludCgiICAgICB+ICVzID0gJXMgIC0+
ICBveXVuOiAlcyAgW2R1bXAndGEgeW9rIV0iICUgKGssIGNba10sIHRhcmdldCkpCgogICAgICAg
IHByaW50KCIgIERFR0VSIEZBUktMSTogJWQiICUgbGVuKG1pc21hdGNoKSkKICAgICAgICBmb3Ig
ayBpbiBtaXNtYXRjaFs6NDBdOgogICAgICAgICAgICBwcmludCgiICAgICAhICVzICAtPiAgZHVt
cDogJXMgICBzZW5pbiBrb2Q6ICVzIiAlIChrLCBkW2tdLCBjW2tdKSkKICAgICAgICBpZiBsZW4o
bWlzbWF0Y2gpID4gNDA6CiAgICAgICAgICAgIHByaW50KCIgICAgIC4uLiAoJWQgdGFuZSBkYWhh
KSIgJSAobGVuKG1pc21hdGNoKSAtIDQwKSkKCgojID09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0KIyBLT01VVDogLS1t
b2JkYgojID09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT0KZGVmIGxvYWRfanNvbl9hcnJheShwYXRoKToKICAgIHdpdGgg
b3BlbihwYXRoLCAiciIsIGVuY29kaW5nPSJ1dGYtOCIpIGFzIGY6CiAgICAgICAgcmV0dXJuIGpz
b24ubG9hZChmKQoKCmRlZiB1bmlxdWVfb2YoaXRlbSk6CiAgICAiIiIidSIgYWxhbmkgPSBkaWwg
YmFnaW1zaXogaWMgaXNpbSAoa2F5bWEga29udHJvbHUgYnVudSBrdWxsYW5pcikuIiIiCiAgICBp
ZiBub3QgaXNpbnN0YW5jZShpdGVtLCBkaWN0KToKICAgICAgICByZXR1cm4gTm9uZQogICAgcmV0
dXJuIGl0ZW0uZ2V0KCJ1IikKCgpkZWYgY21kX21vYmRiKCk6CiAgICBwcmludCgiPSIgKiA3MCkK
ICAgIHByaW50KCJNT0IgREIgS0FSU0lMQVNUSVJNQVNJIChpbmRleCBrYXltYXNpIGtvbnRyb2x1
KSIpCiAgICBwcmludCgiPSIgKiA3MCkKCiAgICBiYXNlX3BhdGggPSBvcy5wYXRoLmpvaW4oSEVM
UEVSLCAibW9ic19FTl9taW4uanNvbiIpCiAgICBpZiBub3Qgb3MucGF0aC5leGlzdHMoYmFzZV9w
YXRoKToKICAgICAgICBwcmludCgiW0hBVEFdIG1vYnNfRU5fbWluLmpzb24gYnVsdW5hbWFkaTog
JXMiICUgYmFzZV9wYXRoKQogICAgICAgIHJldHVybgogICAgYmFzZSA9IGxvYWRfanNvbl9hcnJh
eShiYXNlX3BhdGgpCiAgICBwcmludCgiRU4ga2F5aXQgc2F5aXNpOiAlZCIgJSBsZW4oYmFzZSkp
CiAgICBwcmludCgiTk9UOiBLb2RkYSBUeXBlSWQgPSBpbmRleCArIDE2IHNla2xpbmRlIGhlc2Fw
bGFuaXlvci4iKQogICAgcHJpbnQoIiAgICAgWWFuaSBiaXIga2F5bWEsIG8gc2lyYWRhbiBzb25y
YWtpIFRVTSBtb2JsYXJpbiBraW1saWdpbmkgYm96YXIuXG4iKQoKICAgIGZvciBsYW5nIGluICgi
VFIiLCAiUlUiLCAiWkgiKToKICAgICAgICBwYXRoID0gb3MucGF0aC5qb2luKEhFTFBFUiwgIm1v
YnNfJXNfbWluLmpzb24iICUgbGFuZykKICAgICAgICBpZiBub3Qgb3MucGF0aC5leGlzdHMocGF0
aCk6CiAgICAgICAgICAgIHByaW50KCIlczogZG9zeWEgeW9rIC0+IEVOJ2UgZHVzdXlvciAoc29y
dW4gb2xtYXopIiAlIGxhbmcpCiAgICAgICAgICAgIGNvbnRpbnVlCiAgICAgICAgdHJ5OgogICAg
ICAgICAgICBvdGhlciA9IGxvYWRfanNvbl9hcnJheShwYXRoKQogICAgICAgIGV4Y2VwdCBFeGNl
cHRpb24gYXMgZXg6CiAgICAgICAgICAgIHByaW50KCIlczogT0tVTkFNQURJICglcykiICUgKGxh
bmcsIGV4KSkKICAgICAgICAgICAgY29udGludWUKCiAgICAgICAgcHJpbnQoIiVzIGtheWl0IHNh
eWlzaTogJWQgIChFTiBpbGUgZmFyazogJStkKSIgJSAobGFuZywgbGVuKG90aGVyKSwgbGVuKG90
aGVyKSAtIGxlbihiYXNlKSkpCgogICAgICAgIGxpbWl0ID0gbWluKGxlbihiYXNlKSwgbGVuKG90
aGVyKSkKICAgICAgICBkaWZmcyA9IFtpIGZvciBpIGluIHJhbmdlKGxpbWl0KSBpZiB1bmlxdWVf
b2YoYmFzZVtpXSkgIT0gdW5pcXVlX29mKG90aGVyW2ldKV0KICAgICAgICBwcmludCgiICBrYXlt
YSBvbGFuIGluZGV4IHNheWlzaTogJWQiICUgbGVuKGRpZmZzKSkKICAgICAgICBmb3IgaSBpbiBk
aWZmc1s6MTBdOgogICAgICAgICAgICBwcmludCgiICAgICBpbmRleCAlZCAoVHlwZUlkICVkKTog
IEVOPSclcycgICAlcz0nJXMnIgogICAgICAgICAgICAgICAgICAlIChpLCBpICsgMTYsIHVuaXF1
ZV9vZihiYXNlW2ldKSwgbGFuZywgdW5pcXVlX29mKG90aGVyW2ldKSkpCiAgICAgICAgaWYgbGVu
KGRpZmZzKSA+IDEwOgogICAgICAgICAgICBwcmludCgiICAgICAuLi4gKCVkIHRhbmUgZGFoYSki
ICUgKGxlbihkaWZmcykgLSAxMCkpCgogICAgICAgIGlmIGxlbihvdGhlcikgPCBsZW4oYmFzZSk6
CiAgICAgICAgICAgIHByaW50KCIgIERJS0tBVDogJXMgZG9zeWFzaSAlZCBrYXlpdCBFS1NJSyAt
PiBzb24gJWQgVHlwZUlkIGhpYyB5dWtsZW5taXlvciEiCiAgICAgICAgICAgICAgICAgICUgKGxh
bmcsIGxlbihiYXNlKSAtIGxlbihvdGhlciksIGxlbihiYXNlKSAtIGxlbihvdGhlcikpKQoKCiMg
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PQojIEtPTVVUOiAtLWtvZCAgKGtvZCA8LT4gdGFibG8gZGVuZXRpbWkpCiMg
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PQpSRV9NRVRIT0QgPSByZS5jb21waWxlKHIiXlxzKig/OnB1YmxpY3xwcml2
YXRlfHByb3RlY3RlZHxpbnRlcm5hbClccysoPzpzdGF0aWNccyspPyg/OmFzeW5jXHMrKT8iCiAg
ICAgICAgICAgICAgICAgICAgICAgciIoPzpvdmVycmlkZVxzKyk/KD86dmlydHVhbFxzKyk/Igog
ICAgICAgICAgICAgICAgICAgICAgIHIiKD86dm9pZHxib29sfGludHxzdHJpbmd8ZmxvYXR8bG9u
Z3xkb3VibGV8VGFza3xUYXNrPFx3Kz4pXHMrKFx3KylccypcKCIpClJFX0NBU0UgPSByZS5jb21w
aWxlKHIiY2FzZVxzKig/OlwoaW50XClccyopPyg/OkV2ZW50Q29kZXN8UmVxdWVzdENvZGVzfFJl
c3BvbnNlQ29kZXMpXC4oXHcrKVxzKjoiKQpSRV9JRiA9IHJlLmNvbXBpbGUociIoPzpldmVudENv
ZGV8cmVzcG9uc2VDb2RlfHJlcUNvZGV8b3BlcmF0aW9uQ29kZXxvcENvZGV8cEV2ZW50Q29kZXxw
T3BDb2RlfGNvZGUpIgogICAgICAgICAgICAgICAgICAgciJccyo9PVxzKig/OlwoaW50XClccyop
Pyg/OkV2ZW50Q29kZXN8UmVxdWVzdENvZGVzfFJlc3BvbnNlQ29kZXMpXC4oXHcrKSIpClJFX0NB
TEwgPSByZS5jb21waWxlKHIiXGIoW0EtWl1cdyspXHMqXChccypbQS1aYS16X11cdypccypcKSIp
ClJFX1JFQUQgPSByZS5jb21waWxlKHIiRXh0cmFjdFZhbHVlPChbXHdcW1xdXSspPlxzKlwoXHMq
XHcrXHMqLFxzKihcZCspIikKUkVfUkVBRDIgPSByZS5jb21waWxlKHIiVHJ5R2V0VmFsdWVcKFxz
KihcZCspXHMqLCIpClJFX1JFQUQzID0gcmUuY29tcGlsZShyInBhcmFtZXRlcnNcWyhcZCspXF0i
KQoKCmRlZiBzcGxpdF9tZXRob2RzKGxpbmVzKToKICAgICIiIlsoYWQsIGJhc2xhbmdpY19zYXRp
ciwgYml0aXNfc2F0aXIpXSBkb25kdXJ1ciAoMCB0YWJhbmxpKS4iIiIKICAgIHN0YXJ0cyA9IFtd
CiAgICBmb3IgaSwgbGluZSBpbiBlbnVtZXJhdGUobGluZXMpOgogICAgICAgIG0gPSBSRV9NRVRI
T0QubWF0Y2gobGluZSkKICAgICAgICBpZiBtOgogICAgICAgICAgICBzdGFydHMuYXBwZW5kKCht
Lmdyb3VwKDEpLCBpKSkKICAgIG91dCA9IFtdCiAgICBmb3IgaywgKG5hbWUsIHN0YXJ0KSBpbiBl
bnVtZXJhdGUoc3RhcnRzKToKICAgICAgICBlbmQgPSBzdGFydHNbayArIDFdWzFdIGlmIGsgKyAx
IDwgbGVuKHN0YXJ0cykgZWxzZSBsZW4obGluZXMpCiAgICAgICAgb3V0LmFwcGVuZCgobmFtZSwg
c3RhcnQsIGVuZCkpCiAgICByZXR1cm4gb3V0CgoKZGVmIHNjYW5fZmlsZShwYXRoKToKICAgIHdp
dGggb3BlbihwYXRoLCAiciIsIGVuY29kaW5nPSJ1dGYtOCIsIGVycm9ycz0icmVwbGFjZSIpIGFz
IGY6CiAgICAgICAgbGluZXMgPSBmLnJlYWRsaW5lcygpCgogICAgbWV0aG9kcyA9IHNwbGl0X21l
dGhvZHMobGluZXMpCgogICAgIyAxKSBkaXNwYXRjaGVyOiBjYXNlIC0+IGhlZGVmIG1ldG90CiAg
ICBtZXRob2RfY29kZXMgPSB7fQogICAgZm9yIG5hbWUsIHN0YXJ0LCBlbmQgaW4gbWV0aG9kczoK
ICAgICAgICBib2R5ID0gbGluZXNbc3RhcnQ6ZW5kXQogICAgICAgIGNhc2VfaGl0cyA9IHN1bSgx
IGZvciBsIGluIGJvZHkgaWYgUkVfQ0FTRS5zZWFyY2gobCkgb3IgUkVfSUYuc2VhcmNoKGwpKQog
ICAgICAgIGlmIGNhc2VfaGl0cyA9PSAwOgogICAgICAgICAgICBjb250aW51ZQogICAgICAgIHBl
bmRpbmcgPSBbXQogICAgICAgIGZvciBsIGluIGJvZHk6CiAgICAgICAgICAgIG0gPSBSRV9DQVNF
LnNlYXJjaChsKSBvciBSRV9JRi5zZWFyY2gobCkKICAgICAgICAgICAgaWYgbToKICAgICAgICAg
ICAgICAgIHBlbmRpbmcuYXBwZW5kKG0uZ3JvdXAoMSkpCiAgICAgICAgICAgICAgICBjb250aW51
ZQogICAgICAgICAgICBjID0gUkVfQ0FMTC5zZWFyY2gobCkKICAgICAgICAgICAgaWYgYyBhbmQg
cGVuZGluZzoKICAgICAgICAgICAgICAgIHRndCA9IGMuZ3JvdXAoMSkKICAgICAgICAgICAgICAg
IGlmIHRndCAhPSBuYW1lOgogICAgICAgICAgICAgICAgICAgIG1ldGhvZF9jb2Rlcy5zZXRkZWZh
dWx0KHRndCwgc2V0KCkpLnVwZGF0ZShwZW5kaW5nKQogICAgICAgICAgICAgICAgcGVuZGluZyA9
IFtdCiAgICAgICAgICAgICAgICBjb250aW51ZQogICAgICAgICAgICBpZiBsLnN0cmlwKCkuc3Rh
cnRzd2l0aCgiYnJlYWsiKToKICAgICAgICAgICAgICAgIHBlbmRpbmcgPSBbXQoKICAgICMgMikg
aGVyIG1ldG90dGFraSBva3VtYWxhcgogICAgcmVhZHMgPSB7fQogICAgZm9yIG5hbWUsIHN0YXJ0
LCBlbmQgaW4gbWV0aG9kczoKICAgICAgICByb3dzID0gW10KICAgICAgICBmb3IgaSBpbiByYW5n
ZShzdGFydCwgZW5kKToKICAgICAgICAgICAgbGluZSA9IGxpbmVzW2ldCiAgICAgICAgICAgIGZv
dW5kID0gRmFsc2UKICAgICAgICAgICAgZm9yIHIgaW4gUkVfUkVBRC5maW5kaXRlcihsaW5lKToK
ICAgICAgICAgICAgICAgIHJvd3MuYXBwZW5kKChpICsgMSwgaW50KHIuZ3JvdXAoMikpLCByLmdy
b3VwKDEpKSkKICAgICAgICAgICAgICAgIGZvdW5kID0gVHJ1ZQogICAgICAgICAgICBpZiBmb3Vu
ZDoKICAgICAgICAgICAgICAgIGNvbnRpbnVlCiAgICAgICAgICAgIGZvciByIGluIGxpc3QoUkVf
UkVBRDIuZmluZGl0ZXIobGluZSkpICsgbGlzdChSRV9SRUFEMy5maW5kaXRlcihsaW5lKSk6CiAg
ICAgICAgICAgICAgICByb3dzLmFwcGVuZCgoaSArIDEsIGludChyLmdyb3VwKDEpKSwgIiIpKQog
ICAgICAgIGlmIHJvd3M6CiAgICAgICAgICAgIHJlYWRzW25hbWVdID0gcm93cwoKICAgIHJldHVy
biBtZXRob2RzLCBtZXRob2RfY29kZXMsIHJlYWRzCgoKZGVmIGNtZF9rb2Qoc2hvd19hbGw9RmFs
c2UsIHNob3dfbWFwPUZhbHNlKToKICAgIGVudW1zID0gbG9hZF9jc19lbnVtX2NvZGVzKCkKICAg
IHNjaGVtYSA9IGxvYWRfc2NoZW1hKCkKCiAgICB0b3RhbF9yZWFkcyA9IDAKICAgIHRvdGFsX2Jh
ZCA9IDAKICAgIHVucmVzb2x2ZWQgPSBbXQoKICAgIHByaW50KCI9IiAqIDc2KQogICAgcHJpbnQo
IktPRCA8LT4gVEFCTE8gREVORVRJTUkgKG1ldG9kIGthcHNhbWxpKSIpCiAgICBwcmludCgiPSIg
KiA3NikKCiAgICBmb3IgZGlycGF0aCwgX2QsIGZpbGVzIGluIG9zLndhbGsoS09EX0tPSyk6CiAg
ICAgICAgaWYgIm9iaiIgaW4gZGlycGF0aCBvciAiYmluIiBpbiBkaXJwYXRoOgogICAgICAgICAg
ICBjb250aW51ZQogICAgICAgIGZvciBmbmFtZSBpbiBzb3J0ZWQoZmlsZXMpOgogICAgICAgICAg
ICBpZiBub3QgZm5hbWUuZW5kc3dpdGgoIi5jcyIpOgogICAgICAgICAgICAgICAgY29udGludWUK
ICAgICAgICAgICAgcGF0aCA9IG9zLnBhdGguam9pbihkaXJwYXRoLCBmbmFtZSkKICAgICAgICAg
ICAgbWV0aG9kcywgbWV0aG9kX2NvZGVzLCByZWFkcyA9IHNjYW5fZmlsZShwYXRoKQogICAgICAg
ICAgICBwcmludGVkX2hlYWRlciA9IEZhbHNlCgogICAgICAgICAgICBmb3IgbW5hbWUsIHJvd3Mg
aW4gc29ydGVkKHJlYWRzLml0ZW1zKCkpOgogICAgICAgICAgICAgICAgY29kZXMgPSBtZXRob2Rf
Y29kZXMuZ2V0KG1uYW1lKQogICAgICAgICAgICAgICAgdGFyZ2V0cyA9IFtdCiAgICAgICAgICAg
ICAgICBmb3IgY25hbWUgaW4gKGNvZGVzIG9yIFtdKToKICAgICAgICAgICAgICAgICAgICBmb3Ig
cHJlZml4LCBzZWN0aW9uIGluICgoIkV2ZW50Q29kZXMiLCAiZXZlbnRzIiksCiAgICAgICAgICAg
ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgKCJSZXF1ZXN0Q29kZXMiLCAib3BlcmF0
aW9ucyIpLAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICgiUmVz
cG9uc2VDb2RlcyIsICJvcGVyYXRpb25zIikpOgogICAgICAgICAgICAgICAgICAgICAgICBjb2Rl
ID0gZW51bXMuZ2V0KCIlcy4lcyIgJSAocHJlZml4LCBjbmFtZSkpCiAgICAgICAgICAgICAgICAg
ICAgICAgIGlmIGNvZGUgaXMgbm90IE5vbmUgYW5kIHN0cihjb2RlKSBpbiBzY2hlbWEuZ2V0KHNl
Y3Rpb24sIHt9KToKICAgICAgICAgICAgICAgICAgICAgICAgICAgIHRhcmdldHMuYXBwZW5kKChz
ZWN0aW9uLCBjb2RlLCBzY2hlbWFbc2VjdGlvbl1bc3RyKGNvZGUpXSkpCiAgICAgICAgICAgICAg
ICAgICAgICAgICAgICBicmVhawogICAgICAgICAgICAgICAgICAgIGVsc2U6CiAgICAgICAgICAg
ICAgICAgICAgICAgIHVucmVzb2x2ZWQuYXBwZW5kKChmbmFtZSwgbW5hbWUsIGNuYW1lKSkKCiAg
ICAgICAgICAgICAgICBpZiBub3QgdGFyZ2V0czoKICAgICAgICAgICAgICAgICAgICBjb250aW51
ZQoKICAgICAgICAgICAgICAgIGZpbGVfZmluZGluZ3MgPSBbXQogICAgICAgICAgICAgICAgZm9y
IChsaW5lLCBpZHgsIHJ0eXBlKSBpbiByb3dzOgogICAgICAgICAgICAgICAgICAgIHRvdGFsX3Jl
YWRzICs9IDEKICAgICAgICAgICAgICAgICAgICBiZXN0ID0gTm9uZQogICAgICAgICAgICAgICAg
ICAgIGZvciAoc2VjdGlvbiwgY29kZSwgdmFyaWFudHMpIGluIHRhcmdldHM6CiAgICAgICAgICAg
ICAgICAgICAgICAgIGZvciB2IGluIHZhcmlhbnRzOgogICAgICAgICAgICAgICAgICAgICAgICAg
ICAgaWYgaWR4IDwgdlsiZmllbGRfY291bnQiXToKICAgICAgICAgICAgICAgICAgICAgICAgICAg
ICAgICB0dCA9IHZbImZpZWxkcyJdW2lkeF1bInR5cGUiXQogICAgICAgICAgICAgICAgICAgICAg
ICAgICAgICAgIHN0LCBub3RlID0gY29tcGF0aWJsZShydHlwZSwgdHQpCiAgICAgICAgICAgICAg
ICAgICAgICAgICAgICAgICAgY2FuZCA9IChzdCwgdHQsIHZbImNsYXNzIl0sIGNvZGUsIHZbIm5h
bWUiXSwgbm90ZSkKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICBpZiBzdCA9PSAib2si
OgogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICBiZXN0ID0gY2FuZAogICAgICAg
ICAgICAgICAgICAgICAgICAgICAgICAgICAgICBicmVhawogICAgICAgICAgICAgICAgICAgICAg
ICAgICAgICAgIGlmIGJlc3QgaXMgTm9uZToKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg
ICAgICAgYmVzdCA9IGNhbmQKICAgICAgICAgICAgICAgICAgICAgICAgaWYgYmVzdCBhbmQgYmVz
dFswXSA9PSAib2siOgogICAgICAgICAgICAgICAgICAgICAgICAgICAgYnJlYWsKICAgICAgICAg
ICAgICAgICAgICBpZiBiZXN0IGlzIE5vbmU6CiAgICAgICAgICAgICAgICAgICAgICAgIHRvdGFs
X2JhZCArPSAxCiAgICAgICAgICAgICAgICAgICAgICAgIG14ID0gbWF4KHZbImZpZWxkX2NvdW50
Il0gZm9yIChfcywgX2MsIHZzKSBpbiB0YXJnZXRzIGZvciB2IGluIHZzKQogICAgICAgICAgICAg
ICAgICAgICAgICBmaWxlX2ZpbmRpbmdzLmFwcGVuZCgoImJhZCIsIGxpbmUsIGlkeCwgcnR5cGUs
ICItIiwgIi0iLAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg
IkFMQU4gWU9LIChlbiBidXl1ayB2YXJ5YW50ICVkIGFsYW4pIiAlIG14KSkKICAgICAgICAgICAg
ICAgICAgICBlbHNlOgogICAgICAgICAgICAgICAgICAgICAgICBzdCwgdHQsIHZjbGFzcywgY29k
ZSwgdm5hbWUsIG5vdGUgPSBiZXN0CiAgICAgICAgICAgICAgICAgICAgICAgIGlmIHN0ID09ICJi
YWQiOgogICAgICAgICAgICAgICAgICAgICAgICAgICAgdG90YWxfYmFkICs9IDEKICAgICAgICAg
ICAgICAgICAgICAgICAgZmlsZV9maW5kaW5ncy5hcHBlbmQoKHN0LCBsaW5lLCBpZHgsIHJ0eXBl
LCB0dCwgdmNsYXNzLCBub3RlKSkKCiAgICAgICAgICAgICAgICBpZiBub3QgZmlsZV9maW5kaW5n
czoKICAgICAgICAgICAgICAgICAgICBjb250aW51ZQogICAgICAgICAgICAgICAgaWYgbm90IHNo
b3dfYWxsIGFuZCBhbGwoZlswXSA9PSAib2siIGZvciBmIGluIGZpbGVfZmluZGluZ3MpOgogICAg
ICAgICAgICAgICAgICAgIGNvbnRpbnVlCgogICAgICAgICAgICAgICAgaWYgbm90IHByaW50ZWRf
aGVhZGVyOgogICAgICAgICAgICAgICAgICAgIHByaW50KCJcbiVzIiAlIGZuYW1lKQogICAgICAg
ICAgICAgICAgICAgIHByaW50ZWRfaGVhZGVyID0gVHJ1ZQogICAgICAgICAgICAgICAgY29kZV90
eHQgPSAiLCAiLmpvaW4oIiVzPSVzIiAlICh0WzJdWzBdWyJuYW1lIl0sIHRbMV0pIGZvciB0IGlu
IHRhcmdldHMpCiAgICAgICAgICAgICAgICBwcmludCgiICAlcyAgIFslc10iICUgKG1uYW1lLCBj
b2RlX3R4dCkpCiAgICAgICAgICAgICAgICBmb3IgKHN0LCBsaW5lLCBpZHgsIHJ0eXBlLCB0dCwg
dmNsYXNzLCBub3RlKSBpbiBmaWxlX2ZpbmRpbmdzOgogICAgICAgICAgICAgICAgICAgIGlmIHN0
ID09ICJvayI6CiAgICAgICAgICAgICAgICAgICAgICAgIGlmIHNob3dfYWxsOgogICAgICAgICAg
ICAgICAgICAgICAgICAgICAgcHJpbnQoIiAgICAgT0sgIHNhdGlyICUtNWQgaW5kZWtzICUtM2Qg
JS0xMHMgPSAlLTE2cyAoJXMpIiAlICgKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICBs
aW5lLCBpZHgsIHJ0eXBlIG9yICI/IiwgdHQsIHZjbGFzcykpCiAgICAgICAgICAgICAgICAgICAg
ZWxpZiBzdCA9PSAiYmFkIjoKICAgICAgICAgICAgICAgICAgICAgICAgcHJpbnQoIiAgICAgWCAg
IHNhdGlyICUtNWQgaW5kZWtzICUtM2QgJS0xMHMgLT4gdGFibG86ICUtMTZzICglcykgICVzIiAl
ICgKICAgICAgICAgICAgICAgICAgICAgICAgICAgIGxpbmUsIGlkeCwgcnR5cGUgb3IgIj8iLCB0
dCwgdmNsYXNzLCBub3RlKSkKICAgICAgICAgICAgICAgICAgICBlbHNlOgogICAgICAgICAgICAg
ICAgICAgICAgICBwcmludCgiICAgICAhICAgc2F0aXIgJS01ZCBpbmRla3MgJS0zZCAlLTEwcyAt
PiB0YWJsbzogJS0xNnMgKCVzKSAgJXMiICUgKAogICAgICAgICAgICAgICAgICAgICAgICAgICAg
bGluZSwgaWR4LCBydHlwZSBvciAiPyIsIHR0LCB2Y2xhc3MsIG5vdGUpKQoKICAgIGlmIHNob3df
bWFwOgogICAgICAgIHByaW50KCJcbiIgKyAiPSIgKiA3NikKICAgICAgICBwcmludCgiTUVUT1Qg
LT4gRVZFTlQgRVNMRU1FU0kiKQogICAgICAgIHByaW50KCI9IiAqIDc2KQogICAgICAgIGZvciBk
aXJwYXRoLCBfZCwgZmlsZXMgaW4gb3Mud2FsayhLT0RfS09LKToKICAgICAgICAgICAgaWYgIm9i
aiIgaW4gZGlycGF0aCBvciAiYmluIiBpbiBkaXJwYXRoOgogICAgICAgICAgICAgICAgY29udGlu
dWUKICAgICAgICAgICAgZm9yIGZuYW1lIGluIHNvcnRlZChmaWxlcyk6CiAgICAgICAgICAgICAg
ICBpZiBub3QgZm5hbWUuZW5kc3dpdGgoIi5jcyIpOgogICAgICAgICAgICAgICAgICAgIGNvbnRp
bnVlCiAgICAgICAgICAgICAgICBfbSwgbWMsIF9yID0gc2Nhbl9maWxlKG9zLnBhdGguam9pbihk
aXJwYXRoLCBmbmFtZSkpCiAgICAgICAgICAgICAgICBmb3IgayBpbiBzb3J0ZWQobWMpOgogICAg
ICAgICAgICAgICAgICAgIHByaW50KCIlLTIycyAlLTI4cyAlcyIgJSAoZm5hbWUsIGssICIsICIu
am9pbihzb3J0ZWQobWNba10pKSkpCgogICAgcHJpbnQoIlxuIiArICI9IiAqIDc2KQogICAgcHJp
bnQoIk9aRVQiKQogICAgcHJpbnQoIj0iICogNzYpCiAgICBwcmludCgiSW5jZWxlbmVuIG9rdW1h
ICAgIDogJWQiICUgdG90YWxfcmVhZHMpCiAgICBwcmludCgiS2VzaW4gdXl1bXN1emx1ayBYIDog
JWQiICUgdG90YWxfYmFkKQogICAgaWYgdW5yZXNvbHZlZDoKICAgICAgICBwcmludCgiQ296dWxl
bWV5ZW4gYmFnbGFtIDogJWQgICVzIiAlIChsZW4odW5yZXNvbHZlZCksIHVucmVzb2x2ZWRbOjZd
KSkKICAgIHByaW50KCkKICAgIHByaW50KCJOT1Q6IHlhbG5pemNhIERJU1BBVENIRVIgaWxlIG1l
dG9kIGVzbGVzbWVzaSBrdXJ1bGFiaWxlbiBva3VtYWxhciBkZW5ldGxlbmlyLiIpCiAgICBwcmlu
dCgiICAgICBFc2xlc21lIGt1cnVsYW1heWFuIG1ldG90bGFyICdjb3p1bGVtZXllbiBiYWdsYW0n
IHNheWlsaXIgKHlhbmxpcyBhbGFybSB1cmV0bWV6KS4iKQoKCiMgPT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PQojIEtP
TVVUOiAtLWJpbGdpICAoa29udWxhcmluIGtvbnNvbCBvemV0aSkKIyA9PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09CmRl
ZiBjbWRfYmlsZ2koKToKICAgIEwgPSBbXQogICAgYSA9IEwuYXBwZW5kCiAgICBhKCI9IiAqIDc0
KQogICAgYSgiTklHSFRXQVRDSCAtIFBBS0VUIEFOQUxJWkk6IEJJTElOTUVTSSBHRVJFS0VOTEVS
IikKICAgIGEoIj0iICogNzQpCgogICAgYSgiIikKICAgIGEoIjEpIFVDIEtBVE1BTiAobmV5aSBi
aWxpeW9ydXo/KSIpCiAgICBhKCIgICBLT0RMQVIgICA6IDcwMiBldmVudCArIDU0NSBvcGVyYXN5
b24gPSAxMjQ3IGtvZCAgICAtPiBLRVNJTiAoY2FubGkgZG9ncnVsYW5kaSkiKQogICAgYSgiICAg
QUxBTkxBUiAgOiA3NDkyIGFsYW4gKGluZGVrcyAtPiB0aXApICAgICAgICAgICAgICAgLT4gS0VT
SU4gKGR1bXAndGFuKSIpCiAgICBhKCIgICBBTkxBTUxBUiA6IDkga29kIGNhbmxpIGRvZ3J1bGFu
ZGksIGdlcmlzaSB0YWhtaW4gICAgLT4gS0lTTUkiKQoKICAgIGEoIiIpCiAgICBhKCIyKSBURUwg
Rk9STUFUSSAob24tdGhlLXdpcmUpIikKICAgIGEoIiAgIC0gZHVtcC5jcyAgPSBveXVudW4gSUMg
c2luaWZpICAoJzkgYWxhbmkgdmFyJykiKQogICAgYSgiICAgLSB0ZWwgICAgICA9IHBha2V0dGUg
R0VSQ0VLVEVOIGdlbGVuIHNvemx1ayAoaW5kZWtzIC0+IGRlZ2VyICsgdGlwKSIpCiAgICBhKCIg
ICAtIFBob3RvbiBOVUxMIGFsYW5sYXJpIEdPTkRFUk1FWiAtPiBpbmRla3NsZXIgYXRsYXIiKQog
ICAgYSgiICAgICA+Pj4gT2t1cmtlbiBJTkRFS1MgTlVNQVJBU0lOQSBnb3JlIG9rdSwgbGlzdGVk
ZWtpIHNpcmF5YSBnb3JlIERFR0lMLiIpCiAgICBhKCIgICAtIEJhemkgcGFrZXRsZXIgUEFLRVRM
RU5NSVMgZ2VsaXI6IGF6IHBhcmFtZXRyZSArIGJ5dGVbXSBibG9iIikKICAgIGEoIiAgICAgS2Fu
aXQ6IE1vdmUgKDMpIC0+IHNpbmlmdGEgOSBhbGFuIHZhciwgdGVsZGUgc2FkZWNlIFswXSBsb25n
ICsgWzFdIGJ5dGVbMjYtMzBdIikKICAgIGEoIiAgIC0gRG9ncnVsYW5hbmxhcjogMzkzIChjaGVz
dCksIDMxMiAobW91bnQpLCAyMTEgKG1vdW50ZWQpIC0+IHRhbSB1eXVtbHUiKQogICAgYSgiICAg
LSBZZW5pIHBha2V0IGtvbnRyb2x1OiBrb2R1bnUgV0FUQ0hfQ09ERVMnYSBla2xlIC0+IG95dW5k
YSBnZWMgLT4gLS1kb2dydWxhIikKCiAgICBhKCIiKQogICAgYSgiMykgQ0FOTEkgRE9HUlVMQU5N
SVMgS09ETEFSIChhbmxhbWxhcmkgYmlsaW5lbikiKQogICAgYSgiICAgWzI5XSAgTmV3Q2hhcmFj
dGVyICAgIDogMD1lbnRpdHkgSUQsIDE9T1lVTkNVIEFESSwgOD1ndWlsZCwgNTE9YWxsaWFuY2Us
IikKICAgIGEoIiAgICAgICAgICAgICAgICAgICAgICAgICAgIDQwLzM4PWVraXBtYW4gSUQgZGl6
aXNpLCAxOS8yNT1rb251bSwgMjIvMjM9SFAiKQogICAgYSgiICAgWzQwXSAgTmV3SGFydmVzdGFi
bGUgIDogMD1JRCwgNT10aXAsIDc9dGllciwgOD1rb251bSwgOT1kYXlhbmlrbGlsaWssIDExPWVu
Y2hhbnQiKQogICAgYSgiICAgWzQ3XSAgTW9iQ2hhbmdlU3RhdGUgIDogMD1JRCwgMT1lbmNoYW50
IikKICAgIGEoIiAgIFs2MV0gIEhhcnZlc3RGaW5pc2hlZCA6IDA9a2F5bmFrIElEIikKICAgIGEo
IiAgIFsxMjNdIE5ld01vYiAgICAgICAgICA6IDA9SUQsIDE9bW9iIHRpcCBJRCwgNz1rb251bSwg
MTMvMTQ9bWV2Y3V0L21ha3MgSFAiKQogICAgYSgiICAgWzIxMV0gTW91bnRlZCAgICAgICAgIDog
MD1veXVuY3UgSUQsIDI9YmluZWsgaXRlbSBJRCwgNC81PUhQLCAxMD1yb3Rhc3lvbiIpCiAgICBh
KCIgICBbMzEyXSBOZXdNb3VudE9iamVjdCAgOiAwPUlELCAxPWJpbmVrIGl0ZW0gSUQsIDM9a29u
dW0sIDQ9cm90YXN5b24sIDE0PUhQJSIpCiAgICBhKCIgICBbMzI2XSBXYWl0aW5nUXVldWUgICAg
OiAwPXByZW1pdW0sIDI9a3V5cnVrIHNpcmFzaSwgMz1rYWxhbiBzdXJlIikKICAgIGEoIiAgIFsz
OTNdIE5ld0xvb3RDaGVzdCAgICA6IDA9SUQsIDE9a29udW0sIDMvND1pc2ltLCA1PXJhcml0eSwg
MzE9RmxhZ2dpbmdTdGF0dXMiKQoKICAgIGEoIiIpCiAgICBhKCI0KSBPWVVOIEdVTkNFTExFTUVT
SSBHRUxESUdJTkRFIChzaXJhIG9uZW1saSkiKQogICAgYSgiICAgMCkgWUVERUtMRSA6IGNvcHkg
cGFrZXRfc2VtYXNpLmpzb24gcGFrZXRfc2VtYXNpX2Vza2kuanNvbiIpCiAgICBhKCIgICAxKSBZ
ZW5pIGR1bXAuY3MnaSBFeHRyYSd5YSBrb3kgICgudHh0IHV6YW50aXNpIHRlcmNpaCAtIC5jcyBi
dWlsZCdlIGdpcm1lc2luISkiKQogICAgYSgiICAgMikgcHl0aG9uIGFuYWxpei5weSAtLWZhcmsg
ICAgIChnZXJjZWsgZGVnaXNpa2xpa2xlcjsgaXNpbWxlciB5b2sgc2F5aWxpcikiKQogICAgYSgi
ICAgMykgcHl0aG9uIGFuYWxpei5weSAtLWVudW0gICAgIChlbnVtIGtheW1hc2kgLyB5ZW5pIGtv
ZCB2YXIgbWkpIikKICAgIGEoIiAgIDQpIHB5dGhvbiBhbmFsaXoucHkgLS11cmV0ICAgICAodGFi
bG95dSB5ZW5pbGUpIikKICAgIGEoIiAgIDUpIHB5dGhvbiBhbmFsaXoucHkgLS1rb2QgICAgICAo
a29kIDwtPiB0YWJsbyB1eXVtdSkiKQogICAgYSgiICAgNikgcHl0aG9uIGFuYWxpei5weSAtLW1k
IC0tc296bHVrICAgKGRva3VtYW5sYXJpIHllbmlsZSkiKQoKICAgIGEoIiIpCiAgICBhKCI1KSBE
T1NZQUxBUiAoRXh0cmEga2xhc29ydSkiKQogICAgYSgiICAgYW5hbGl6LnB5ICAgICAgICAgICAg
OiBURUsgYXJhYyAodXJldC9tZC9mYXJrL3Nvemx1ay9kb2dydWxhL2VudW0vbW9iZGIva29kL2Fy
YSkiKQogICAgYSgiICAgZHVtcC5jcyAgICAgICAgICAgICAgOiBveXVudW4gZG9rdW11ICh0ZWsg
Z2VyY2VrIGtheW5haykgLSBQUk9KRSBBR0FDSSBESVNJTkRBIHR1dCEiKQogICAgYSgiICAgcGFr
ZXRfc2VtYXNpLmpzb24gICAgOiB1cmV0aWxlbiB0YWJsbyAoa29kIGRlbmV0aW1pIGJ1bnUgb2t1
cikiKQogICAgYSgiICAgUEFSQU1FVFJFTEVSLm1kLy50c3YgOiBwYXJhbWV0cmUgc296bHVndSAo
RXhjZWwnZGUgZmlsdHJlbGVuZWJpbGlyKSIpCiAgICBhKCIgICBBTEFOLUhBUklUQVNJLVRBTS5t
ZCA6IHR1bSBwYWtldGxlcmluIGFsYW4gc2lyYXNpIikKICAgIGEoIiAgIGxvZ1xcZXZlbnRfY29k
ZXNfc2Vlbi5sb2cgOiBjYW5saWRhIGhhbmdpIGtvZGxhciBnZWxkaSIpCiAgICBhKCIgICBsb2dc
XGV2ZW50X3dhdGNoLmxvZyAgICAgIDogV0FUQ0hfQ09ERVMgaWNlcmlnaSAoY2FubGkgYWxhbiBk
ZWdlcmxlcmkpIikKCiAgICBhKCIiKQogICAgYSgiNikgTk9UIikKICAgIGEoIiAgIC0gT3l1bmN1
IGtvbnVtbGFyaSBveXVuIHRhcmFmaW5kYW4gZ2l6bGVuaXlvciAoc2lmcmVsaSkgLT4gbyBraXNp
bWxhIGlsZ2lsZW5pbG1peW9yLiIpCiAgICBhKCIgICAtIE1vYi9rYXluYWsvY2hlc3QvYmluZWsg
a29udW1sYXJpIERVWiBNRVRJTiAtPiBoZXBzaSBva3VuYWJpbGlyLiIpCiAgICBhKCIgICAtIE9i
ZnVzY2F0ZWQgaXNpbWxlciAoYTA1LCBjdmcuLi4pIGhlciBndW5jZWxsZW1lZGUga2F5YXI7IEtP
RERBIElTSU0gS1VMTEFOTUEsIikKICAgIGEoIiAgICAgc2FkZWNlIElOREVLUyArIFRJUCBrdWxs
YW4uIikKICAgIGEoIj0iICogNzQpCgogICAgcHJpbnQoIlxuIi5qb2luKEwpKQoKCiMgPT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PQojIEtPTVVUOiAtLWNzaGFycCAgKHBha2V0IG1vZGVsbGVyaW5pIEMjIG9sYXJhayB1
cmV0KQojID09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT0KQ1NIQVJQX0RJUiA9IG9zLnBhdGguam9pbihLT0RfS09LLCAi
UGFja2V0cyIpCgojIHNlbWEgdGlwaSAtPiBDIyB0aXBpICAoYmlsaW5tZXllbiBlbnVtL3N0cnVj
dCB0aXBsZXJpIC0+IGludCkKQ1NfVFlQRSA9IHsKICAgICJsb25nIjogImxvbmciLCAiaW50Ijog
ImludCIsICJzaG9ydCI6ICJzaG9ydCIsICJieXRlIjogImJ5dGUiLAogICAgImJvb2wiOiAiYm9v
bCIsICJmbG9hdCI6ICJmbG9hdCIsICJkb3VibGUiOiAiZG91YmxlIiwgInN0cmluZyI6ICJzdHJp
bmciLAogICAgIlZlY3RvcjIiOiAiZmxvYXRbXSIsICJWZWN0b3IzIjogImZsb2F0W10iLCAiR3Vp
ZCI6ICJieXRlW10iLAogICAgIyBPTkVNTEk6IEdhbWVUaW1lU3RhbXAgNjQtYml0IHRpY2sndGly
OyBpbnQgeWFwaWxpcnNhIGRlZ2VyIFRBU0FSIHZlIDAgb2t1bnVyLgogICAgIkdhbWVUaW1lU3Rh
bXAiOiAibG9uZyIsCiAgICAibG9uZ1tdIjogImxvbmdbXSIsICJpbnRbXSI6ICJpbnRbXSIsICJm
bG9hdFtdIjogImZsb2F0W10iLAogICAgImJ5dGVbXSI6ICJieXRlW10iLCAic3RyaW5nW10iOiAi
c3RyaW5nW10iLCAiYm9vbFtdIjogImJvb2xbXSIsCiAgICAjIERJWkkgdGlwbGVyaTogYnVubGFy
IGVrc2lra2VuICJpbnQiZSBkdXN1eW9yZHUgLT4gbW9kZWwgZGl6aSBhbGFuaW5pIEhJQwogICAg
IyBva3V5YW1peW9yZHUgKGNhbmxpIGtheWl0OiBrb2QgMzkndW4gWzNdIGFsYW5pIHRlbGRlIGZs
b2F0W10ga29udW0gaWtlbgogICAgIyBtb2RlbGRlIGludCBpZGkpLiBFa2xlbmRpOiBWZWN0b3Iy
W10gKGtvbnVtIGxpc3Rlc2kpLCBzaG9ydFtdLCBHYW1lVGltZVN0YW1wW10uCiAgICAiVmVjdG9y
MltdIjogImZsb2F0W10iLCAic2hvcnRbXSI6ICJzaG9ydFtdIiwgIkdhbWVUaW1lU3RhbXBbXSI6
ICJsb25nW10iLAp9CgojIERpemkgYWxhbmxhcmkgaWNpbiBFU05FSyBva3V5dWN1bGFyIC0+IEV2
ZW50SGFuZGxlclV0aWxzLgojIE5FREVOOiBQaG90b24gYmlyIGRpemkgYWxhbmluaSBkdW1wJ3Rh
a2kgdGlwdGVuIEZBUktMSSBnb25kZXJlYmlsaXlvcgojIChjYW5saSBrYXlpdDoga29kIDM5J3Vu
IFswXSBhbGFuaSB0ZWxkZSBzaG9ydFtdL2J5dGVbXSwgbW9kZWxkZSBsb25nW10pLgojIFRla2ls
IChkaXppIG9sbWF5YW4pIGFsYW5sYXJpbiBva3VtYSBkYXZyYW5pc2kgREVHSVNNRVouCkNTX0FS
UkFZX1JFQURFUiA9IHsKICAgICJsb25nW10iOiAiRXh0cmFjdExvbmdBcnJheSIsICJpbnRbXSI6
ICJFeHRyYWN0SW50QXJyYXkiLCAic2hvcnRbXSI6ICJFeHRyYWN0U2hvcnRBcnJheSIsCiAgICAi
Ynl0ZVtdIjogIkV4dHJhY3RCeXRlQXJyYXkiLCAiZmxvYXRbXSI6ICJFeHRyYWN0RmxvYXRBcnJh
eSIsICJzdHJpbmdbXSI6ICJFeHRyYWN0U3RyaW5nQXJyYXkiLAp9CgojIE9uZW1saSBwYWtldGxl
ciBpY2luIEVMTEUgdmVyaWxlbiBhbGFuIGlzaW1sZXJpIChva3VuYWJpbGlybGlrKQpDU19GSUVM
RF9OQU1FUyA9IHsKICAgICIyOSI6ICB7IjAiOiAiRW50aXR5SWQiLCAiMSI6ICJQbGF5ZXJOYW1l
IiwgIjgiOiAiR3VpbGROYW1lIiwgIjE5IjogIlNwYXduWCIsCiAgICAgICAgICAgICIyMiI6ICJD
dXJyZW50SGVhbHRoIiwgIjIzIjogIk1heEhlYWx0aCIsICIyNSI6ICJTcGF3blkiLAogICAgICAg
ICAgICAiMzgiOiAiRXF1aXBtZW50SWRzQWx0IiwgIjQwIjogIkVxdWlwbWVudElkcyIsICI1MSI6
ICJBbGxpYW5jZU5hbWUiLCAiNTMiOiAiRmFjdGlvbiJ9LAogICAgIjQwIjogIHsiMCI6ICJSZXNv
dXJjZUlkIiwgIjUiOiAiUmVzb3VyY2VUeXBlIiwgIjciOiAiVGllciIsICI4IjogIlBvc2l0aW9u
IiwKICAgICAgICAgICAgIjkiOiAiRHVyYWJpbGl0eSIsICIxMCI6ICJTaXplIiwgIjExIjogIkVu
Y2hhbnQifSwKICAgICI0NyI6ICB7IjAiOiAiRW50aXR5SWQiLCAiMSI6ICJFbmNoYW50In0sCiAg
ICAiNjEiOiAgeyIwIjogIlJlc291cmNlSWQifSwKICAgICIxMjMiOiB7IjAiOiAiRW50aXR5SWQi
LCAiMSI6ICJNb2JUeXBlSWQiLCAiMiI6ICJGbGFnZ2luZ1N0YXR1cyIsICI3IjogIlBvc2l0aW9u
IiwKICAgICAgICAgICAgIjgiOiAiU2Vjb25kUG9zaXRpb24iLCAiMTMiOiAiQ3VycmVudEhlYWx0
aCIsICIxNCI6ICJNYXhIZWFsdGgiLCAiMzQiOiAiUmFyaXR5In0sCiAgICAiMjExIjogeyIwIjog
IlBsYXllcklkIiwgIjEiOiAiVGltZXN0YW1wIiwgIjIiOiAiTW91bnRJdGVtSWQiLCAiNCI6ICJI
ZWFsdGhYMTAwIiwKICAgICAgICAgICAgIjUiOiAiSGVhbHRoIiwgIjYiOiAiVGltZXN0YW1wMiIs
ICIxMCI6ICJSb3RhdGlvbiJ9LAogICAgIjMxMiI6IHsiMCI6ICJJZCIsICIxIjogIk1vdW50SXRl
bUlkIiwgIjIiOiAiVW5rbm93bjIiLCAiMyI6ICJQb3NpdGlvbiIsICI0IjogIlJvdGF0aW9uIiwK
ICAgICAgICAgICAgIjUiOiAiR3VpZDEiLCAiNiI6ICJGbGFnZ2luZ1N0YXR1cyIsICI3IjogIlR5
cGUiLCAiOCI6ICJVbmtub3duOCIsCiAgICAgICAgICAgICI5IjogIkd1aWQyIiwgIjEwIjogIkd1
aWQzIiwgIjE0IjogIkhlYWx0aFBlcmNlbnQifSwKICAgICIzMjYiOiB7IjAiOiAiSXNQcmVtaXVt
IiwgIjEiOiAiTnVtYmVyIiwgIjIiOiAiUXVldWVQb3NpdGlvbiIsICIzIjogIlJlbWFpbmluZ1Rp
bWUifSwKICAgICIzOTMiOiB7IjAiOiAiQ2hlc3RJZCIsICIxIjogIlBvc2l0aW9uIiwgIjIiOiAi
U2l6ZSIsICIzIjogIlNob3J0TmFtZSIsICI0IjogIlVuaXF1ZU5hbWUiLAogICAgICAgICAgICAi
NSI6ICJSYXJpdHkiLCAiNiI6ICJUaW1lc3RhbXAiLCAiNyI6ICJVbmxvY2tUaWNrcyIsICI4Ijog
Ikd1aWRCeXRlczEiLAogICAgICAgICAgICAiMTAiOiAiR3VpZEJ5dGVzMiIsICIxNiI6ICJWYWx1
ZTE2IiwgIjE3IjogIkZsYWcxNyIsICIxOCI6ICJTZWNvbmROYW1lIiwKICAgICAgICAgICAgIjIw
IjogIkd1aWRTdHJpbmciLCAiMjEiOiAiVmFsdWUyMSIsICIyMyI6ICJSYXJpdHlDb3B5IiwgIjI2
IjogIkZsYWcyNiIsCiAgICAgICAgICAgICIyOCI6ICJGbGFnMjgiLCAiMjkiOiAiVmFsdWUyOSIs
ICIzMSI6ICJGbGFnZ2luZ1N0YXR1cyJ9LAp9CgoKZGVmIGNzX3R5cGUodCk6CiAgICByZXR1cm4g
Q1NfVFlQRS5nZXQodCwgImludCIpCgoKZGVmIGNzX2NsYXNzX25hbWUocmF3LCB2YXJpYW50X2lu
ZGV4LCB2YXJpYW50X2NvdW50KToKICAgIG4gPSByZS5zdWIociJbXjAtOUEtWmEtel9dIiwgIl8i
LCBzdHIocmF3KSkKICAgIGlmIG5vdCBuIG9yIG5bMF0uaXNkaWdpdCgpOgogICAgICAgIG4gPSAi
UCIgKyBuCiAgICBpZiB2YXJpYW50X2NvdW50ID4gMToKICAgICAgICBuICs9ICJfViVkIiAlIHZh
cmlhbnRfaW5kZXgKICAgIHJldHVybiBuCgoKZGVmIGNzX2ZpZWxkX25hbWUoY29kZSwgaW5kZXgp
OgogICAgbm0gPSBDU19GSUVMRF9OQU1FUy5nZXQoc3RyKGNvZGUpLCB7fSkuZ2V0KHN0cihpbmRl
eCkpCiAgICByZXR1cm4gbm0gaWYgbm0gZWxzZSAiRiVkIiAlIGluZGV4CgoKZGVmIGNtZF9jc2hh
cnAoKToKICAgICIiInBha2V0X3NlbWFzaS5qc29uIC0+IG9rdW5hYmlsaXIgQyMgcGFrZXQgbW9k
ZWxsZXJpICgyIGRvc3lhKS4iIiIKICAgICpfLCBzY2hlbWEsIF91ID0gbG9hZCgpCiAgICBvcy5t
YWtlZGlycyhDU0hBUlBfRElSLCBleGlzdF9vaz1UcnVlKQogICAgd3JpdHRlbiA9IFtdCgogICAg
Zm9yIHNlY3Rpb24sIGZuYW1lLCBucywgdGl0bGUgaW4gKAogICAgICAgICAgICAoImV2ZW50cyIs
ICJFdmVudHMuZy5jcyIsICJBbGJpb25EYXRhSGFuZGxlcnMuUGFja2V0cy5FdmVudHMiLCAiRVZF
TlQiKSwKICAgICAgICAgICAgKCJvcGVyYXRpb25zIiwgIk9wZXJhdGlvbnMuZy5jcyIsICJBbGJp
b25EYXRhSGFuZGxlcnMuUGFja2V0cy5PcGVyYXRpb25zIiwgIk9QRVJBU1lPTiIpKToKCiAgICAg
ICAgTCA9IFtdCiAgICAgICAgTC5hcHBlbmQoIi8vIDxhdXRvLWdlbmVyYXRlZD4iKQogICAgICAg
IEwuYXBwZW5kKCIvLyAgIE9UT01BVElLIHVyZXRpbGlyOiAgcHl0aG9uIGFuYWxpei5weSAtLWNz
aGFycCIpCiAgICAgICAgTC5hcHBlbmQoIi8vICAgS2F5bmFrOiBFeHRyYS9wYWtldF9zZW1hc2ku
anNvbiAgKGR1bXAuY3MndGVuIHVyZXRpbGlyKSIpCiAgICAgICAgTC5hcHBlbmQoIi8vICAgRUxM
RSBEVVpFTkxFTUVZSU4gLSBiaXIgc29ucmFraSB1cmV0aW1kZSBrYXlib2x1ci4iKQogICAgICAg
IEwuYXBwZW5kKCIvLyAgIEFubGFtbGkgYWxhbmxhciBlbGxlIGFkbGFuZGlyaWxkaSAoQ2hlc3RJ
ZCwgUG9zaXRpb24sIFJhcml0eS4uLiksIikKICAgICAgICBMLmFwcGVuZCgiLy8gICBnZXJpc2kg
RjxpbmRla3M+LiBBbmxhbWxhciBYTUwgeW9ydW11bmRhLiIpCiAgICAgICAgTC5hcHBlbmQoIi8v
ICAgTk9UOiBQaG90b24gbnVsbCBhbGFubGFyaSBHT05ERVJNRVogLT4gYWxhbmxhciAwL251bGwg
a2FsYWJpbGlyLCIpCiAgICAgICAgTC5hcHBlbmQoIi8vICAgICAgICBidSB5dXpkZW4gJ2FsYW4g
dmFyIG1pJyBrb250cm9sdSBpY2luIHAuQ29udGFpbnNLZXkoKGJ5dGUpTikga3VsbGFuLiIpCiAg
ICAgICAgTC5hcHBlbmQoIi8vIDwvYXV0by1nZW5lcmF0ZWQ+IikKICAgICAgICBMLmFwcGVuZCgi
IikKICAgICAgICBMLmFwcGVuZCgidXNpbmcgU3lzdGVtLkNvbGxlY3Rpb25zLkdlbmVyaWM7IikK
ICAgICAgICBMLmFwcGVuZCgidXNpbmcgQWxiaW9uRGF0YUhhbmRsZXJzLlV0aWxzOyIpCiAgICAg
ICAgTC5hcHBlbmQoIiIpCiAgICAgICAgTC5hcHBlbmQoIm5hbWVzcGFjZSAlcyIgJSBucykKICAg
ICAgICBMLmFwcGVuZCgieyIpCiAgICAgICAgY2xzX2NvdW50ID0gMAoKICAgICAgICBmb3IgY29k
ZSBpbiBzb3J0ZWQoc2NoZW1hW3NlY3Rpb25dLCBrZXk9aW50KToKICAgICAgICAgICAgdmFyaWFu
dHMgPSBzb3J0ZWQoc2NoZW1hW3NlY3Rpb25dW2NvZGVdLCBrZXk9bGFtYmRhIGU6IGVbImNsYXNz
Il0pCiAgICAgICAgICAgIGZvciB2aSwgZSBpbiBlbnVtZXJhdGUodmFyaWFudHMsIDEpOgogICAg
ICAgICAgICAgICAgY2xzX2NvdW50ICs9IDEKICAgICAgICAgICAgICAgIGNuYW1lID0gY3NfY2xh
c3NfbmFtZShlWyJuYW1lIl0sIHZpLCBsZW4odmFyaWFudHMpKQogICAgICAgICAgICAgICAgTC5h
cHBlbmQoIiAgICAvLy8gPHN1bW1hcnk+WyVzXSAlcyAtLSAlZCBhbGFuJXM8L3N1bW1hcnk+IiAl
ICgKICAgICAgICAgICAgICAgICAgICBjb2RlLCBlWyJuYW1lIl0sIGVbImZpZWxkX2NvdW50Il0s
CiAgICAgICAgICAgICAgICAgICAgIiIgaWYgbGVuKHZhcmlhbnRzKSA9PSAxIGVsc2UgIiAodmFy
eWFudCAlZC8lZCkiICUgKHZpLCBsZW4odmFyaWFudHMpKSkpCiAgICAgICAgICAgICAgICBMLmFw
cGVuZCgiICAgIHB1YmxpYyBzZWFsZWQgY2xhc3MgJXMiICUgY25hbWUpCiAgICAgICAgICAgICAg
ICBMLmFwcGVuZCgiICAgIHsiKQogICAgICAgICAgICAgICAgTC5hcHBlbmQoIiAgICAgICAgcHVi
bGljIGNvbnN0IGludCBDb2RlID0gJXM7IiAlIGNvZGUpCiAgICAgICAgICAgICAgICBMLmFwcGVu
ZCgiICAgICAgICBwdWJsaWMgY29uc3Qgc3RyaW5nIFBhY2tldE5hbWUgPSBcIiVzXCI7IiAlIGVb
Im5hbWUiXSkKICAgICAgICAgICAgICAgIEwuYXBwZW5kKCIiKQogICAgICAgICAgICAgICAgZm9y
IGYgaW4gZVsiZmllbGRzIl06CiAgICAgICAgICAgICAgICAgICAgbWVhbmluZywgdmVyaWZpZWQg
PSBmaWVsZF9tZWFuaW5nKGNvZGUsIGYpCiAgICAgICAgICAgICAgICAgICAgZmxhZyA9ICIgIFtD
QU5MSSBET0dSVUxBTkRJXSIgaWYgdmVyaWZpZWQgZWxzZSAiIgogICAgICAgICAgICAgICAgICAg
IEwuYXBwZW5kKCIgICAgICAgIC8vLyA8c3VtbWFyeT5bJXNdICVzJXM8L3N1bW1hcnk+IiAlIChm
WyJpbmRleCJdLCBtZWFuaW5nLCBmbGFnKSkKICAgICAgICAgICAgICAgICAgICBMLmFwcGVuZCgi
ICAgICAgICBwdWJsaWMgJXMgJXM7IiAlIChjc190eXBlKGZbInR5cGUiXSksIGNzX2ZpZWxkX25h
bWUoY29kZSwgZlsiaW5kZXgiXSkpKQogICAgICAgICAgICAgICAgTC5hcHBlbmQoIiIpCiAgICAg
ICAgICAgICAgICBMLmFwcGVuZCgiICAgICAgICBwdWJsaWMgc3RhdGljICVzIEZyb20oRGljdGlv
bmFyeTxieXRlLCBvYmplY3Q+IHApIiAlIGNuYW1lKQogICAgICAgICAgICAgICAgTC5hcHBlbmQo
IiAgICAgICAgeyIpCiAgICAgICAgICAgICAgICBMLmFwcGVuZCgiICAgICAgICAgICAgaWYgKHAg
PT0gbnVsbCkgcmV0dXJuIG51bGw7IikKICAgICAgICAgICAgICAgIEwuYXBwZW5kKCIgICAgICAg
ICAgICB2YXIgbSA9IG5ldyAlcygpOyIgJSBjbmFtZSkKICAgICAgICAgICAgICAgIGZvciBmIGlu
IGVbImZpZWxkcyJdOgogICAgICAgICAgICAgICAgICAgIGN0ID0gY3NfdHlwZShmWyJ0eXBlIl0p
CiAgICAgICAgICAgICAgICAgICAgZm4gPSBjc19maWVsZF9uYW1lKGNvZGUsIGZbImluZGV4Il0p
CiAgICAgICAgICAgICAgICAgICAgaWYgY3QgPT0gImxvbmciOgogICAgICAgICAgICAgICAgICAg
ICAgICAjIGxvbmcgYWxhbmxhcjogYnl0ZVs4XSBkdXJ1bXVudSBkYSBrYXBzYXlhbiBndXZlbmxp
IG9rdXl1Y3UKICAgICAgICAgICAgICAgICAgICAgICAgTC5hcHBlbmQoIiAgICAgICAgICAgIG0u
JXMgPSBFdmVudEhhbmRsZXJVdGlscy5FeHRyYWN0TG9uZyhwLCAoYnl0ZSklcyk7IiAlICgKICAg
ICAgICAgICAgICAgICAgICAgICAgICAgIGZuLCBmWyJpbmRleCJdKSkKICAgICAgICAgICAgICAg
ICAgICBlbGlmIGN0IGluIENTX0FSUkFZX1JFQURFUjoKICAgICAgICAgICAgICAgICAgICAgICAg
IyBkaXppIGFsYW5sYXJpOiB0ZWwgZm9ybWF0aW5kYSB0aXAgZGVnaXNlYmlsaXIgLT4gZXNuZWsg
b2t1eXVjdQogICAgICAgICAgICAgICAgICAgICAgICBMLmFwcGVuZCgiICAgICAgICAgICAgbS4l
cyA9IEV2ZW50SGFuZGxlclV0aWxzLiVzKHAsIChieXRlKSVzKTsiICUgKAogICAgICAgICAgICAg
ICAgICAgICAgICAgICAgZm4sIENTX0FSUkFZX1JFQURFUltjdF0sIGZbImluZGV4Il0pKQogICAg
ICAgICAgICAgICAgICAgIGVsc2U6CiAgICAgICAgICAgICAgICAgICAgICAgIEwuYXBwZW5kKCIg
ICAgICAgICAgICBtLiVzID0gRXZlbnRIYW5kbGVyVXRpbHMuRXh0cmFjdFZhbHVlPCVzPihwLCAo
Ynl0ZSklcyk7IiAlICgKICAgICAgICAgICAgICAgICAgICAgICAgICAgIGZuLCBjdCwgZlsiaW5k
ZXgiXSkpCiAgICAgICAgICAgICAgICBMLmFwcGVuZCgiICAgICAgICAgICAgcmV0dXJuIG07IikK
ICAgICAgICAgICAgICAgIEwuYXBwZW5kKCIgICAgICAgIH0iKQogICAgICAgICAgICAgICAgTC5h
cHBlbmQoIiAgICB9IikKICAgICAgICAgICAgICAgIEwuYXBwZW5kKCIiKQoKICAgICAgICBMLmFw
cGVuZCgifSIpCgogICAgICAgIHBhdGggPSBvcy5wYXRoLmpvaW4oQ1NIQVJQX0RJUiwgZm5hbWUp
CiAgICAgICAgd2l0aCBvcGVuKHBhdGgsICJ3IiwgZW5jb2Rpbmc9InV0Zi04IikgYXMgZmg6CiAg
ICAgICAgICAgIGZoLndyaXRlKCJcbiIuam9pbihMKSArICJcbiIpCiAgICAgICAgd3JpdHRlbi5h
cHBlbmQoKGZuYW1lLCBjbHNfY291bnQsIHBhdGgpKQoKICAgICMgLS0tIFBhY2tldE1ldGEuZy5j
cyA6IGtvZCAtPiBpbmRla3MgLT4gYW5sYW0gIChQYWNrZXQgSW5zcGVjdG9yIGljaW4pIC0tLQog
ICAgTSA9IFtdCiAgICBNLmFwcGVuZCgiLy8gPGF1dG8tZ2VuZXJhdGVkPiIpCiAgICBNLmFwcGVu
ZCgiLy8gICBPVE9NQVRJSyB1cmV0aWxpcjogIHB5dGhvbiBhbmFsaXoucHkgLS1jc2hhcnAiKQog
ICAgTS5hcHBlbmQoIi8vICAgUGFrZXQgYWxhbiBhbmxhbWxhcmkgKFBhY2tldCBJbnNwZWN0b3Ig
cGFuZWxpIGt1bGxhbmlyKS4iKQogICAgTS5hcHBlbmQoIi8vIDwvYXV0by1nZW5lcmF0ZWQ+IikK
ICAgIE0uYXBwZW5kKCIiKQogICAgTS5hcHBlbmQoInVzaW5nIFN5c3RlbS5Db2xsZWN0aW9ucy5H
ZW5lcmljOyIpCiAgICBNLmFwcGVuZCgiIikKICAgIE0uYXBwZW5kKCJuYW1lc3BhY2UgQWxiaW9u
RGF0YUhhbmRsZXJzLlBhY2tldHMiKQogICAgTS5hcHBlbmQoInsiKQogICAgTS5hcHBlbmQoIiAg
ICAvLy8gPHN1bW1hcnk+UGFrZXQgYWxhbiBhbmxhbWxhcmk6IGtvZCAtPiAoaW5kZWtzIC0+IGFu
bGFtKTwvc3VtbWFyeT4iKQogICAgTS5hcHBlbmQoIiAgICBwdWJsaWMgc3RhdGljIGNsYXNzIFBh
Y2tldE1ldGEiKQogICAgTS5hcHBlbmQoIiAgICB7IikKICAgIE0uYXBwZW5kKCIgICAgICAgIHB1
YmxpYyBzdGF0aWMgcmVhZG9ubHkgRGljdGlvbmFyeTxpbnQsIERpY3Rpb25hcnk8aW50LCBzdHJp
bmc+PiBNZWFuaW5ncyA9IikKICAgIE0uYXBwZW5kKCIgICAgICAgICAgICBuZXcgRGljdGlvbmFy
eTxpbnQsIERpY3Rpb25hcnk8aW50LCBzdHJpbmc+PiIpCiAgICBNLmFwcGVuZCgiICAgICAgICB7
IikKICAgIG1ldGFfY291bnQgPSAwCiAgICBmb3Igc2VjdGlvbiBpbiAoImV2ZW50cyIsICJvcGVy
YXRpb25zIik6CiAgICAgICAgZm9yIGNvZGUgaW4gc29ydGVkKHNjaGVtYVtzZWN0aW9uXSwga2V5
PWludCk6CiAgICAgICAgICAgIHZhcmlhbnRzID0gc29ydGVkKHNjaGVtYVtzZWN0aW9uXVtjb2Rl
XSwga2V5PWxhbWJkYSBlOiBlWyJjbGFzcyJdKQogICAgICAgICAgICBlID0gdmFyaWFudHNbMF0K
ICAgICAgICAgICAgcGFydHMgPSBbXQogICAgICAgICAgICBmb3IgZiBpbiBlWyJmaWVsZHMiXToK
ICAgICAgICAgICAgICAgIG1lYW5pbmcsIHZlcmlmaWVkID0gZmllbGRfbWVhbmluZyhjb2RlLCBm
KQogICAgICAgICAgICAgICAgaWYgbm90IG1lYW5pbmc6CiAgICAgICAgICAgICAgICAgICAgY29u
dGludWUKICAgICAgICAgICAgICAgIGVzYyA9IHN0cihtZWFuaW5nKS5yZXBsYWNlKCJcXCIsICJc
XFxcIikucmVwbGFjZSgiXCIiLCAiXFxcIiIpCiAgICAgICAgICAgICAgICBpZiB2ZXJpZmllZDoK
ICAgICAgICAgICAgICAgICAgICBlc2MgKz0gIiAgW0NBTkxJXSIKICAgICAgICAgICAgICAgIHBh
cnRzLmFwcGVuZCgiWyVzXSA9IFwiJXNcIiIgJSAoZlsiaW5kZXgiXSwgZXNjKSkKICAgICAgICAg
ICAgaWYgbm90IHBhcnRzOgogICAgICAgICAgICAgICAgY29udGludWUKICAgICAgICAgICAgTS5h
cHBlbmQoIiAgICAgICAgICAgIFslc10gPSBuZXcgRGljdGlvbmFyeTxpbnQsIHN0cmluZz4geyAl
cyB9LCIgJSAoY29kZSwgIiwgIi5qb2luKHBhcnRzKSkpCiAgICAgICAgICAgIG1ldGFfY291bnQg
Kz0gMQogICAgTS5hcHBlbmQoIiAgICAgICAgfTsiKQogICAgTS5hcHBlbmQoIiAgICB9IikKICAg
IE0uYXBwZW5kKCJ9IikKICAgIG1ldGFfcGF0aCA9IG9zLnBhdGguam9pbihDU0hBUlBfRElSLCAi
UGFja2V0TWV0YS5nLmNzIikKICAgIHdpdGggb3BlbihtZXRhX3BhdGgsICJ3IiwgZW5jb2Rpbmc9
InV0Zi04IikgYXMgZmg6CiAgICAgICAgZmgud3JpdGUoIlxuIi5qb2luKE0pICsgIlxuIikKICAg
IHdyaXR0ZW4uYXBwZW5kKCgiUGFja2V0TWV0YS5nLmNzIiwgbWV0YV9jb3VudCwgbWV0YV9wYXRo
KSkKCiAgICBmb3IgZm5hbWUsIG4sIHBhdGggaW4gd3JpdHRlbjoKICAgICAgICBwcmludCgiJS0x
NnMgOiAlNWQga2F5aXQgIC0+ICAlcyIgJSAoZm5hbWUsIG4sIHBhdGgpKQogICAgcHJpbnQoIlxu
S3VsbGFuaW0gb3JuZWdpIChoYW5kbGVyIGljaW5kZSk6IikKICAgIHByaW50KCIgICAgdmFyIGNo
ZXN0ID0gQWxiaW9uRGF0YUhhbmRsZXJzLlBhY2tldHMuRXZlbnRzLk5ld0xvb3RDaGVzdC5Gcm9t
KHBhcmFtZXRlcnMpOyIpCiAgICBwcmludCgiICAgIGlmIChjaGVzdCAhPSBudWxsKSB7IHZhciBp
ZCA9IGNoZXN0LkNoZXN0SWQ7IHZhciBwb3MgPSBjaGVzdC5Qb3NpdGlvbjsgfSIpCgoKIyA9PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09CiMgS09NVVQ6IC0tdWkgIChJbUd1aSBhcmF5dXp1bnUgSFRNTCdlIGNldmlyIC0+
IEV4dHJhXGd1aS5odG1sKQojID09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0KVUlfUk9PVCA9IG9zLnBhdGguam9pbihS
T09ULCAiTmlnaHR3YXRjaCIsICJVc2VyQ29udHJvbHMiKQpVSV9PVVQgPSBvcy5wYXRoLmpvaW4o
U0NSSVBUX0RJUiwgImd1aS5odG1sIikKCiMgTWVudGFsaXR5VGhlbWUuY3MgLT4gQ29sb3JzIChE
YXJrKSAgW2tvZCBpbGUgYmlyZWJpcl0KVUlfVEhFTUUgPSB7CiAgICAiYmciOiAiIzBhMGEwYSIs
ICJzaWRlYmFyIjogIiMwZTBlMGUiLCAiY2FyZCI6ICIjMTIxMjEyIiwgImNhcmRoIjogIiMxODE4
MTgiLAogICAgImlucHV0IjogIiMxNjE2MTYiLCAiaW5wdXRoIjogIiMxYzFjMWMiLCAiYWNjZW50
IjogIiM2YzVjZTciLCAiYWNjZW50bHQiOiAiI2EyOWJmZSIsCiAgICAidGVhbCI6ICIjMDBjZWM5
IiwgImRhbmdlciI6ICIjZmY2YjZiIiwgInN1Y2Nlc3MiOiAiIzUxY2Y2NiIsICJ3YXJuaW5nIjog
IiNmZmQ0M2IiLAogICAgInRleHQiOiAiI2YxZjNmNSIsICJ0ZXh0MiI6ICIjODY4ZTk2IiwgIm11
dGVkIjogIiM0OTUwNTciLCAiYm9yZGVyIjogIiMxZTI5M2IiLAp9CgpSRV9MQU5HMiA9IHJlLmNv
bXBpbGUocidMYW5nXC5HZXRcKCIoW14iXSspIlwpXHMqXD9cP1xzKiIoW14iXSopIicpClJFX0xB
TkcxID0gcmUuY29tcGlsZShyJ0xhbmdcLkdldFwoIihbXiJdKykiXCknKQpSRV9TVFIgPSByZS5j
b21waWxlKHInIigoPzpbXiJcXF18XFwuKSopIicpClVJTV9DQUxMID0gcmUuY29tcGlsZShyJ1xi
KD86SW1HdWl8TWVudGFsaXR5VGhlbWUpXC4oXHcrKVxzKlwoJykKVUlNX01FVEhPRCA9IHJlLmNv
bXBpbGUocideWyBcdF0qKD86cHVibGljfHByaXZhdGV8cHJvdGVjdGVkfGludGVybmFsKVxzKyg/
OnN0YXRpY1xzKyk/KD86YXN5bmNccyspPycKICAgICAgICAgICAgICAgICAgICAgICAgcicoPzpv
dmVycmlkZVxzKyk/KD86dmlydHVhbFxzKyk/KD86dm9pZHxib29sfGludHxzdHJpbmd8ZmxvYXQp
XHMrKFx3KylccypcKCcsCiAgICAgICAgICAgICAgICAgICAgICAgIHJlLk0pCgoKZGVmIF9zdHJp
cF9jb21tZW50cyh0ZXh0KToKICAgIHRleHQgPSByZS5zdWIociIvXCouKj9cKi8iLCAiIiwgdGV4
dCwgZmxhZ3M9cmUuUykKICAgIHJldHVybiByZS5zdWIociIvL1teXG5dKiIsICIiLCB0ZXh0KQoK
CmRlZiBfbGFiZWwoYXJnKToKICAgICIiIkltR3VpIGNhZ3Jpc2luZGFuIG9rdW5hYmlsaXIgZXRp
a2V0IGNpa2FyaXIuIiIiCiAgICBtID0gUkVfTEFORzIuc2VhcmNoKGFyZykKICAgIGlmIG06CiAg
ICAgICAgcmV0dXJuIG0uZ3JvdXAoMikKICAgIG0gPSBSRV9MQU5HMS5zZWFyY2goYXJnKQogICAg
aWYgbToKICAgICAgICByZXR1cm4gIkxhbmc6IiArIG0uZ3JvdXAoMSkKICAgIG0gPSBSRV9TVFIu
c2VhcmNoKGFyZykKICAgIHJldHVybiBtLmdyb3VwKDEpIGlmIG0gZWxzZSAiIgoKCmRlZiB1aV9j
b2xsZWN0KHBhdGgpOgogICAgIiIiRG9zeWFkYWtpIEltR3VpIGNhZ3JpbGFyaW5pIE1FVE9UIGJh
emluZGEsIFNJUkFZTEEgY2lrYXJpci4iIiIKICAgIHdpdGggb3BlbihwYXRoLCAiciIsIGVuY29k
aW5nPSJ1dGYtOCIsIGVycm9ycz0icmVwbGFjZSIpIGFzIGY6CiAgICAgICAgdGV4dCA9IF9zdHJp
cF9jb21tZW50cyhmLnJlYWQoKSkKCiAgICAjIG1ldG90IHNpbmlybGFyaQogICAgc3RhcnRzID0g
WyhtLmdyb3VwKDEpLCBtLnN0YXJ0KCkpIGZvciBtIGluIFVJTV9NRVRIT0QuZmluZGl0ZXIodGV4
dCldCiAgICBtZXRob2RzID0gW10KICAgIGZvciBrLCAobmFtZSwgcykgaW4gZW51bWVyYXRlKHN0
YXJ0cyk6CiAgICAgICAgZSA9IHN0YXJ0c1trICsgMV1bMV0gaWYgayArIDEgPCBsZW4oc3RhcnRz
KSBlbHNlIGxlbih0ZXh0KQogICAgICAgIG1ldGhvZHMuYXBwZW5kKChuYW1lLCB0ZXh0W3M6ZV0p
KQoKICAgIG91dCA9IFtdCiAgICBmb3IgbmFtZSwgYm9keSBpbiBtZXRob2RzOgogICAgICAgIGNh
bGxzID0gW10KICAgICAgICBmb3IgbSBpbiBVSU1fQ0FMTC5maW5kaXRlcihib2R5KToKICAgICAg
ICAgICAgY24gPSBtLmdyb3VwKDEpCiAgICAgICAgICAgIGksIGRlcHRoLCBzdGFydCA9IG0uZW5k
KCksIDEsIG0uZW5kKCkKICAgICAgICAgICAgd2hpbGUgaSA8IGxlbihib2R5KSBhbmQgZGVwdGgg
PiAwOgogICAgICAgICAgICAgICAgY2ggPSBib2R5W2ldCiAgICAgICAgICAgICAgICBpZiBjaCA9
PSAiKCI6CiAgICAgICAgICAgICAgICAgICAgZGVwdGggKz0gMQogICAgICAgICAgICAgICAgZWxp
ZiBjaCA9PSAiKSI6CiAgICAgICAgICAgICAgICAgICAgZGVwdGggLT0gMQogICAgICAgICAgICAg
ICAgaSArPSAxCiAgICAgICAgICAgIGFyZyA9IGJvZHlbc3RhcnQ6aSAtIDFdCiAgICAgICAgICAg
IGNhbGxzLmFwcGVuZCgoY24sIGFyZykpCiAgICAgICAgaWYgY2FsbHM6CiAgICAgICAgICAgIG91
dC5hcHBlbmQoKG5hbWUsIGNhbGxzKSkKICAgIHJldHVybiBvdXQKCgojIEltR3VpIGNhZ3Jpc2kg
LT4gSFRNTCB3aWRnZXQgdGlwaQpVSV9XSURHRVQgPSB7CiAgICAiQ2hlY2tib3giOiAiY2hlY2si
LCAiQ2hlY2tib3hTdHlsZWQiOiAiY2hlY2siLAogICAgIkJ1dHRvbiI6ICJidXR0b24iLCAiVG9n
Z2xlU3dpdGNoIjogInRvZ2dsZSIsICJTd2l0Y2giOiAidG9nZ2xlIiwKICAgICJTbGlkZXJGbG9h
dCI6ICJzbGlkZXIiLCAiU2xpZGVySW50IjogInNsaWRlciIsICJTbGlkZXJGbG9hdDIiOiAic2xp
ZGVyIiwKICAgICJJbnB1dFRleHQiOiAiaW5wdXQiLCAiSW5wdXRUZXh0V2l0aEhpbnQiOiAiaW5w
dXQiLCAiSW5wdXRGbG9hdCI6ICJpbnB1dCIsICJJbnB1dEludCI6ICJpbnB1dCIsCiAgICAiQ29t
Ym8iOiAiY29tYm8iLCAiQmVnaW5Db21ibyI6ICJjb21ibyIsCiAgICAiQ29sbGFwc2luZ0hlYWRl
ciI6ICJoZWFkZXIiLCAiVHJlZU5vZGVFeCI6ICJoZWFkZXIiLCAiVHJlZU5vZGUiOiAiaGVhZGVy
IiwKICAgICJTZWxlY3RhYmxlIjogInJvdyIsICJNZW51SXRlbSI6ICJyb3ciLCAiUmFkaW9CdXR0
b24iOiAicm93IiwKfQoKCmRlZiB1aV93aWRnZXRzX3RvX2h0bWwoY2FsbHMsIGRlcHRoPTApOgog
ICAgIiIiQ2FncmkgbGlzdGVzaW5pIEhUTUwnZSBjZXZpcmlyIChzZWttZS9jb2N1ayB5aWdpbmkg
aWxlKS4iIiIKICAgIGggPSBbXQogICAgc3RhY2sgPSBbXSAgICAgICAgICAjICgidGFiInwiY2hp
bGQiLCAuLi4pCiAgICBwYWQgPSAxMCArIGRlcHRoICogMTAKCiAgICBmb3IgbmFtZSwgYXJnIGlu
IGNhbGxzOgogICAgICAgIGxibCA9IF9sYWJlbChhcmcpCgogICAgICAgIGlmIG5hbWUgPT0gIkJl
Z2luVGFiQmFyIjoKICAgICAgICAgICAgaC5hcHBlbmQoJzxkaXYgY2xhc3M9InRhYmJhciI+JykK
ICAgICAgICBlbGlmIG5hbWUgPT0gIkVuZFRhYkJhciI6CiAgICAgICAgICAgIGguYXBwZW5kKCc8
L2Rpdj4nKQogICAgICAgIGVsaWYgbmFtZSA9PSAiQmVnaW5UYWJJdGVtIjoKICAgICAgICAgICAg
c2FmZSA9IGxibC5yZXBsYWNlKCciJywgIiciKQogICAgICAgICAgICBoLmFwcGVuZCgnPGRpdiBj
bGFzcz0idGFiaXRlbSI+PGRpdiBjbGFzcz0idGFiaGVhZCIgZGF0YS10PSIlcyI+JXM8L2Rpdj48
ZGl2IGNsYXNzPSJ0YWJib2R5Ij4nCiAgICAgICAgICAgICAgICAgICAgICUgKHNhZmUsIHNhZmUp
KQogICAgICAgIGVsaWYgbmFtZSA9PSAiRW5kVGFiSXRlbSI6CiAgICAgICAgICAgIGguYXBwZW5k
KCc8L2Rpdj48L2Rpdj4nKQogICAgICAgIGVsaWYgbmFtZSA9PSAiQmVnaW5DaGlsZCI6CiAgICAg
ICAgICAgIGguYXBwZW5kKCc8ZGl2IGNsYXNzPSJjYXJkIj4nKQogICAgICAgIGVsaWYgbmFtZSA9
PSAiRW5kQ2hpbGQiOgogICAgICAgICAgICBoLmFwcGVuZCgnPC9kaXY+JykKICAgICAgICBlbGlm
IG5hbWUgaW4gKCJCZWdpblRhYmxlIiwpOgogICAgICAgICAgICBoLmFwcGVuZCgnPGRpdiBjbGFz
cz0idGFibGVwaCI+dGFibG8nICsgKCcgKCVzKScgJSBsYmwgaWYgbGJsIGVsc2UgJycpICsgJzwv
ZGl2PicpCiAgICAgICAgZWxpZiBuYW1lID09ICJTZXBhcmF0b3IiOgogICAgICAgICAgICBoLmFw
cGVuZCgnPGRpdiBjbGFzcz0ic2VwIj48L2Rpdj4nKQogICAgICAgIGVsaWYgbmFtZSA9PSAiU2Ft
ZUxpbmUiOgogICAgICAgICAgICBoLmFwcGVuZCgnPCEtLSBzYW1lLWxpbmUgLS0+JykKICAgICAg
ICBlbGlmIG5hbWUgaW4gKCJTcGFjaW5nIiwgIkR1bW15IiwgIk5ld0xpbmUiKToKICAgICAgICAg
ICAgaC5hcHBlbmQoJzxkaXYgY2xhc3M9InNwOCI+PC9kaXY+JykKICAgICAgICBlbGlmIG5hbWUg
aW4gKCJUZXh0IiwgIlRleHRXcmFwcGVkIiwgIlRleHRVbmZvcm1hdHRlZCIsICJUZXh0RGlzYWJs
ZWQiLCAiTGFiZWxUZXh0IiwgIkJ1bGxldFRleHQiKToKICAgICAgICAgICAgY2xzID0gInR4dCBk
aW0iIGlmIG5hbWUgPT0gIlRleHREaXNhYmxlZCIgZWxzZSAidHh0IgogICAgICAgICAgICBoLmFw
cGVuZCgnPGRpdiBjbGFzcz0iJXMiPiVzPC9kaXY+JyAlIChjbHMsIGxibCBvciAi4oCmIikpCiAg
ICAgICAgZWxpZiBuYW1lID09ICJUZXh0Q29sb3JlZCI6CiAgICAgICAgICAgIGguYXBwZW5kKCc8
ZGl2IGNsYXNzPSJ0eHQgYWNjIj4lczwvZGl2PicgJSAobGJsIG9yICLigKYiKSkKICAgICAgICBl
bGlmIG5hbWUgaW4gVUlfV0lER0VUOgogICAgICAgICAgICBraW5kID0gVUlfV0lER0VUW25hbWVd
CiAgICAgICAgICAgIGlmIGtpbmQgPT0gImNoZWNrIjoKICAgICAgICAgICAgICAgIGguYXBwZW5k
KCc8bGFiZWwgY2xhc3M9ImNoayI+PHNwYW4gY2xhc3M9ImJveCBvbiI+4pyTPC9zcGFuPiAlczwv
bGFiZWw+JyAlIGxibCkKICAgICAgICAgICAgZWxpZiBraW5kID09ICJ0b2dnbGUiOgogICAgICAg
ICAgICAgICAgaC5hcHBlbmQoJzxsYWJlbCBjbGFzcz0iY2hrIj48c3BhbiBjbGFzcz0idGdsIj48
aT48L2k+PC9zcGFuPiAlczwvbGFiZWw+JyAlIGxibCkKICAgICAgICAgICAgZWxpZiBraW5kID09
ICJidXR0b24iOgogICAgICAgICAgICAgICAgaC5hcHBlbmQoJzxidXR0b24gY2xhc3M9ImJ0biI+
JXM8L2J1dHRvbj4nICUgKGxibCBvciAi4oCmIikpCiAgICAgICAgICAgIGVsaWYga2luZCA9PSAi
c2xpZGVyIjoKICAgICAgICAgICAgICAgIGguYXBwZW5kKCc8ZGl2IGNsYXNzPSJzbGlkIj48c3Bh
bj4lczwvc3Bhbj48ZGl2IGNsYXNzPSJ0cmFjayI+PGk+PC9pPjwvZGl2PjwvZGl2PicgJSBsYmwp
CiAgICAgICAgICAgIGVsaWYga2luZCA9PSAiaW5wdXQiOgogICAgICAgICAgICAgICAgaGludCA9
ICIiCiAgICAgICAgICAgICAgICBtID0gcmUuc2VhcmNoKHInIihbXiJdKikiXHMqLFxzKnJlZics
IGFyZykKICAgICAgICAgICAgICAgIGlmIG5hbWUgPT0gIklucHV0VGV4dFdpdGhIaW50IjoKICAg
ICAgICAgICAgICAgICAgICBwYXJ0cyA9IFJFX1NUUi5maW5kYWxsKGFyZykKICAgICAgICAgICAg
ICAgICAgICBoaW50ID0gcGFydHNbMV0gaWYgbGVuKHBhcnRzKSA+IDEgZWxzZSAiIgogICAgICAg
ICAgICAgICAgaC5hcHBlbmQoJzxpbnB1dCBjbGFzcz0iaW5wIiBwbGFjZWhvbGRlcj0iJXMiPicg
JSAoaGludCBvciBsYmwpKQogICAgICAgICAgICBlbGlmIGtpbmQgPT0gImNvbWJvIjoKICAgICAg
ICAgICAgICAgIGguYXBwZW5kKCc8ZGl2IGNsYXNzPSJjb21ibyI+JXMgPGI+4pa+PC9iPjwvZGl2
PicgJSBsYmwpCiAgICAgICAgICAgIGVsaWYga2luZCA9PSAiaGVhZGVyIjoKICAgICAgICAgICAg
ICAgIGguYXBwZW5kKCc8ZGl2IGNsYXNzPSJoZHIiPiVzPC9kaXY+JyAlIGxibCkKICAgICAgICAg
ICAgZWxzZToKICAgICAgICAgICAgICAgIGguYXBwZW5kKCc8ZGl2IGNsYXNzPSJyb3ciPiVzPC9k
aXY+JyAlIGxibCkKICAgICAgICBlbGlmIG5hbWUuc3RhcnRzd2l0aCgiUHVzaFN0eWxlIikgb3Ig
bmFtZS5zdGFydHN3aXRoKCJQb3BTdHlsZSIpIG9yIFwKICAgICAgICAgICAgIG5hbWUuc3RhcnRz
d2l0aCgiU2V0TmV4dCIpIG9yIG5hbWUuc3RhcnRzd2l0aCgiQmVnaW5Hcm91cCIpIG9yIG5hbWUg
PT0gIkVuZEdyb3VwIiBvciBcCiAgICAgICAgICAgICBuYW1lLnN0YXJ0c3dpdGgoIlRhYmxlU2V0
dXAiKSBvciBuYW1lLnN0YXJ0c3dpdGgoIlRhYmxlTmV4dCIpIG9yIG5hbWUgPT0gIlRhYmxlSGVh
ZGVyc1JvdyIgb3IgXAogICAgICAgICAgICAgbmFtZSBpbiAoIkVuZFRhYmxlIiwgIlRyZWVQb3Ai
LCAiRW5kVG9vbHRpcCIsICJCZWdpblRvb2x0aXAiLCAiSW5kZW50IiwgIlVuaW5kZW50IiwgIkNv
bHVtbnMiLCAiTmV4dENvbHVtbiIpOgogICAgICAgICAgICBwYXNzCiAgICByZXR1cm4gIlxuIi5q
b2luKGgpCgoKZGVmIGNtZF91aSgpOgogICAgIiIiSW1HdWkgYXJheXV6dW51IHRhcmF5aXAgRXh0
cmFcXGd1aS5odG1sIHVyZXRpci4iIiIKICAgIGZpbGVzID0gW10KICAgIGZvciBkcCwgX2QsIGZz
IGluIG9zLndhbGsoVUlfUk9PVCk6CiAgICAgICAgaWYgIlxcYmluIiBpbiBkcCBvciAiXFxvYmoi
IGluIGRwOgogICAgICAgICAgICBjb250aW51ZQogICAgICAgIGZvciBmbiBpbiBzb3J0ZWQoZnMp
OgogICAgICAgICAgICBpZiBmbi5lbmRzd2l0aCgiLmNzIik6CiAgICAgICAgICAgICAgICBmaWxl
cy5hcHBlbmQob3MucGF0aC5qb2luKGRwLCBmbikpCgogICAgcGFuZWxzID0gW10KICAgIGZvciBw
IGluIGZpbGVzOgogICAgICAgIHJlbCA9IG9zLnBhdGgucmVscGF0aChwLCBvcy5wYXRoLmpvaW4o
Uk9PVCwgIk5pZ2h0d2F0Y2giKSkKICAgICAgICBmb3IgbW5hbWUsIGNhbGxzIGluIHVpX2NvbGxl
Y3QocCk6CiAgICAgICAgICAgICMgc2FkZWNlIGNpemltIG1ldG90bGFyaSAoSW1HdWkgY2Fncmlz
aSBpY2VyZW5sZXIpIHZlIG1ha3VsIGJ1eXVrbHVrdGUgb2xhbmxhcgogICAgICAgICAgICBpZiBs
ZW4oY2FsbHMpIDwgNDoKICAgICAgICAgICAgICAgIGNvbnRpbnVlCiAgICAgICAgICAgIHBhbmVs
cy5hcHBlbmQoKHJlbCwgbW5hbWUsIGNhbGxzKSkKCiAgICBwYW5lbHMuc29ydChrZXk9bGFtYmRh
IHg6ICgtbGVuKHhbMl0pLCB4WzFdKSkKCiAgICBMID0gW10KICAgIEwuYXBwZW5kKCI8IURPQ1RZ
UEUgaHRtbD48aHRtbCBsYW5nPSd0cic+PGhlYWQ+PG1ldGEgY2hhcnNldD0ndXRmLTgnPiIpCiAg
ICBMLmFwcGVuZCgiPHRpdGxlPk5pZ2h0d2F0Y2ggLSBHVUkgKGtvZGRhbiB1cmV0aWxkaSk8L3Rp
dGxlPjxzdHlsZT4iKQogICAgTC5hcHBlbmQoIiIiCmJvZHl7bWFyZ2luOjA7YmFja2dyb3VuZDoj
MDAwO2NvbG9yOiUodGV4dClzO2ZvbnQ6MTNweC8xLjUgIlNlZ29lIFVJIixUYWhvbWEsc2Fucy1z
ZXJpZn0KLndyYXB7bWF4LXdpZHRoOjEyNTBweDttYXJnaW46MThweCBhdXRvO3BhZGRpbmc6MCAx
NHB4IDQwcHh9Ci50b3B7Y29sb3I6JSh0ZXh0MilzO2ZvbnQtc2l6ZToxMi41cHg7cGFkZGluZzox
MHB4IDEycHg7Ym9yZGVyOjFweCBzb2xpZCAlKGJvcmRlcilzOwogICAgIGJvcmRlci1yYWRpdXM6
MTJweDtiYWNrZ3JvdW5kOiUoYmcpczttYXJnaW4tYm90dG9tOjE0cHh9Ci50b3AgYntjb2xvcjol
KHRleHQpc30KLnBhbmVse2JhY2tncm91bmQ6JShiZylzO2JvcmRlcjoxcHggc29saWQgJShib3Jk
ZXIpcztib3JkZXItcmFkaXVzOjE0cHg7bWFyZ2luOjEycHggMDtvdmVyZmxvdzpoaWRkZW59Ci5w
aHtkaXNwbGF5OmZsZXg7anVzdGlmeS1jb250ZW50OnNwYWNlLWJldHdlZW47YWxpZ24taXRlbXM6
Y2VudGVyO3BhZGRpbmc6OXB4IDE0cHg7CiAgICBib3JkZXItYm90dG9tOjFweCBzb2xpZCAlKGJv
cmRlcilzO2NvbG9yOiUodGV4dDIpcztmb250LXNpemU6MTIuNXB4fQoucGggYntjb2xvcjolKHRl
eHQpcztmb250LXNpemU6MTNweH0KLnBie3BhZGRpbmc6MTJweH0KLmNhcmR7YmFja2dyb3VuZDol
KGNhcmQpcztib3JkZXI6MXB4IHNvbGlkICUoYm9yZGVyKXM7Ym9yZGVyLXJhZGl1czoxMnB4O3Bh
ZGRpbmc6OXB4IDExcHg7bWFyZ2luOjZweCAwfQoudGFiYmFye2Rpc3BsYXk6ZmxleDtnYXA6NHB4
O2FsaWduLWl0ZW1zOmZsZXgtZW5kO2JvcmRlci1ib3R0b206MXB4IHNvbGlkICUoYm9yZGVyKXM7
CiAgICAgICAgbWFyZ2luOjRweCAwIDEwcHg7ZmxleC13cmFwOndyYXB9Ci50YWJoZWFke3BhZGRp
bmc6NnB4IDEycHg7Ym9yZGVyLXJhZGl1czoxMHB4IDEwcHggMCAwO2JhY2tncm91bmQ6JShzaWRl
YmFyKXM7Y29sb3I6JSh0ZXh0MilzOwogICAgICAgICBmb250LXNpemU6MTIuNXB4O2N1cnNvcjpw
b2ludGVyfQoudGFiaGVhZC5vbntiYWNrZ3JvdW5kOiUoY2FyZClzO2NvbG9yOiUodGV4dClzO2Jv
eC1zaGFkb3c6aW5zZXQgMCAycHggMCAlKGFjY2VudClzfQoudGFiaXRlbXtkaXNwbGF5Om5vbmU7
d2lkdGg6MTAwJSV9Ci50YWJpdGVtLm9ue2Rpc3BsYXk6YmxvY2t9Ci50YWJib2R5e3BhZGRpbmct
dG9wOjRweH0KLnR4dHttYXJnaW46MnB4IDB9Ci50eHQuZGlte2NvbG9yOiUodGV4dDIpc30KLnR4
dC5hY2N7Y29sb3I6JShhY2NlbnRsdClzfQouaGRye2NvbG9yOiUoYWNjZW50bHQpcztmb250LXdl
aWdodDo2MDA7bWFyZ2luOjEwcHggMCA0cHg7Ym9yZGVyLWJvdHRvbToxcHggc29saWQgJShib3Jk
ZXIpcztwYWRkaW5nLWJvdHRvbTozcHh9Ci5jaGt7ZGlzcGxheTpmbGV4O2FsaWduLWl0ZW1zOmNl
bnRlcjtnYXA6N3B4O21hcmdpbjozcHggMH0KLmJveHt3aWR0aDoxNXB4O2hlaWdodDoxNXB4O2Jv
cmRlci1yYWRpdXM6NHB4O2JhY2tncm91bmQ6JShhY2NlbnQpcztjb2xvcjojZmZmOwogICAgIGRp
c3BsYXk6aW5saW5lLWZsZXg7YWxpZ24taXRlbXM6Y2VudGVyO2p1c3RpZnktY29udGVudDpjZW50
ZXI7Zm9udC1zaXplOjEwcHh9Ci50Z2x7d2lkdGg6MzBweDtoZWlnaHQ6MTZweDtib3JkZXItcmFk
aXVzOjlweDtiYWNrZ3JvdW5kOiUoYWNjZW50KXM7ZGlzcGxheTppbmxpbmUtYmxvY2s7cG9zaXRp
b246cmVsYXRpdmV9Ci50Z2wgaXtwb3NpdGlvbjphYnNvbHV0ZTtyaWdodDoycHg7dG9wOjJweDt3
aWR0aDoxMnB4O2hlaWdodDoxMnB4O2JvcmRlci1yYWRpdXM6NTAlJTtiYWNrZ3JvdW5kOiNmZmZ9
Ci5idG57cGFkZGluZzo2cHggMTRweDtib3JkZXItcmFkaXVzOjEwcHg7YmFja2dyb3VuZDolKGlu
cHV0KXM7Ym9yZGVyOjFweCBzb2xpZCAlKGJvcmRlcilzOwogICAgIGNvbG9yOiUodGV4dClzO2Zv
bnQtc2l6ZToxMi41cHg7bWFyZ2luOjNweCAwO2N1cnNvcjpwb2ludGVyfQouYnRuOmhvdmVye2Jh
Y2tncm91bmQ6JShpbnB1dGgpc30KLmlucHt3aWR0aDoyMjBweDtwYWRkaW5nOjZweCAxMHB4O2Jv
cmRlci1yYWRpdXM6MTBweDtiYWNrZ3JvdW5kOiUoaW5wdXQpczsKICAgICBib3JkZXI6MXB4IHNv
bGlkICUoYm9yZGVyKXM7Y29sb3I6JSh0ZXh0KXM7bWFyZ2luOjNweCAwO2Rpc3BsYXk6YmxvY2t9
Ci5jb21ib3twYWRkaW5nOjZweCAxMHB4O2JvcmRlci1yYWRpdXM6MTBweDtiYWNrZ3JvdW5kOiUo
aW5wdXQpcztib3JkZXI6MXB4IHNvbGlkICUoYm9yZGVyKXM7CiAgICAgICBkaXNwbGF5OmlubGlu
ZS1ibG9jazttYXJnaW46M3B4IDB9Ci5zbGlke2Rpc3BsYXk6ZmxleDthbGlnbi1pdGVtczpjZW50
ZXI7Z2FwOjEwcHg7bWFyZ2luOjRweCAwO2NvbG9yOiUodGV4dDIpc30KLnRyYWNre3dpZHRoOjE4
MHB4O2hlaWdodDo2cHg7Ym9yZGVyLXJhZGl1czo0cHg7YmFja2dyb3VuZDolKGlucHV0KXM7cG9z
aXRpb246cmVsYXRpdmV9Ci50cmFjayBpe3Bvc2l0aW9uOmFic29sdXRlO2xlZnQ6NjAlJTt0b3A6
LTRweDt3aWR0aDoxNHB4O2hlaWdodDoxNHB4O2JvcmRlci1yYWRpdXM6NTAlJTsKICAgICAgICAg
YmFja2dyb3VuZDolKGFjY2VudClzO2JveC1zaGFkb3c6MCAwIDhweCAlKGFjY2VudClzfQoucm93
e3BhZGRpbmc6M3B4IDZweDtib3JkZXItcmFkaXVzOjZweDtjb2xvcjolKHRleHQyKXN9Ci5yb3c6
aG92ZXJ7YmFja2dyb3VuZDolKGNhcmRoKXM7Y29sb3I6JSh0ZXh0KXN9Ci5zZXB7aGVpZ2h0OjFw
eDtiYWNrZ3JvdW5kOiUoYm9yZGVyKXM7bWFyZ2luOjhweCAwfQouc3A4e2hlaWdodDo4cHh9Ci50
YWJsZXBoe2JvcmRlcjoxcHggZGFzaGVkICUoYm9yZGVyKXM7Ym9yZGVyLXJhZGl1czo4cHg7Y29s
b3I6JShtdXRlZClzO3BhZGRpbmc6NnB4IDEwcHg7Zm9udC1zaXplOjEycHg7bWFyZ2luOjRweCAw
fQoubGVnZW5ke2NvbG9yOiUodGV4dDIpcztmb250LXNpemU6MTJweDttYXJnaW4tdG9wOjZweH0K
IiIiICUgVUlfVEhFTUUpCiAgICBMLmFwcGVuZCgiPC9zdHlsZT48L2hlYWQ+PGJvZHk+PGRpdiBj
bGFzcz0nd3JhcCc+IikKICAgIEwuYXBwZW5kKCI8ZGl2IGNsYXNzPSd0b3AnPiIpCiAgICBMLmFw
cGVuZCgiPGI+TmlnaHR3YXRjaCBHVUkg4oCUIGtvZGRhbiBvdG9tYXRpayB1cmV0aWxkaTwvYj4g
Jm5ic3A7wrcmbmJzcDsgJXM8YnI+IgogICAgICAgICAgICAgJSBkYXRldGltZS5kYXRldGltZS5u
b3coKS5zdHJmdGltZSgiJWQuJW0uJVkgJUg6JU0iKSkKICAgIEwuYXBwZW5kKCJLYXluYWs6IDxi
Pk5pZ2h0d2F0Y2hcXFVzZXJDb250cm9sc1xcKipcXCouY3M8L2I+IOKAlCBJbUd1aSBjYWdyaWxh
cmkgc2lyYXlsYSB0YXJhbmlyLjxicj4iCiAgICAgICAgICAgICAiUmVua2xlcjogPGI+TWVudGFs
aXR5VGhlbWUuY3M8L2I+IChEYXJrKSBpbGUgYmlyZWJpci4gIgogICAgICAgICAgICAgIlllcmxl
c2ltIHlha2xhc2lrdGlyIChJbUd1aSduaW4gcGlrc2VsIHJlbmRlcidpIGRlZ2lsKTsgPGI+ZXRp
a2V0bGVyLCBzaXJhLCB3aWRnZXQgdGlwbGVyaSBiaXJlYmlyPC9iPi4iKQogICAgTC5hcHBlbmQo
IjxkaXYgY2xhc3M9J2xlZ2VuZCc+VUkga29kdW51IGRlZ2lzdGlyZGlrdGVuIHNvbnJhOiA8Yj5w
eXRob24gYW5hbGl6LnB5IC0tdWk8L2I+ICZyYXJyOyB0YXJheWljaWRhIEY1LjwvZGl2PiIpCiAg
ICBMLmFwcGVuZCgiPC9kaXY+IikKCiAgICBmb3IgcmVsLCBtbmFtZSwgY2FsbHMgaW4gcGFuZWxz
OgogICAgICAgIEwuYXBwZW5kKCI8ZGl2IGNsYXNzPSdwYW5lbCc+IikKICAgICAgICBMLmFwcGVu
ZCgiPGRpdiBjbGFzcz0ncGgnPjxiPiVzPC9iPjxzcGFuPiVzICZuYnNwO8K3Jm5ic3A7ICVkIGNh
Z3JpPC9zcGFuPjwvZGl2PiIKICAgICAgICAgICAgICAgICAlIChtbmFtZSwgcmVsLnJlcGxhY2Uo
IlxcIiwgIi8iKSwgbGVuKGNhbGxzKSkpCiAgICAgICAgTC5hcHBlbmQoIjxkaXYgY2xhc3M9J3Bi
Jz4iKQogICAgICAgIEwuYXBwZW5kKHVpX3dpZGdldHNfdG9faHRtbChjYWxscykpCiAgICAgICAg
TC5hcHBlbmQoIjwvZGl2PjwvZGl2PiIpCgogICAgTC5hcHBlbmQoIjwvZGl2PjxzY3JpcHQ+IikK
ICAgIEwuYXBwZW5kKCIiIgpkb2N1bWVudC5xdWVyeVNlbGVjdG9yQWxsKCcudGFiYmFyJykuZm9y
RWFjaChmdW5jdGlvbihiYXIpewogIHZhciBpdGVtcyA9IGJhci5xdWVyeVNlbGVjdG9yQWxsKCcu
dGFiaXRlbScpOwogIGJhci5wYXJlbnROb2RlLnF1ZXJ5U2VsZWN0b3JBbGwoJy50YWJpdGVtJyku
Zm9yRWFjaChmdW5jdGlvbihpdCl7IH0pOwogIHZhciBoZWFkcyA9IGJhci5xdWVyeVNlbGVjdG9y
QWxsKCcudGFiaGVhZCcpOwogIGhlYWRzLmZvckVhY2goZnVuY3Rpb24oaCxpKXsKICAgIGgub25j
bGljaz1mdW5jdGlvbigpewogICAgICBoZWFkcy5mb3JFYWNoKGZ1bmN0aW9uKHgpe3guY2xhc3NM
aXN0LnJlbW92ZSgnb24nKX0pOwogICAgICBoLmNsYXNzTGlzdC5hZGQoJ29uJyk7CiAgICAgIGRv
Y3VtZW50LnF1ZXJ5U2VsZWN0b3JBbGwoJy50YWJpdGVtJykuZm9yRWFjaChmdW5jdGlvbih0KXt0
LmNsYXNzTGlzdC5yZW1vdmUoJ29uJyl9KTsKICAgICAgdmFyIGhvc3Q9aC5jbG9zZXN0KCcucGIn
KTsKICAgICAgdmFyIGl0cz1ob3N0LnF1ZXJ5U2VsZWN0b3JBbGwoJy50YWJpdGVtJyk7CiAgICAg
IGlmKGl0c1tpXSkgaXRzW2ldLmNsYXNzTGlzdC5hZGQoJ29uJyk7CiAgICB9OwogIH0pOwogIHZh
ciBob3N0PWJhci5wYXJlbnROb2RlOwogIHZhciBpdHM9aG9zdC5xdWVyeVNlbGVjdG9yQWxsKCcu
dGFiaXRlbScpOwogIGlmKGhlYWRzWzBdJiZpdHNbMF0pe2hlYWRzWzBdLmNsYXNzTGlzdC5hZGQo
J29uJyk7aXRzWzBdLmNsYXNzTGlzdC5hZGQoJ29uJyk7fQp9KTsKIiIiKQogICAgTC5hcHBlbmQo
Ijwvc2NyaXB0PjwvYm9keT48L2h0bWw+IikKCiAgICB3aXRoIG9wZW4oVUlfT1VULCAidyIsIGVu
Y29kaW5nPSJ1dGYtOCIpIGFzIGY6CiAgICAgICAgZi53cml0ZSgiXG4iLmpvaW4oTCkpCgogICAg
dG90YWwgPSBzdW0obGVuKGMpIGZvciBfciwgX20sIGMgaW4gcGFuZWxzKQogICAgcHJpbnQoIlRh
cmFuYW4gZG9zeWEgOiAlZCIgJSBsZW4oZmlsZXMpKQogICAgcHJpbnQoIlJlbmRlciBtZXRvZHUg
OiAlZCIgJSBsZW4ocGFuZWxzKSkKICAgIHByaW50KCJJbUd1aSBjYWdyaXNpIDogJWQiICUgdG90
YWwpCiAgICBwcmludCgiWWF6aWxkaSAgICAgICA6ICVzIiAlIFVJX09VVCkKICAgIHByaW50KCJc
bkVuIGJ1eXVrIHBhbmVsbGVyOiIpCiAgICBmb3IgcmVsLCBtbmFtZSwgY2FsbHMgaW4gcGFuZWxz
WzoxMl06CiAgICAgICAgcHJpbnQoIiAgICUtNDJzICUtMjhzICVkIiAlIChyZWwucmVwbGFjZSgi
XFwiLCAiLyIpWzo0Ml0sIG1uYW1lWzoyOF0sIGxlbihjYWxscykpKQoKCiMgPT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PQojIEFOQSBHSVJJUyAoYmF5cmFrbGFyKQojID09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0KZGVmIG1haW4oKToKICAg
IHJhdyA9IHN5cy5hcmd2WzE6XQogICAgYXJncyA9IFthLmxvd2VyKCkgZm9yIGEgaW4gcmF3XQoK
ICAgICMgZXNraSB1c3VsIGtvbXV0bGFyIGRhIGNhbGlzc2luCiAgICBsZWdhY3kgPSB7InVyZXQi
OiAiLS11cmV0IiwgIm1kIjogIi0tbWQiLCAiZmFyayI6ICItLWZhcmsiLCAic296bHVrIjogIi0t
c296bHVrIiwKICAgICAgICAgICAgICAiZG9ncnVsYSI6ICItLWRvZ3J1bGEiLCAiZW51bSI6ICIt
LWVudW0iLCAibW9iZGIiOiAiLS1tb2JkYiIsCiAgICAgICAgICAgICAgImtvZCI6ICItLWtvZCIs
ICJvcm5layI6ICItLW9ybmVrIiwgImhlcHNpIjogIi0taGVwc2kiLAogICAgICAgICAgICAgICJi
aWxnaSI6ICItLWJpbGdpIiwgImNzaGFycCI6ICItLWNzaGFycCIsICJ1aSI6ICItLXVpIiwgImFy
YSI6ICItLWFyYSJ9CiAgICBpZiBhcmdzIGFuZCBub3QgYXJnc1swXS5zdGFydHN3aXRoKCItIik6
CiAgICAgICAgaWYgYXJnc1swXSBpbiBsZWdhY3k6CiAgICAgICAgICAgIGFyZ3NbMF0gPSBsZWdh
Y3lbYXJnc1swXV0KICAgICAgICBlbHNlOgogICAgICAgICAgICBwcmludCgiQmlsaW5tZXllbiBr
b211dDogJXMiICUgcmF3WzBdKQogICAgICAgICAgICBwcmludChfX2RvY19fKQogICAgICAgICAg
ICByZXR1cm4KCiAgICBpZiBub3QgYXJncyBvciAiLS15YXJkaW0iIGluIGFyZ3Mgb3IgIi0taGVs
cCIgaW4gYXJncyBvciAiLWgiIGluIGFyZ3M6CiAgICAgICAgcHJpbnQoX19kb2NfXykKICAgICAg
ICByZXR1cm4KCiAgICBydW5fYWxsID0gIi0taGVwc2kiIGluIGFyZ3MKICAgIGRpZCA9IEZhbHNl
CgogICAgaWYgcnVuX2FsbCBvciAiLS11cmV0IiBpbiBhcmdzOgogICAgICAgIGNtZF91cmV0KCk7
IGRpZCA9IFRydWUKCiAgICBpZiBydW5fYWxsIG9yICItLW1kIiBpbiBhcmdzOgogICAgICAgIHBy
aW50KCk7IGNtZF9tZCgpOyBkaWQgPSBUcnVlCgogICAgaWYgcnVuX2FsbCBvciAiLS1zb3psdWsi
IGluIGFyZ3M6CiAgICAgICAgcHJpbnQoKTsgY21kX3Nvemx1aygpOyBkaWQgPSBUcnVlCgogICAg
aWYgIi0tY3NoYXJwIiBpbiBhcmdzOgogICAgICAgIHByaW50KCk7IGNtZF9jc2hhcnAoKTsgZGlk
ID0gVHJ1ZQoKICAgIGlmICItLXVpIiBpbiBhcmdzOgogICAgICAgIHByaW50KCk7IGNtZF91aSgp
OyBkaWQgPSBUcnVlCgogICAgaWYgcnVuX2FsbCBvciAiLS1mYXJrIiBpbiBhcmdzOgogICAgICAg
IHByaW50KCk7IGNtZF9mYXJrKCk7IGRpZCA9IFRydWUKCiAgICBpZiAiLS1iaWxnaSIgaW4gYXJn
czoKICAgICAgICBwcmludCgpOyBjbWRfYmlsZ2koKTsgZGlkID0gVHJ1ZQoKICAgIGlmIHJ1bl9h
bGwgb3IgIi0tZG9ncnVsYSIgaW4gYXJnczoKICAgICAgICBsb2duYW1lID0gImV2ZW50X3dhdGNo
LmxvZyIKICAgICAgICBpZiAiLS1kb2dydWxhIiBpbiBhcmdzOgogICAgICAgICAgICBpID0gYXJn
cy5pbmRleCgiLS1kb2dydWxhIikKICAgICAgICAgICAgaWYgaSArIDEgPCBsZW4ocmF3KSBhbmQg
bm90IHJhd1tpICsgMV0uc3RhcnRzd2l0aCgiLSIpOgogICAgICAgICAgICAgICAgbG9nbmFtZSA9
IHJhd1tpICsgMV0KICAgICAgICBwcmludCgpOyBjbWRfZG9ncnVsYShsb2duYW1lKTsgZGlkID0g
VHJ1ZQoKICAgIGlmIHJ1bl9hbGwgb3IgIi0tZW51bSIgaW4gYXJnczoKICAgICAgICBwcmludCgp
OyBjbWRfZW51bSgpOyBkaWQgPSBUcnVlCgogICAgaWYgcnVuX2FsbCBvciAiLS1tb2JkYiIgaW4g
YXJnczoKICAgICAgICBwcmludCgpOyBjbWRfbW9iZGIoKTsgZGlkID0gVHJ1ZQoKICAgIGlmIHJ1
bl9hbGwgb3IgIi0ta29kIiBpbiBhcmdzOgogICAgICAgIHByaW50KCk7IGNtZF9rb2Qoc2hvd19h
bGw9KCItLWtvZC1oZXBzaSIgaW4gYXJncyksIHNob3dfbWFwPSgiLS1tZXRvdCIgaW4gYXJncykp
OyBkaWQgPSBUcnVlCgogICAgaWYgIi0tb3JuZWsiIGluIGFyZ3M6CiAgICAgICAgcHJpbnQoKTsg
Y21kX29ybmVrKCk7IGRpZCA9IFRydWUKCiAgICBpZiAiLS1hcmEiIGluIGFyZ3M6CiAgICAgICAg
aSA9IGFyZ3MuaW5kZXgoIi0tYXJhIikKICAgICAgICBpZiBpICsgMSA8IGxlbihyYXcpOgogICAg
ICAgICAgICBwcmludCgpOyBjbWRfYXJhKHJhd1tpICsgMV0pOyBkaWQgPSBUcnVlCiAgICAgICAg
ZWxzZToKICAgICAgICAgICAgcHJpbnQoIltIQVRBXSAtLWFyYSBpY2luIGJpciBrZWxpbWUgdmVy
OiAgcHl0aG9uIGFuYWxpei5weSAtLWFyYSBtb2IiKQoKICAgIGlmIG5vdCBkaWQ6CiAgICAgICAg
cHJpbnQoX19kb2NfXykKCgppZiBfX25hbWVfXyA9PSAiX19tYWluX18iOgogICAgbWFpbigpCg==
:::B64:ANALIZ:END

:::B64:UPDATER:START
IiIiCkFsYmlvbiBPbmxpbmUgLSBUYW0gRG9udXN0dXJ1Y3UgKENvayBEaWxsaSBLdXN1cnN1eiBE
dW1wZXIgKyBTdGF0IEJpcmxlc3RpcmljaSkKPT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0KS1VMTEFO
SU06CiAgICAxLiBFc2tpIHN0YXRsaSBpdGVtcy5qc29uIGRvc3lhc2luaW4gYWRpbmkgIml0ZW1z
X3N0YXRzLmpzb24iIHlhcC4KICAgIDIuIER1bXBlcidpbiB1cmV0dGlnaSAyMyBNQidsaWsgZG9z
eWFuaW4gYWRpbmkgIml0ZW1zX2R1bXBlci5qc29uIiB5YXAuCiAgICAzLiBtb2JzLmpzb24gdmUg
bG9jYWxpemF0aW9uLmpzb24gKGhhbSwgQ09LIERJTExJIFRNWCBrYXluYWdpKSBkb3N5YWxhcmlu
aSB5YW5pbmEga295LgogICAgNC4gcHl0aG9uIFVwZGF0ZS5weQoKQ0lLVElMQVIgKEhlbHBlci8g
a2xhc29ydW5lKToKICAgIGl0ZW1zLm1pbi5qc29uICAgICAgICAgICAgICAtPiBkaWxkZW4gYmFn
aW1zaXosIElQIHZlcmlzaQogICAgbG9jYWxpemF0aW9uX3tESUx9Lmpzb24gICAgIC0+IGhlciBk
aWwgaWNpbiBpc2xlbm1pcyBsb2thbGl6YXN5b24gY2FjaGUnaQogICAgaXRlbXNfe0RJTH0udHh0
ICAgICAgICAgICAgIC0+IEluZGV4OlVuaXF1ZU5hbWU6RGlzcGxheU5hbWU6SVAKICAgIG1vYnNf
e0RJTH1fbWluLmpzb24gICAgICAgICAtPiBoZXIgZGlsIGljaW4gY2V2cmlsbWlzIG1vYiB2ZXJp
c2kKICAgIE1vYnNJRF97RElMfS50eHQgICAgICAgICAgICAtPiBbaW5kZXhdIDogQ2V2cmlsbWlz
IElzaW0KCk5PVDogQ09ORklHWyJsYW5ndWFnZXMiXSBsaXN0ZXNpIGJvcyBiaXJha2lsaXJzYSwg
bG9jYWxpemF0aW9uLmpzb24gaWNpbmRla2kKICAgICBUVU0gZGlsbGVyIG90b21hdGlrIHRlc3Bp
dCBlZGlsaXAgdXJldGlsaXIuCiIiIgoKaW1wb3J0IGpzb24sIG9zLCBzeXMKCiMgKz09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0rCiMgfCAgQ09ORklH
IC0gQnVyYWRhbiBkb3N5YSB5b2xsYXJpbmkgYXlhcmxhICAgICAgICAgICAgfAojICs9PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09KwpDT05GSUcgPSB7
CiAgICAiaXRlbXNfc3RhdHNfanNvbiI6ICAgICJpdGVtc19zdGF0cy5qc29uIiwgICAgICAgICMg
U3RhdGxhcmluIChJUCB2Yi4pIG9sZHVndSBlc2tpIGRvc3lhCiAgICAiaXRlbXNfZHVtcGVyX2pz
b24iOiAgICJpdGVtc19kdW1wZXIuanNvbiIsICAgICAgICMgRHVtcGVyJ2luIHZlcmRpZ2kgZ2Vy
Y2VrIElEIGRvc3lhc2kKICAgICJtb2JzX2pzb24iOiAgICAgICAgICAgIm1vYnMuanNvbiIsCiAg
ICAibG9jYWxpemF0aW9uX2pzb24iOiAgICJsb2NhbGl6YXRpb24uanNvbiIsICAgICAgICMgSEFN
LCBDT0sgRElMTEkgVE1YIGtheW5hZ2kgKHR1bSBkaWxsZXIgYnVyYWRhKQogICAgIm91dHB1dF9k
aXIiOiAgICAgICAgICAiSGVscGVyIiwKCiAgICAiaXRlbXNfbWluX2pzb24iOiAgICAgICJpdGVt
cy5taW4uanNvbiIsICAgICAgICAgICMgZGlsZGVuIGJhZ2ltc2l6CgogICAgIyBEaWwgYmFzaW5h
IHVyZXRpbGVjZWsgZG9zeWEgYWRpIGthbGlwbGFyaSAoe1NVRkZJWH0gb3RvbWF0aWsgZGVnaXNp
ciwgb3JuIEVOL1JVL1RSL1pIKQogICAgImxvY2FsaXphdGlvbl9jYWNoZV9wYXR0ZXJuIjogImxv
Y2FsaXphdGlvbl97U1VGRklYfS5qc29uIiwKICAgICJpdGVtc190eHRfcGF0dGVybiI6ICAgICAg
ICAgICJpdGVtc197U1VGRklYfS50eHQiLAogICAgIm1vYnNfbWluX3BhdHRlcm4iOiAgICAgICAg
ICAgIm1vYnNfe1NVRkZJWH1fbWluLmpzb24iLAogICAgIm1vYnNpZF90eHRfcGF0dGVybiI6ICAg
ICAgICAgIk1vYnNJRF97U1VGRklYfS50eHQiLAoKICAgICJnZW5lcmF0ZV9pdGVtcyI6ICAgICAg
VHJ1ZSwKICAgICJnZW5lcmF0ZV9tb2JzIjogICAgICAgVHJ1ZSwKICAgICJnZW5lcmF0ZV9pdGVt
c190eHQiOiAgVHJ1ZSwKICAgICJnZW5lcmF0ZV9tb2JzX3R4dCI6ICAgVHJ1ZSwKICAgICJnZW5l
cmF0ZV9zcGVsbHMiOiAgICAgVHJ1ZSwKICAgICJnZW5lcmF0ZV96b25lcyI6ICAgICAgVHJ1ZSwK
CiAgICAic3BlbGxzX2pzb24iOiAgICAgICAgICJzcGVsbHMuanNvbiIsICAgICAgICAgIyBEdW1w
ZXInZGVuIGdlbGVuIGhhbSBzcGVsbHMga2F5bmFnaQogICAgInNwZWxsc19vdXRwdXQiOiAgICAg
ICAic3BlbGxzLm1pbi5qc29uIiwgICAgICAjIFVyZXRpbGVjZWsgY2lrdGkgZG9zeWFzaQoKICAg
ICJ3b3JsZF9qc29uIjogICAgICAgICAgIndvcmxkLmpzb24iLCAgICAgICAgICAgIyBEdW1wZXIn
ZGVuIGdlbGVuIHdvcmxkL3pvbmUgbGlzdGVzaQogICAgInpvbmVzX2Jhc2VfanNvbiI6ICAgICAi
em9uZXNfYmFzZS5qc29uIiwgICAgICAjIE1ldmN1dCB6b25lcy5qc29uIChiYXogZG9zeWEsIHll
bmkgem9ubGFyIGJ1cmF5YSBla2xlbmlyKQogICAgInpvbmVzX291dHB1dCI6ICAgICAgICAiem9u
ZXMuanNvbiIsICAgICAgICAgICAjIFVyZXRpbGVjZWsgY2lrdGkgZG9zeWFzaQoKICAgICMgVXJl
dGlsZWNlayBkaWxsZXIuIFRNWCB4bWw6bGFuZyBrb2R1IGJpcmViaXIgc3RyaW5nICgiRU4tVVMi
KSB5YSBkYQogICAgIyB7InRteCI6ICJFTi1VUyIsICJzdWZmaXgiOiAiRU4ifSBzZWtsaW5kZSBv
emVsIHN1ZmZpeCBkZSB2ZXJpbGViaWxpci4KICAgICMgQk9TIExJU1RFIChbXSkgYmlyYWtpbGly
c2EgVE1YIGljaW5kZWtpIFRVTSBkaWxsZXIgb3RvbWF0aWsgYnVsdW51ci4KICAgICJsYW5ndWFn
ZXMiOiBbIkVOLVVTIiwgIlJVLVJVIiwgIlRSLVRSIiwgIlpILUNOIl0sCgogICAgIm1vYnNfaW5k
ZXhfb2Zmc2V0IjogICAxNCwKICAgICJlbmNoYW50X2lwX3N0ZXAiOiAgICAgMTAwLAogICAgInBy
b3RvdHlwZV9lbmNfYmFzZSI6ICAxMjAwLAp9CgoKIyArPT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PSsKIyB8ICBHZW5lbCB5YXJkaW1jaWxhciAgICAg
ICAgICAgICAgICAgICAgICAgICAgICAgICAgICB8CiMgKz09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0rCmRlZiBsb2FkX2pzb24ocGF0aCwgb3B0aW9u
YWw9RmFsc2UpOgogICAgaWYgbm90IG9zLnBhdGguZXhpc3RzKHBhdGgpOgogICAgICAgIGlmIG9w
dGlvbmFsOiByZXR1cm4gTm9uZQogICAgICAgIHByaW50KGYiXG5bSEFUQV0gRG9zeWEgYnVsdW5h
bWFkaToge29zLnBhdGguYWJzcGF0aChwYXRoKX0iKQogICAgICAgIHN5cy5leGl0KDEpCiAgICBz
aXplX21iID0gb3MucGF0aC5nZXRzaXplKHBhdGgpIC8gKDEwMjQgKiAxMDI0KQogICAgcHJpbnQo
ZiIgIFl1a2xlbml5b3IgOiB7cGF0aH0gKHtzaXplX21iOi4xZn0gTUIpIC4uLiIsIGVuZD0iICIs
IGZsdXNoPVRydWUpCiAgICB3aXRoIG9wZW4ocGF0aCwgZW5jb2Rpbmc9InV0Zi04IikgYXMgZjoK
ICAgICAgICBkYXRhID0ganNvbi5sb2FkKGYpCiAgICBwcmludCgiT0siKQogICAgcmV0dXJuIGRh
dGEKCmRlZiBzYXZlX2pzb24oZGF0YSwgcGF0aCk6CiAgICB3aXRoIG9wZW4ocGF0aCwgInciLCBl
bmNvZGluZz0idXRmLTgiKSBhcyBmOgogICAgICAgIGpzb24uZHVtcChkYXRhLCBmLCBlbnN1cmVf
YXNjaWk9RmFsc2UsIHNlcGFyYXRvcnM9KCIsIiwgIjoiKSkKICAgIHNpemVfa2IgPSBvcy5wYXRo
LmdldHNpemUocGF0aCkgLyAxMDI0CiAgICBjb3VudCA9IGxlbihkYXRhKSBpZiBpc2luc3RhbmNl
KGRhdGEsIChsaXN0LCBkaWN0KSkgZWxzZSAiLSIKICAgIHByaW50KGYiICBLYXlkZWRpbGRpIDog
e3BhdGh9ICh7Y291bnR9IGtheWl0LCB7c2l6ZV9rYjouMWZ9IEtCKSIpCgpkZWYgd3JpdGVfbGlu
ZXNfY3JsZihwYXRoLCBsaW5lcyk6CiAgICAiIiJEb3N5YXlpIFxcclxcbiBzYXRpciBzb25sYXJp
eWxhLCBzb24gc2F0aXJpbiBhcmRpbmRhIGZhemxhZGFuIGJvcyBzYXRpciBvbG1hZGFuIHlhemFy
LiIiIgogICAgd2l0aCBvcGVuKHBhdGgsICJ3IiwgZW5jb2Rpbmc9InV0Zi04IiwgbmV3bGluZT0i
IikgYXMgZjoKICAgICAgICBmLndyaXRlKCJcclxuIi5qb2luKGxpbmVzKSkKICAgIHByaW50KGYi
ICBLYXlkZWRpbGRpIDoge3BhdGh9ICh7bGVuKGxpbmVzKX0gc2F0aXIsIHtvcy5wYXRoLmdldHNp
emUocGF0aCkvMTAyNDouMWZ9IEtCKSIpCgpkZWYgb3V0KGZpbGVuYW1lKToKICAgIGQgPSBDT05G
SUcuZ2V0KCJvdXRwdXRfZGlyIiwgIiIpLnN0cmlwKCkKICAgIGlmIGQ6CiAgICAgICAgb3MubWFr
ZWRpcnMoZCwgZXhpc3Rfb2s9VHJ1ZSkKICAgICAgICByZXR1cm4gb3MucGF0aC5qb2luKGQsIGZp
bGVuYW1lKQogICAgcmV0dXJuIGZpbGVuYW1lCgojICs9PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09KwojIHwgIEdJUkRJIEFSQU1BIChEVVpFTFRNRSAt
IDA4LjEwLjIwMjYpICAgICAgICAgICAgICAgIHwKIyArPT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PSsKIyBTT1JVTjogdXBkYXRlciBnaXJkaWxlcmkg
KHdvcmxkLmpzb24gLyB6b25lc19iYXNlLmpzb24gLyBzcGVsbHMuanNvbikgS0VOREkga2xhc29y
dW5kZQojIGFyaXlvcmR1LiBEdW1wZXIgY2lrdGlsYXJpIGlzZSBiYXNrYSB5ZXJkZSAob3JuLiBD
OlxvdXRwdXRcZm9ybWF0dGVkKSBkdXJ1eW9yLgojIFNvbnVjOiAid29ybGQuanNvbiBidWx1bmFt
YWRpIC0+IEFUTEFOREkiIHZlIHpvbmVzLmpzb24gSElDIHVyZXRpbG1peW9yZHUuCiMgQVlSSUNB
OiBiYXogZG9zeWEgKGVza2kgem9uZXMuanNvbikgYnVsdW5hbWF6c2Egc2lmaXJkYW4gYmFzbGl5
b3IgdmUgVFVNIGVza2kKIyB6b25lIGJpbGdpc2kgKG5hbWUvdHlwZS9wdnBUeXBlL3RpZXIvZmls
ZS9ib3VuZHMpIEtBWUJPTFVZT1JEVS4KIyBDT1pVTTogYXNhZ2lkYWtpIGFkYXkgeW9sbGFyZGEg
c2lyYXlsYSBhcmE7IGlsayBidWx1bmFuaSBrdWxsYW4gdmUgaGFuZ2lzaW5pCiMga3VsbGFuZGln
aW5pIEVLUkFOQSBZQVouClNFQVJDSF9ESVJTID0gWwogICAgIi4iLCAgICAgICAgICAgICAgICAg
ICAgICAgICAgICAgICAgICAgIyBzY3JpcHQga2xhc29ydSAoVEFTSU5BQklMSVIgLSBhcmFjIG5l
cmV5ZSBrb3B5YWxhbmlyc2Egb3Jhc2kpCiAgICAiSGVscGVyIiwgICAgICAgICAgICAgICAgICAg
ICAgICAgICAgICAjIHVwZGF0ZXIgY2lrdGlsYXJpCiAgICByIkM6XG91dHB1dFxmb3JtYXR0ZWQi
LCAgICAgICAgICAgICAgICAjIFNBQklUIGtsYXNvcjogZHVtcGVyIHRlbWl6bGVubWlzIGNpa3Rp
bGFyCiAgICByIkM6XG91dHB1dFxjbHVzdGVyIiwgICAgICAgICAgICAgICAgICAjIFNBQklUIGts
YXNvcjogZHVtcGVyIGhhbSBjbHVzdGVyIGNpa3RpbGFyCiAgICByIkM6XG91dHB1dCIsICAgICAg
ICAgICAgICAgICAgICAgICAgICAjIFNBQklUIGtsYXNvcjogZHVtcGVyIGtvawogICAgIl9naXJk
aSIsICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIyAob3BzaXlvbmVsKSBhcmFjaW4gWUFO
SU5EQUtJIHZlcmkga2xhc29ydQogICAgb3MucGF0aC5qb2luKCIuLiIsICJfZ2lyZGkiKSwgICAg
ICAgICAgIyAob3BzaXlvbmVsKSBiaXIgdXN0IGtsYXNvcmRla2kgdmVyaSBrbGFzb3J1CiAgICBv
cy5wYXRoLmpvaW4oIi4uIiwgIi4uIiwgIk5pZ2h0d2F0Y2giLCAiQXNzZXRzIiwgIkhlbHBlciIp
LCAgICMgdXlndWxhbWFuaW4gQ0FOTEkgem9uZXMuanNvbidpCl0KCmRlZiByZXNvbHZlX2lucHV0
KG5hbWUsIGV4dHJhPU5vbmUpOgogICAgIiIiVmVyaWxlbiBkb3N5YSBhZGluaSBhZGF5IGtsYXNv
cmxlcmRlIGFyYXI7IGlsayBidWx1bmFuIHRhbSB5b2x1IGRvbmR1cnVyLiIiIgogICAgY2FuZHMg
PSBsaXN0KGV4dHJhIG9yIFtdKSArIFtvcy5wYXRoLmpvaW4oZCwgbmFtZSkgZm9yIGQgaW4gU0VB
UkNIX0RJUlNdCiAgICBmb3IgYyBpbiBjYW5kczoKICAgICAgICBpZiBjIGFuZCBvcy5wYXRoLmV4
aXN0cyhjKToKICAgICAgICAgICAgcmV0dXJuIGMKICAgIHJldHVybiBOb25lCgpkZWYgaXRlcl9p
dGVtX3R5cGUoaXRlbXNfcm9vdCwgaXRlbV90eXBlKToKICAgIGVudHJpZXMgPSBpdGVtc19yb290
LmdldChpdGVtX3R5cGUsIFtdKQogICAgaWYgaXNpbnN0YW5jZShlbnRyaWVzLCBkaWN0KTogZW50
cmllcyA9IFtlbnRyaWVzXQogICAgcmV0dXJuIGVudHJpZXMKCgojICs9PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09KwojIHwgIERpbCBsaXN0ZXNpIGNv
enVtbGVtZSAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIHwKIyArPT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PSsKZGVmIG5vcm1hbGl6ZV9sYW5n
dWFnZXMobGFuZ3VhZ2VzX2NmZyk6CiAgICByZXN1bHQgPSBbXQogICAgZm9yIGVudHJ5IGluIGxh
bmd1YWdlc19jZmc6CiAgICAgICAgaWYgaXNpbnN0YW5jZShlbnRyeSwgZGljdCk6CiAgICAgICAg
ICAgIHRteCA9IGVudHJ5WyJ0bXgiXQogICAgICAgICAgICBzdWZmaXggPSBlbnRyeS5nZXQoInN1
ZmZpeCIpIG9yIHRteC5zcGxpdCgiLSIpWzBdLnVwcGVyKCkKICAgICAgICBlbHNlOgogICAgICAg
ICAgICB0bXggPSBlbnRyeQogICAgICAgICAgICBzdWZmaXggPSB0bXguc3BsaXQoIi0iKVswXS51
cHBlcigpCiAgICAgICAgcmVzdWx0LmFwcGVuZCgodG14LCBzdWZmaXgpKQogICAgcmV0dXJuIHJl
c3VsdAoKZGVmIGRpc2NvdmVyX2xhbmd1YWdlcyhjZmcpOgogICAgIiIiVE1YIGtheW5hZ2luZGFr
aSBUVU0geG1sOmxhbmcga29kbGFyaW5pIHRlc3BpdCBlZGVyIChDT05GSUdbJ2xhbmd1YWdlcydd
IGJvcyBpc2Uga3VsbGFuaWxpcikuIiIiCiAgICBzcmMgPSBjZmdbImxvY2FsaXphdGlvbl9qc29u
Il0KICAgIGlmIG5vdCBvcy5wYXRoLmV4aXN0cyhzcmMpOgogICAgICAgIHByaW50KGYiXG5bSEFU
QV0gRGlsbGVyIG90b21hdGlrIHRlc3BpdCBlZGlsZW1peW9yLCAne3NyY30nIGJ1bHVuYW1hZGku
IikKICAgICAgICByZXR1cm4gW10KICAgIHByaW50KGYiXG5bRGlsbGVyIG90b21hdGlrIHRlc3Bp
dCBlZGlsaXlvciAtPiB7c3JjfV0iKQogICAgZGF0YSA9IGxvYWRfanNvbihzcmMpCiAgICBmb3Vu
ZCA9IHNldCgpCgogICAgZGVmIHNjYW5fdHUodHUpOgogICAgICAgIHR1diA9IHR1LmdldCgidHV2
IiwgW10pCiAgICAgICAgaWYgaXNpbnN0YW5jZSh0dXYsIGRpY3QpOiB0dXYgPSBbdHV2XQogICAg
ICAgIGZvciB0IGluIHR1djoKICAgICAgICAgICAgeGwgPSB0LmdldCgiQHhtbDpsYW5nIiwgIiIp
CiAgICAgICAgICAgIGlmIHhsOiBmb3VuZC5hZGQoeGwudXBwZXIoKSkKCiAgICB0cnk6IHR1X2xp
c3QgPSBkYXRhWyJ0bXgiXVsiYm9keSJdWyJ0dSJdCiAgICBleGNlcHQgRXhjZXB0aW9uOiB0dV9s
aXN0ID0gTm9uZQoKICAgIGlmIHR1X2xpc3QgaXMgbm90IE5vbmU6CiAgICAgICAgaWYgaXNpbnN0
YW5jZSh0dV9saXN0LCBkaWN0KTogdHVfbGlzdCA9IFt0dV9saXN0XQogICAgICAgIGZvciB0dSBp
biB0dV9saXN0OiBzY2FuX3R1KHR1KQogICAgZWxzZToKICAgICAgICBkZWYgZXh0cmFjdChvYmop
OgogICAgICAgICAgICBpZiBpc2luc3RhbmNlKG9iaiwgZGljdCk6CiAgICAgICAgICAgICAgICBp
ZiAidHV2IiBpbiBvYmo6IHNjYW5fdHUob2JqKQogICAgICAgICAgICAgICAgZWxzZToKICAgICAg
ICAgICAgICAgICAgICBmb3IgdiBpbiBvYmoudmFsdWVzKCk6CiAgICAgICAgICAgICAgICAgICAg
ICAgIGlmIGlzaW5zdGFuY2UodiwgKGRpY3QsIGxpc3QpKTogZXh0cmFjdCh2KQogICAgICAgICAg
ICBlbGlmIGlzaW5zdGFuY2Uob2JqLCBsaXN0KToKICAgICAgICAgICAgICAgIGZvciBpdGVtIGlu
IG9iajogZXh0cmFjdChpdGVtKQogICAgICAgIGV4dHJhY3QoZGF0YSkKCiAgICBsYW5ncyA9IHNv
cnRlZChmb3VuZCkKICAgIHByaW50KGYiICBCdWx1bmFuIGRpbGxlciA6IHtsYW5nc30iKQogICAg
cmV0dXJuIFsoY29kZSwgY29kZS5zcGxpdCgiLSIpWzBdLnVwcGVyKCkpIGZvciBjb2RlIGluIGxh
bmdzXQoKZGVmIHJlc29sdmVfbGFuZ3VhZ2VzKGNmZyk6CiAgICBsYW5ndWFnZXNfY2ZnID0gY2Zn
LmdldCgibGFuZ3VhZ2VzIikgb3IgW10KICAgIGlmIGxhbmd1YWdlc19jZmc6CiAgICAgICAgcmV0
dXJuIG5vcm1hbGl6ZV9sYW5ndWFnZXMobGFuZ3VhZ2VzX2NmZykKICAgIHJldHVybiBkaXNjb3Zl
cl9sYW5ndWFnZXMoY2ZnKQoKCiMgKz09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT0rCiMgfCAgTG9rYWxpemFzeW9uIChjb2sgZGlsbGksIHRlayBnZWNp
c2xpIFRNWCBva3VtYSkgICAgIHwKIyArPT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PSsKZGVmIGxvYWRfb3JfYnVpbGRfbG9jYWxpemF0aW9uX211bHRp
KGNmZywgbGFuZ3VhZ2VzKToKICAgICIiIgogICAgbGFuZ3VhZ2VzOiBbKHRteF9jb2RlLCBzdWZm
aXgpLCAuLi5dCiAgICBEb251czoge3N1ZmZpeDoge3R1aWQ6IHRleHR9fQogICAgQ2FjaGUnaSBk
aXNrdGUgb2xhbiBkaWxsZXIgaWNpbiBUTVgnZSBoaWMgZG9rdW5tYXouCiAgICBFa3NpayBvbGFu
bGFyaW4gaGVwc2luaSBURUsgYmlyIFRNWCBnZWNpc2luZGUgYmlybGlrdGUgdXJldGlyLgogICAg
IiIiCiAgICBwcmludCgiXG5bTG9rYWxpemFzeW9uXSIpCiAgICByZXN1bHQgPSB7fQogICAgbWlz
c2luZyA9IFtdCiAgICBmb3IgdG14X2NvZGUsIHN1ZmZpeCBpbiBsYW5ndWFnZXM6CiAgICAgICAg
Y2FjaGVfcGF0aCA9IG91dChjZmdbImxvY2FsaXphdGlvbl9jYWNoZV9wYXR0ZXJuIl0uZm9ybWF0
KFNVRkZJWD1zdWZmaXgpKQogICAgICAgIGlmIG9zLnBhdGguZXhpc3RzKGNhY2hlX3BhdGgpOgog
ICAgICAgICAgICBkYXRhID0gbG9hZF9qc29uKGNhY2hlX3BhdGgpCiAgICAgICAgICAgIHByaW50
KGYiICBDYWNoZSdkZW4gIDoge3N1ZmZpeH0gLT4ge2xlbihkYXRhKX0gY2V2aXJpIikKICAgICAg
ICAgICAgcmVzdWx0W3N1ZmZpeF0gPSBkYXRhCiAgICAgICAgZWxzZToKICAgICAgICAgICAgbWlz
c2luZy5hcHBlbmQoKHRteF9jb2RlLCBzdWZmaXgsIGNhY2hlX3BhdGgpKQoKICAgIGlmIG5vdCBt
aXNzaW5nOgogICAgICAgIHJldHVybiByZXN1bHQKCiAgICBzcmNfcGF0aCA9IGNmZ1sibG9jYWxp
emF0aW9uX2pzb24iXQogICAgaWYgbm90IG9zLnBhdGguZXhpc3RzKHNyY19wYXRoKToKICAgICAg
ICBwcmludChmIiAgW0hBVEFdICd7c3JjX3BhdGh9JyBidWx1bmFtYWRpISBFa3NpayBkaWwgY2Fj
aGUnbGVyaSB1cmV0aWxlbWl5b3I6ICIKICAgICAgICAgICAgICBmIntbcyBmb3IgXywgcywgXyBp
biBtaXNzaW5nXX0iKQogICAgICAgIGZvciBfLCBzdWZmaXgsIF8gaW4gbWlzc2luZzoKICAgICAg
ICAgICAgcmVzdWx0W3N1ZmZpeF0gPSB7fQogICAgICAgIHJldHVybiByZXN1bHQKCiAgICBwcmlu
dChmIiAgVE1YIGtheW5hZ2kgb2t1bnV5b3IgKGVrc2lrIGRpbGxlcjoge1tzIGZvciBfLCBzLCBf
IGluIG1pc3NpbmddfSkiKQogICAgZGF0YSA9IGxvYWRfanNvbihzcmNfcGF0aCkKCiAgICB3YW50
ZWQgPSB7dG14X2NvZGUudXBwZXIoKTogc3VmZml4IGZvciB0bXhfY29kZSwgc3VmZml4LCBfIGlu
IG1pc3Npbmd9CiAgICBidWNrZXRzID0ge3N1ZmZpeDoge30gZm9yIF8sIHN1ZmZpeCwgXyBpbiBt
aXNzaW5nfQogICAgY291bnRzICA9IHtzdWZmaXg6IDAgZm9yIF8sIHN1ZmZpeCwgXyBpbiBtaXNz
aW5nfQoKICAgIGRlZiBwcm9jZXNzX3R1KHR1KToKICAgICAgICB0dWlkID0gdHUuZ2V0KCJAdHVp
ZCIsICIiKQogICAgICAgIGlmIG5vdCB0dWlkOiByZXR1cm4KICAgICAgICB0dXYgPSB0dS5nZXQo
InR1diIsIFtdKQogICAgICAgIGlmIGlzaW5zdGFuY2UodHV2LCBkaWN0KTogdHV2ID0gW3R1dl0K
ICAgICAgICBmb3IgdCBpbiB0dXY6CiAgICAgICAgICAgIHhtbF9sYW5nID0gdC5nZXQoIkB4bWw6
bGFuZyIsICIiKS51cHBlcigpCiAgICAgICAgICAgIHN1ZiA9IHdhbnRlZC5nZXQoeG1sX2xhbmcp
CiAgICAgICAgICAgIGlmIHN1ZjoKICAgICAgICAgICAgICAgIHNlZyA9IHQuZ2V0KCJzZWciLCAi
IikKICAgICAgICAgICAgICAgIGlmIGlzaW5zdGFuY2Uoc2VnLCBzdHIpIGFuZCBzZWc6CiAgICAg
ICAgICAgICAgICAgICAgYnVja2V0c1tzdWZdW3R1aWQubHN0cmlwKCJAIildID0gc2VnCiAgICAg
ICAgICAgICAgICAgICAgY291bnRzW3N1Zl0gKz0gMQoKICAgIHRyeTogdHVfbGlzdCA9IGRhdGFb
InRteCJdWyJib2R5Il1bInR1Il0KICAgIGV4Y2VwdCBFeGNlcHRpb246IHR1X2xpc3QgPSBOb25l
CgogICAgaWYgdHVfbGlzdCBpcyBub3QgTm9uZToKICAgICAgICBpZiBpc2luc3RhbmNlKHR1X2xp
c3QsIGRpY3QpOiB0dV9saXN0ID0gW3R1X2xpc3RdCiAgICAgICAgZm9yIHR1IGluIHR1X2xpc3Q6
IHByb2Nlc3NfdHUodHUpCiAgICBlbHNlOgogICAgICAgIGRlZiBleHRyYWN0KG9iaik6CiAgICAg
ICAgICAgIGlmIGlzaW5zdGFuY2Uob2JqLCBkaWN0KToKICAgICAgICAgICAgICAgIGlmICJAdHVp
ZCIgaW4gb2JqOiBwcm9jZXNzX3R1KG9iaikKICAgICAgICAgICAgICAgIGVsc2U6CiAgICAgICAg
ICAgICAgICAgICAgZm9yIHYgaW4gb2JqLnZhbHVlcygpOgogICAgICAgICAgICAgICAgICAgICAg
ICBpZiBpc2luc3RhbmNlKHYsIChkaWN0LCBsaXN0KSk6IGV4dHJhY3QodikKICAgICAgICAgICAg
ZWxpZiBpc2luc3RhbmNlKG9iaiwgbGlzdCk6CiAgICAgICAgICAgICAgICBmb3IgaXRlbSBpbiBv
Ymo6IGV4dHJhY3QoaXRlbSkKICAgICAgICBleHRyYWN0KGRhdGEpCgogICAgZm9yIHRteF9jb2Rl
LCBzdWZmaXgsIGNhY2hlX3BhdGggaW4gbWlzc2luZzoKICAgICAgICBwcmludChmIiAgQnVsdW5h
biAoe3N1ZmZpeH0pOiB7Y291bnRzW3N1ZmZpeF19IGNldmlyaSIpCiAgICAgICAgc2F2ZV9qc29u
KGJ1Y2tldHNbc3VmZml4XSwgY2FjaGVfcGF0aCkKICAgICAgICByZXN1bHRbc3VmZml4XSA9IGJ1
Y2tldHNbc3VmZml4XQoKICAgIHJldHVybiByZXN1bHQKCgojICs9PT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09KwojIHwgIGl0ZW1zLm1pbi5qc29uIChk
aWxkZW4gYmFnaW1zaXopICAgICAgICAgICAgICAgICAgIHwKIyArPT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PSsKZGVmIGdldF9lbmNoYW50X2xldmVs
cyhlbnRyeSk6CiAgICB0aWVyID0gaW50KGVudHJ5LmdldCgiQHRpZXIiLCAwKSBvciAwKQogICAg
aWYgdGllciA8IDQgb3IgIkRFQlVHIiBpbiBlbnRyeS5nZXQoIkB1bmlxdWVuYW1lIiwgIiIpOiBy
ZXR1cm4gW10KICAgIGNhbmJlb3ZlcmNoYXJnZWQgID0gZW50cnkuZ2V0KCJAY2FuYmVvdmVyY2hh
cmdlZCIsICIiKQogICAgc2xvdHR5cGUgICAgICAgICAgPSBlbnRyeS5nZXQoIkBzbG90dHlwZSIs
ICIiKQogICAgc2hvd2lubWFya2V0cGxhY2UgPSBlbnRyeS5nZXQoIkBzaG93aW5tYXJrZXRwbGFj
ZSIsICIiKQogICAgaWYgY2FuYmVvdmVyY2hhcmdlZCA9PSAidHJ1ZSI6IHJldHVybiBbMSwgMiwg
M10gaWYgc2hvd2lubWFya2V0cGxhY2UgPT0gImZhbHNlIiBlbHNlIFsxLCAyLCAzLCA0XQogICAg
aWYgc2xvdHR5cGUgaW4gKCJjYXBlIiwgImJhZyIpIGFuZCBzaG93aW5tYXJrZXRwbGFjZSAhPSAi
ZmFsc2UiOiByZXR1cm4gWzEsIDIsIDMsIDRdCiAgICByZXR1cm4gW10KCmRlZiBjYWxjX2VuY19p
cChiYXNlX2lwLCBsdmwsIGNmZyk6CiAgICBzdGVwID0gY2ZnWyJlbmNoYW50X2lwX3N0ZXAiXQog
ICAgaWYgYmFzZV9pcCA+PSAxNjAwOiByZXR1cm4gY2ZnWyJwcm90b3R5cGVfZW5jX2Jhc2UiXSAr
IChsdmwgLSAxKSAqIHN0ZXAKICAgIHJldHVybiBiYXNlX2lwICsgbHZsICogc3RlcAoKZGVmIGJ1
aWxkX2l0ZW1zX21pbihpdGVtc19qc29uX3BhdGgsIG91dHB1dF9wYXRoLCBjZmcpOgogICAgcHJp
bnQoIlxuW2l0ZW1zLm1pbi5qc29uIHVyZXRpbGl5b3IgKGRpbGRlbiBiYWdpbXNpeildIikKICAg
IGRhdGEgICAgICAgPSBsb2FkX2pzb24oaXRlbXNfanNvbl9wYXRoKQogICAgaXRlbXNfcm9vdCA9
IGRhdGEuZ2V0KCJpdGVtcyIsIGRhdGEpCiAgICBJVEVNX1RZUEVTID0gWwogICAgICAgICJlcXVp
cG1lbnRpdGVtIiwgIndlYXBvbiIsICJtb3VudCIsICJ0cmFja2luZ2l0ZW0iLAogICAgICAgICJz
aW1wbGVpdGVtIiwgImNvbnN1bWFibGVpdGVtIiwgImNvbnN1bWFibGVmcm9taW52ZW50b3J5aXRl
bSIsCiAgICAgICAgImZ1cm5pdHVyZWl0ZW0iLCAibW91bnRza2luIiwgImpvdXJuYWxpdGVtIiwg
ImZhcm1hYmxlaXRlbSIsCiAgICAgICAgImxhYm91cmVyY29udHJhY3QiLCAiY3J5c3RhbGxlYWd1
ZWl0ZW0iLCAiaGlkZW91dGl0ZW0iLAogICAgXQogICAgcmVzdWx0LCBza2lwcGVkID0gW10sIDAK
CiAgICBmb3IgaXR5cGUgaW4gSVRFTV9UWVBFUzoKICAgICAgICBmb3IgZW50cnkgaW4gaXRlcl9p
dGVtX3R5cGUoaXRlbXNfcm9vdCwgaXR5cGUpOgogICAgICAgICAgICB1bmlxdWVuYW1lID0gZW50
cnkuZ2V0KCJAdW5pcXVlbmFtZSIsICIiKQogICAgICAgICAgICBpZiBub3QgdW5pcXVlbmFtZTog
c2tpcHBlZCArPSAxOyBjb250aW51ZQogICAgICAgICAgICByYXdfaXAgPSBlbnRyeS5nZXQoIkBp
dGVtcG93ZXIiLCAiIikKICAgICAgICAgICAgdHJ5OiBiYXNlX2lwID0gaW50KGZsb2F0KHJhd19p
cCkpIGlmIHJhd19pcCAhPSAiIiBlbHNlIE5vbmUKICAgICAgICAgICAgZXhjZXB0OiBiYXNlX2lw
ID0gTm9uZQogICAgICAgICAgICBpZiBiYXNlX2lwIGlzIE5vbmU6IHNraXBwZWQgKz0gMTsgY29u
dGludWUKCiAgICAgICAgICAgIGNhdCwgc2xvdCA9IGVudHJ5LmdldCgiQHNob3BjYXRlZ29yeSIs
ICIiKSwgZW50cnkuZ2V0KCJAc2xvdHR5cGUiLCAiIikKICAgICAgICAgICAgcmVjICA9IHsibiI6
IHVuaXF1ZW5hbWUsICJwIjogYmFzZV9pcCwgInQiOiBpdHlwZSwgImNhdCI6IGNhdCwgInNsb3Qi
OiBzbG90fQogICAgICAgICAgICBpZiBlbnRyeS5nZXQoIkB0d29oYW5kZWQiKSA9PSAidHJ1ZSI6
IHJlY1siaDIiXSA9IFRydWUKICAgICAgICAgICAgcmVzdWx0LmFwcGVuZChyZWMpCgogICAgICAg
ICAgICBmb3IgbHZsIGluIGdldF9lbmNoYW50X2xldmVscyhlbnRyeSk6CiAgICAgICAgICAgICAg
ICBlbmMgPSB7Im4iOiBmInt1bmlxdWVuYW1lfUB7bHZsfSIsICJwIjogY2FsY19lbmNfaXAoYmFz
ZV9pcCwgbHZsLCBjZmcpLCAidCI6IGl0eXBlLCAiY2F0IjogY2F0LCAic2xvdCI6IHNsb3R9CiAg
ICAgICAgICAgICAgICBpZiBlbnRyeS5nZXQoIkB0d29oYW5kZWQiKSA9PSAidHJ1ZSI6IGVuY1si
aDIiXSA9IFRydWUKICAgICAgICAgICAgICAgIHJlc3VsdC5hcHBlbmQoZW5jKQoKICAgIHByaW50
KGYiICBJc2xlbmRpICAgOiB7bGVuKHJlc3VsdCl9IGtheWl0ICB8ICBBdGxhbmFuOiB7c2tpcHBl
ZH0iKQogICAgc2F2ZV9qc29uKHJlc3VsdCwgb3V0cHV0X3BhdGgpCiAgICByZXR1cm4gcmVzdWx0
CgoKIyArPT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PSsKIyB8ICBNb2IgdmVyaXNpIChkaWxkZW4gYmFnaW1zaXogYmF6ICsgZGlsIGJhc2luYSBpc2lt
KSAgfAojICs9PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09KwpkZWYgYnVpbGRfbW9ic19iYXNlKG1vYnNfanNvbl9wYXRoKToKICAgICIiIk1vYiB2ZXJp
c2luaSBiaXIga2VyZSBva3VyLiBJc2ltIChuKSBJQ0VSTUVaIC0gc2FkZWNlIGNvenVtbGVuZWNl
ayBoYW0gJ190YWcnIHR1dHVsdXIuIiIiCiAgICBwcmludCgiXG5bbW9iIHZlcmlzaSBva3VudXlv
ciAoZGlsZGVuIGJhZ2ltc2l6KV0iKQogICAgZGF0YSA9IGxvYWRfanNvbihtb2JzX2pzb25fcGF0
aCkKICAgIG1vYl9saXN0ID0gZGF0YS5nZXQoIk1vYnMiLCBkYXRhKS5nZXQoIk1vYiIsIFtdKQog
ICAgaWYgaXNpbnN0YW5jZShtb2JfbGlzdCwgZGljdCk6IG1vYl9saXN0ID0gW21vYl9saXN0XQog
ICAgcmVzdWx0LCBza2lwcGVkID0gW10sIDAKCiAgICBmb3IgbW9iIGluIG1vYl9saXN0OgogICAg
ICAgIHVuaXF1ZW5hbWUgPSBtb2IuZ2V0KCJAdW5pcXVlbmFtZSIsICIiKQogICAgICAgIGlmIG5v
dCB1bmlxdWVuYW1lOiBza2lwcGVkICs9IDE7IGNvbnRpbnVlCiAgICAgICAgdHJ5OgogICAgICAg
ICAgICB0aWVyID0gaW50KG1vYi5nZXQoIkB0aWVyIiwgMCkgb3IgMCkKICAgICAgICAgICAgZmFt
ZSA9IGludChmbG9hdChtb2IuZ2V0KCJAZmFtZSIsIDApIG9yIDApKQogICAgICAgICAgICBocCAg
ID0gaW50KGZsb2F0KG1vYi5nZXQoIkBoaXRwb2ludHNtYXgiLCAwKSBvciAwKSkKICAgICAgICBl
eGNlcHQ6IHNraXBwZWQgKz0gMTsgY29udGludWUKCiAgICAgICAgcmVjID0geyJ1IjogdW5pcXVl
bmFtZSwgInQiOiB0aWVyfQogICAgICAgIG1vYl9jID0gbW9iLmdldCgiQG1vYnR5cGVjYXRlZ29y
eSIsICIiKSBvciBtb2IuZ2V0KCJAY2F0ZWdvcnkiLCAiIikKICAgICAgICBpZiBtb2JfYzogcmVj
WyJjIl0gPSBtb2JfYwogICAgICAgIHJlY1siZmFtZSJdLCByZWNbImhwIl0sIHJlY1siYXZhdGFy
Il0gPSBmYW1lLCBocCwgbW9iLmdldCgiQGF2YXRhciIsICIiKQogICAgICAgIGlmIG1vYi5nZXQo
IkBkYW5nZXJzdGF0ZSIsICIiKTogcmVjWyJkYW5nZXIiXSA9IG1vYi5nZXQoIkBkYW5nZXJzdGF0
ZSIsICIiKQoKICAgICAgICBoYXJ2ZXN0YWJsZSA9IG1vYi5nZXQoIkxvb3QiLCB7fSkuZ2V0KCJI
YXJ2ZXN0YWJsZSIsIHt9KSBpZiBpc2luc3RhbmNlKG1vYi5nZXQoIkxvb3QiLCB7fSksIGRpY3Qp
IGVsc2Uge30KICAgICAgICBpZiBoYXJ2ZXN0YWJsZSBhbmQgaGFydmVzdGFibGUuZ2V0KCJAdHlw
ZSIsICIiKToKICAgICAgICAgICAgcmVjWyJsIl0gPSBoYXJ2ZXN0YWJsZS5nZXQoIkB0eXBlIiwg
IiIpCiAgICAgICAgICAgIHJlY1sibHQiXSA9IGludChoYXJ2ZXN0YWJsZS5nZXQoIkB0aWVyIiwg
dGllcikpCgogICAgICAgIHJlY1siX3RhZyJdID0gbW9iLmdldCgiQG5hbWVsb2NhdGFnIiwgIiIp
ICAjIGNldnJpbGVjZWsgaGFtIGV0aWtldCAoY2lrdGl5YSB5YXppbG1heikKICAgICAgICByZXN1
bHQuYXBwZW5kKHJlYykKCiAgICBwcmludChmIiAgSXNsZW5kaSAgIDoge2xlbihyZXN1bHQpfSBr
YXlpdCAgfCAgQXRsYW5hbjoge3NraXBwZWR9IikKICAgIHJldHVybiByZXN1bHQKCmRlZiByZXNv
bHZlX21vYl9uYW1lKHRhZywgbG9jKToKICAgICIiIgogICAgT25jZWxpZ2k6CiAgICAgIDEpIGxv
Y1t0YWddIHZhcnNhIG9udSBrdWxsYW4gKGdlcmNlayBjZXZyaWxtaXMgaXNpbSkKICAgICAgMikg
eW9rc2EgaGFtIHRhZydpIHRpdGxlLWNhc2UgeWFwYXJhayBmYWxsYmFjayB1cmV0IChUVU0gZGls
bGVyZGUgYXluaSBrYWxpcikKICAgICIiIgogICAgaWYgbm90IHRhZzogcmV0dXJuICIiCiAgICBr
ZXkgPSB0YWcubHN0cmlwKCJAIikKICAgIG5hbWUgPSBsb2MuZ2V0KGtleSwgIiIpCiAgICBpZiBu
YW1lOiByZXR1cm4gbmFtZQogICAgcmV0dXJuIGtleS5yZXBsYWNlKCJfIiwgIiAiKS50aXRsZSgp
CgpkZWYgYnVpbGRfbW9ic19taW5fZm9yX2xhbmcobW9ic19iYXNlLCBsb2MsIG91dHB1dF9wYXRo
KToKICAgIHJlc3VsdCA9IFtdCiAgICBmb3IgcmVjIGluIG1vYnNfYmFzZToKICAgICAgICBpdGVt
ID0geyJ1IjogcmVjWyJ1Il0sICJ0IjogcmVjWyJ0Il19CiAgICAgICAgaWYgImMiIGluIHJlYzog
aXRlbVsiYyJdID0gcmVjWyJjIl0KICAgICAgICAjIEhhbSBsb2thbGl6YXN5b24ga2V5J2luaSBo
ZXIgemFtYW4ga2F5ZGV0IChwYXJzZXIgQE1PQl8uLi4gZ29uZGVyaXJzZSBlc2xlc3NpbikKICAg
ICAgICByYXdfdGFnID0gcmVjLmdldCgiX3RhZyIsICIiKQogICAgICAgIGlmIHJhd190YWc6IGl0
ZW1bImsiXSA9IHJhd190YWcKICAgICAgICBuYW1lID0gcmVzb2x2ZV9tb2JfbmFtZShyYXdfdGFn
LCBsb2MpCiAgICAgICAgaWYgbmFtZTogaXRlbVsibiJdID0gbmFtZQogICAgICAgIGl0ZW1bImZh
bWUiXSwgaXRlbVsiaHAiXSwgaXRlbVsiYXZhdGFyIl0gPSByZWNbImZhbWUiXSwgcmVjWyJocCJd
LCByZWNbImF2YXRhciJdCiAgICAgICAgaWYgImRhbmdlciIgaW4gcmVjOiBpdGVtWyJkYW5nZXIi
XSA9IHJlY1siZGFuZ2VyIl0KICAgICAgICBpZiAibCIgaW4gcmVjOiBpdGVtWyJsIl0gPSByZWNb
ImwiXQogICAgICAgIGlmICJsdCIgaW4gcmVjOiBpdGVtWyJsdCJdID0gcmVjWyJsdCJdCiAgICAg
ICAgcmVzdWx0LmFwcGVuZChpdGVtKQogICAgc2F2ZV9qc29uKHJlc3VsdCwgb3V0cHV0X3BhdGgp
CiAgICByZXR1cm4gcmVzdWx0CgpkZWYgYnVpbGRfbW9ic190eHQobW9ic19taW4sIG91dHB1dF9w
YXRoLCBvZmZzZXQpOgogICAgbGluZXMgPSBbZiJbe2kgKyBvZmZzZXQgKyAxfV0gOiB7bW9iLmdl
dCgnbicsICcnKSBvciAnVW5rbm93bid9IiBmb3IgaSwgbW9iIGluIGVudW1lcmF0ZShtb2JzX21p
bildCiAgICB3cml0ZV9saW5lc19jcmxmKG91dHB1dF9wYXRoLCBsaW5lcykKCgojICs9PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09KwojIHwgIGl0ZW1z
LnR4dCAoZGlsIGJhc2luYSwgRHVtcGVyIElEICsgU3RhdCBJUCBiaXJsZXNpbWkpfAojICs9PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09KwpkZWYgYnVp
bGRfaXRlbXNfdHh0X2Zvcl9sYW5nKGR1bXBlcl9kYXRhLCBpcF9tYXAsIGxvYywgb3V0cHV0X3Bh
dGgpOgogICAgaWYgbm90IGxvYzoKICAgICAgICBwcmludChmIiAgW2l0ZW1zLnR4dF0gVVlBUkk6
IGxva2FsaXphc3lvbiB5b2ssIGF0bGFuaXlvciAtPiB7b3V0cHV0X3BhdGh9IikKICAgICAgICBy
ZXR1cm4KCiAgICBkZWYgZ2V0X2Rpc3BsYXkodW5pcXVlbmFtZSk6CiAgICAgICAgbmFtZSA9IGxv
Yy5nZXQoZiJJVEVNU197dW5pcXVlbmFtZX0iLCAiIikKICAgICAgICBpZiBuYW1lOiByZXR1cm4g
bmFtZQogICAgICAgIGlmICJAIiBpbiB1bmlxdWVuYW1lOgogICAgICAgICAgICBiYXNlID0gdW5p
cXVlbmFtZS5yc3BsaXQoIkAiLCAxKVswXQogICAgICAgICAgICBuYW1lID0gbG9jLmdldChmIklU
RU1TX3tiYXNlfSIsICIiKQogICAgICAgICAgICBpZiBuYW1lOiByZXR1cm4gbmFtZQogICAgICAg
IHJldHVybiAiIgoKICAgIG91dF9saW5lcyA9IFtdCiAgICBub19uYW1lID0gMAogICAgZm9yIGl0
ZW0gaW4gZHVtcGVyX2RhdGE6CiAgICAgICAgaWR4X3N0ciA9IHN0cihpdGVtLmdldCgiSW5kZXgi
LCAiIikpCiAgICAgICAgdW5pcXVlbmFtZSA9IGl0ZW0uZ2V0KCJVbmlxdWVOYW1lIiwgIiIpLnN0
cmlwKCkKICAgICAgICBpZiBub3QgaWR4X3N0ciBvciBub3QgdW5pcXVlbmFtZToKICAgICAgICAg
ICAgY29udGludWUKCiAgICAgICAgZGlzcGxheSA9IGdldF9kaXNwbGF5KHVuaXF1ZW5hbWUpLnN0
cmlwKCkKICAgICAgICBpZiBub3QgZGlzcGxheTogbm9fbmFtZSArPSAxCgogICAgICAgIGlwID0g
aXBfbWFwLmdldCh1bmlxdWVuYW1lLCAwKQogICAgICAgIGlmIGlwID4gMDogb3V0X2xpbmVzLmFw
cGVuZChmIntpZHhfc3RyfTp7dW5pcXVlbmFtZX06e2Rpc3BsYXl9OntpcH0iKQogICAgICAgIGVs
c2U6IG91dF9saW5lcy5hcHBlbmQoZiJ7aWR4X3N0cn06e3VuaXF1ZW5hbWV9OntkaXNwbGF5fSIp
CgogICAgd3JpdGVfbGluZXNfY3JsZihvdXRwdXRfcGF0aCwgb3V0X2xpbmVzKQogICAgcHJpbnQo
ZiIgIElzaW0gYnVsdW5hbWF5YW46IHtub19uYW1lfSIpCgoKIyArPT09PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PSsKIyB8ICBTcGVsbHMgKHNwZWxscy5q
c29uIHVyZXRpbWkpICAgICAgICAgICAgICAgICAgICAgICAgfAojICs9PT09PT09PT09PT09PT09
PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09KwpkZWYgYnVpbGRfc3BlbGxzKHNw
ZWxsc19qc29uX3BhdGgsIG91dHB1dF9wYXRoKToKICAgIHByaW50KCJcbltzcGVsbHMubWluLmpz
b24gdXJldGlsaXlvcl0iKQogICAgaWYgbm90IG9zLnBhdGguZXhpc3RzKHNwZWxsc19qc29uX3Bh
dGgpOgogICAgICAgIHByaW50KGYiICBbQVRMQU5ESV0gJ3tzcGVsbHNfanNvbl9wYXRofScgYnVs
dW5hbWFkaS4iKQogICAgICAgIHJldHVybgoKICAgIGRhdGEgPSBsb2FkX2pzb24oc3BlbGxzX2pz
b25fcGF0aCkKICAgIHNwZWxsc19yb290ID0gZGF0YS5nZXQoInNwZWxscyIsIGRhdGEpCiAgICBT
UEVMTF9UWVBFUyA9IFsiYWN0aXZlc3BlbGwiLCAicGFzc2l2ZXNwZWxsIiwgInRvZ2dsZXNwZWxs
Il0KICAgIHJlc3VsdCA9IFtdCgogICAgZm9yIHN0eXBlIGluIFNQRUxMX1RZUEVTOgogICAgICAg
IGVudHJpZXMgPSBzcGVsbHNfcm9vdC5nZXQoc3R5cGUsIFtdKQogICAgICAgIGlmIGlzaW5zdGFu
Y2UoZW50cmllcywgZGljdCk6IGVudHJpZXMgPSBbZW50cmllc10KICAgICAgICBmb3IgZW50cnkg
aW4gZW50cmllczoKICAgICAgICAgICAgdW5pcXVlbmFtZSA9IGVudHJ5LmdldCgiQHVuaXF1ZW5h
bWUiLCAiIikKICAgICAgICAgICAgaWYgbm90IHVuaXF1ZW5hbWU6IGNvbnRpbnVlCiAgICAgICAg
ICAgIHJlYyA9IHsibiI6IHVuaXF1ZW5hbWUsICJ0Ijogc3R5cGV9CiAgICAgICAgICAgIGluY29y
cG9yYXRlID0gZW50cnkuZ2V0KCJAaW5jb3Jwb3JhdGVzcGVsbCIsICIiKQogICAgICAgICAgICBp
ZiBpbmNvcnBvcmF0ZTogcmVjWyJpIl0gPSBpbmNvcnBvcmF0ZQogICAgICAgICAgICByZXN1bHQu
YXBwZW5kKHJlYykKCiAgICBzYXZlX2pzb24ocmVzdWx0LCBvdXRwdXRfcGF0aCkKICAgIHByaW50
KGYiICBJc2xlbmRpOiB7bGVuKHJlc3VsdCl9IHNwZWxsIikKICAgIHJldHVybiByZXN1bHQKCgoj
ICs9PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09Kwoj
IHwgIFpvbmVzICh6b25lcy5qc29uIHVyZXRpbWkgLyBndW5jZWxsZW1lKSAgICAgICAgICAgICB8
CiMgKz09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0r
CmRlZiBfcGFyc2VfdGllcl9mcm9tX3VuaXF1ZW5hbWUodW5hbWUpOgogICAgIiIiVW5pcXVlTmFt
ZSdkZW4gdGllciBjaWthcnQ6ICcwMDAwX0NUWV9TV19BVVRPX1Q1X05PTicgLT4gNSIiIgogICAg
aW1wb3J0IHJlCiAgICBtID0gcmUuc2VhcmNoKHInX1QoXGQrKV8nLCB1bmFtZSkKICAgIHJldHVy
biBpbnQobS5ncm91cCgxKSkgaWYgbSBlbHNlIDEKCmRlZiBfcGFyc2VfcHZwdHlwZV9mcm9tX3Vu
aXF1ZW5hbWUodW5hbWUpOgogICAgdW5hbWVfdXAgPSB1bmFtZS51cHBlcigpCiAgICBpZiAiX0JM
XyIgaW4gdW5hbWVfdXAgb3IgIl9IQl8iIGluIHVuYW1lX3VwOiByZXR1cm4gImJsYWNrIgogICAg
aWYgIl9ITF8iIGluIHVuYW1lX3VwOiByZXR1cm4gInJlZCIKICAgIGlmICJfWUxfIiBpbiB1bmFt
ZV91cDogcmV0dXJuICJ5ZWxsb3ciCiAgICByZXR1cm4gInNhZmUiCgpkZWYgYnVpbGRfem9uZXMo
d29ybGRfanNvbl9wYXRoLCB6b25lc19iYXNlX3BhdGgsIG91dHB1dF9wYXRoKToKICAgIHByaW50
KCJcblt6b25lcy5qc29uIGd1bmNlbGxlbml5b3JdIikKCiAgICAjIC0tLSBEVVpFTFRNRTogZ2ly
ZGlsZXJpIGFkYXkga2xhc29ybGVyZGUgYXJhIChlc2tpZGVuIHNhZGVjZSBrZW5kaSBrbGFzb3J1
bmUgYmFraXlvcmR1KSAtLS0KICAgIHJlYWxfd29ybGQgPSByZXNvbHZlX2lucHV0KG9zLnBhdGgu
YmFzZW5hbWUod29ybGRfanNvbl9wYXRoKSkKICAgIHJlYWxfYmFzZSA9IE5vbmUgICAjIE9OQ0VM
SUs6IHV5Z3VsYW1hbmluIGNhbmxpIGRvc3lhc2kgLT4gdXBkYXRlciBjaWt0aXNpIC0+IHZlcmls
ZW4gYmF6IC0+IEM6XG91dHB1dAogICAgZm9yIGNhbmQgaW4gKG9zLnBhdGguam9pbigiLi4iLCAi
Li4iLCAiTmlnaHR3YXRjaCIsICJBc3NldHMiLCAiSGVscGVyIiwgInpvbmVzLmpzb24iKSwKICAg
ICAgICAgICAgICAgICBvcy5wYXRoLmpvaW4oIkhlbHBlciIsICJ6b25lcy5qc29uIiksCiAgICAg
ICAgICAgICAgICAgb3MucGF0aC5iYXNlbmFtZSh6b25lc19iYXNlX3BhdGgpLAogICAgICAgICAg
ICAgICAgIHIiQzpcb3V0cHV0XHpvbmVzLmpzb24iKToKICAgICAgICBpZiBjYW5kIGFuZCBvcy5w
YXRoLmV4aXN0cyhjYW5kKToKICAgICAgICAgICAgcmVhbF9iYXNlID0gY2FuZAogICAgICAgICAg
ICBicmVhawogICAgIyBCYXogZG9zeWEgaWNpbiBheXJpY2EgZXNraSBjaWt0aXlpIGRhIGRlbmUg
KEhlbHBlclx6b25lcy5qc29uKQogICAgaWYgbm90IHJlYWxfYmFzZToKICAgICAgICByZWFsX2Jh
c2UgPSByZXNvbHZlX2lucHV0KCJ6b25lcy5qc29uIikKCiAgICAjIEJheiB6b25lcyBkb3N5YXNp
bmkgeXVrbGUgKG1ldmN1dCB6b25lcy5qc29uKSAtPiBFU0tJIFZFUkkgS09SVU5VUgogICAgYmFz
ZSA9IHt9CiAgICBpZiByZWFsX2Jhc2U6CiAgICAgICAgYmFzZSA9IGxvYWRfanNvbihyZWFsX2Jh
c2UpCiAgICAgICAgcHJpbnQoZiIgIEJheiBkb3N5YSA6IHtyZWFsX2Jhc2V9ICh7bGVuKGJhc2Up
fSB6b25lKSIpCiAgICBlbHNlOgogICAgICAgIHByaW50KCIgIFtVWUFSSV0gQmF6IHpvbmVzIGRv
c3lhc2kgaGljYmlyIGFkYXkgeW9sZGEgYnVsdW5hbWFkaSwgYm9zIGJhc2xhbml5b3IuIikKICAg
ICAgICBwcmludCgiICAgICAgICAgIChCdSBkdXJ1bWRhIGVza2kgem9uZSBiaWxnaWxlcmkgS0FZ
Qk9MVVI7IHV5Z3VsYW1hbmluIikKICAgICAgICBwcmludCgiICAgICAgICAgICBOaWdodHdhdGNo
XFxBc3NldHNcXEhlbHBlclxcem9uZXMuanNvbiBkb3N5YXNpbmkgYmF6IG9sYXJhayBrb3l1bi4p
IikKCiAgICAjIHdvcmxkLmpzb24geXVrbGUKICAgIGlmIG5vdCByZWFsX3dvcmxkOgogICAgICAg
IHByaW50KGYiICBbQVRMQU5ESV0gJ3tvcy5wYXRoLmJhc2VuYW1lKHdvcmxkX2pzb25fcGF0aCl9
JyBoaWNiaXIgYWRheSB5b2xkYSBidWx1bmFtYWRpLiIpCiAgICAgICAgcHJpbnQoZiIgICAgICAg
ICAgQXJhbmFuIGtsYXNvcmxlcjogeycsICcuam9pbihTRUFSQ0hfRElSUyl9IikKICAgICAgICBp
ZiBiYXNlOgogICAgICAgICAgICBzYXZlX2pzb24oYmFzZSwgb3V0cHV0X3BhdGgpCiAgICAgICAg
ICAgIHByaW50KCIgIChCYXogZG9zeWEgeWluZSBkZSB5YXppbGRpIC0+IGVza2kgdmVyaSBrb3J1
bmR1LikiKQogICAgICAgIHJldHVybgoKICAgIHByaW50KGYiICBXb3JsZCAgICAgOiB7cmVhbF93
b3JsZH0iKQogICAgd29ybGQgPSBsb2FkX2pzb24ocmVhbF93b3JsZCkKICAgIGFkZGVkID0gMAog
ICAgZm9yIGVudHJ5IGluIHdvcmxkOgogICAgICAgIGlkeCAgICAgICAgPSBzdHIoZW50cnkuZ2V0
KCJJbmRleCIsICIiKSBvciAiIikuc3RyaXAoKQogICAgICAgIHVuaXF1ZW5hbWUgPSBzdHIoZW50
cnkuZ2V0KCJVbmlxdWVOYW1lIiwgIiIpIG9yICIiKS5zdHJpcCgpCiAgICAgICAgaWYgbm90IGlk
eCBvciBpZHggPT0gIkRlYnVnIjogY29udGludWUKICAgICAgICBpZiBpZHggaW4gYmFzZTogY29u
dGludWUgICMgWmF0ZW4gdmFyLCBla2xlbWUKCiAgICAgICAgIyBZZW5pIHpvbmU6IG1pbmltYWwg
YmlsZ2l5bGUgZWtsZQogICAgICAgIHRpZXIgICAgPSBfcGFyc2VfdGllcl9mcm9tX3VuaXF1ZW5h
bWUodW5pcXVlbmFtZSkKICAgICAgICBwdnB0eXBlID0gX3BhcnNlX3B2cHR5cGVfZnJvbV91bmlx
dWVuYW1lKHVuaXF1ZW5hbWUpCiAgICAgICAgYmFzZVtpZHhdID0gewogICAgICAgICAgICAibmFt
ZSI6ICAgIGlkeCwgICAgICAgICAgICMgR2VyY2VrIGlzaW0gYmlsaW5lbWV6LCBpZHgga3VsbGFu
CiAgICAgICAgICAgICJ0eXBlIjogICAgIiIsICAgICAgICAgICAgIyBEdW1wZXInZGFuIGdlbG1l
egogICAgICAgICAgICAicHZwVHlwZSI6IHB2cHR5cGUsCiAgICAgICAgICAgICJ0aWVyIjogICAg
dGllciwKICAgICAgICAgICAgImZpbGUiOiAgICB1bmlxdWVuYW1lLAogICAgICAgICAgICAiYm91
bmRzIjogIHsibWluIjogWzAsIDBdLCAibWF4IjogWzAsIDBdfQogICAgICAgIH0KICAgICAgICBh
ZGRlZCArPSAxCgogICAgcHJpbnQoZiIgIFllbmkgZWtsZW5lbjoge2FkZGVkfSB6b25lIHwgVG9w
bGFtOiB7bGVuKGJhc2UpfSB6b25lIikKICAgIHNhdmVfanNvbihiYXNlLCBvdXRwdXRfcGF0aCkK
CgoKIyArPT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PSsKIyB8ICBBTkEgQUtJUyAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg
ICAgfAojICs9PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09
PT09KwpkZWYgbWFpbigpOgogICAgcHJpbnQoIj0iICogNjApCiAgICBwcmludCgiICAgQWxiaW9u
IE9ubGluZSBKU09OIERvbnVzdHVydWN1IChDb2sgRGlsbGkgTmloYWkgQmlybGVzdGlyaWNpKSIp
CiAgICBwcmludCgiPSIgKiA2MCkKICAgIHByaW50KGYiICBEaXppbiA6IHtvcy5nZXRjd2QoKX0i
KQoKICAgIG91dHB1dF9kaXIgPSBDT05GSUcuZ2V0KCJvdXRwdXRfZGlyIiwgIiIpLnN0cmlwKCkK
ICAgIGlmIG91dHB1dF9kaXI6IHByaW50KGYiICBDaWt0aSA6IHtvcy5wYXRoLmFic3BhdGgob3V0
cHV0X2Rpcil9LyIpCgogICAgbGFuZ3VhZ2VzID0gcmVzb2x2ZV9sYW5ndWFnZXMoQ09ORklHKQog
ICAgaWYgbm90IGxhbmd1YWdlczoKICAgICAgICBwcmludCgiXG5bSEFUQV0gSXNsZW5lY2VrIGRp
bCBidWx1bmFtYWRpLCBjaWtpbGl5b3IuIikKICAgICAgICBzeXMuZXhpdCgxKQogICAgcHJpbnQo
ZiIgIERpbGxlcjoge1tmJ3tzfSAoe3R9KScgZm9yIHQsIHMgaW4gbGFuZ3VhZ2VzXX0iKQoKICAg
ICMgMCkgR0lSREkgQ09aVU1MRU1FICgwOC4xMC4yMDI2IC0gVEFNIE9UT01BVElLIElDSU4pCiAg
ICAjICAgIEVza2lkZW4gYnUgZG9zeWFsYXIgU0FERUNFIHVwZGF0ZXIga2xhc29ydW5kZSBhcmFu
aXlvcmR1OyBkdW1wZXIgY2lrdGlsYXJpCiAgICAjICAgIEM6XG91dHB1dCBhbHRpbmRhIG9sZHVn
dSBpY2luIGVza2kga29weWFsYXIga3VsbGFuaWxpeW9yZHUuCiAgICAjICAgIEFydGlrIG9uY2Ug
Qzpcb3V0cHV0J3Rha2kgR1VOQ0VMIGRvc3lhbGFyIGJ1bHVudXIsIHlva3NhIHllcmVsIGtvcHlh
eWEgZHVzdWx1ci4KICAgIGRlZiBfZ2lyZGkoYW5haHRhciwgYWRheWxhcik6CiAgICAgICAgcCA9
IHJlc29sdmVfaW5wdXQob3MucGF0aC5iYXNlbmFtZShDT05GSUdbYW5haHRhcl0pLCBhZGF5bGFy
KQogICAgICAgIGlmIHA6CiAgICAgICAgICAgIHByaW50KGYiICBbZ2lyZGldIHthbmFodGFyOjwy
MH0gLT4ge3B9IikKICAgICAgICAgICAgQ09ORklHW2FuYWh0YXJdID0gcAogICAgICAgIGVsc2U6
CiAgICAgICAgICAgIHByaW50KGYiICBbZ2lyZGldIHthbmFodGFyOjwyMH0gLT4gKGJ1bHVuYW1h
ZGksIHllcmVsOiB7Q09ORklHW2FuYWh0YXJdfSkiKQoKICAgIHByaW50KCJcbltHaXJkaSBkb3N5
YWxhcmkgY296dWx1eW9yXSIpCiAgICBfZ2lyZGkoIml0ZW1zX3N0YXRzX2pzb24iLCAgW3IiQzpc
b3V0cHV0XGl0ZW1zLmpzb24iXSkKICAgIF9naXJkaSgiaXRlbXNfZHVtcGVyX2pzb24iLCBbciJD
OlxvdXRwdXRcZm9ybWF0dGVkXGl0ZW1zLmpzb24iXSkKICAgIF9naXJkaSgibW9ic19qc29uIiwg
ICAgICAgICBbciJDOlxvdXRwdXRcbW9icy5qc29uIl0pCiAgICBfZ2lyZGkoImxvY2FsaXphdGlv
bl9qc29uIiwgW3IiQzpcb3V0cHV0XGxvY2FsaXphdGlvbi5qc29uIl0pCiAgICBfZ2lyZGkoInNw
ZWxsc19qc29uIiwgICAgICAgW3IiQzpcb3V0cHV0XHNwZWxscy5qc29uIl0pCgogICAgIyAxKSBE
aWxkZW4gYmFnaW1zaXogdmVyaWxlciAtIEhFUiBCSVJJIFRFSyBTRUZFUiBva3VudXIKICAgIGl0
ZW1zX21pbiwgaXBfbWFwID0gW10sIHt9CiAgICBpZiBDT05GSUdbImdlbmVyYXRlX2l0ZW1zIl06
CiAgICAgICAgaXRlbXNfbWluID0gYnVpbGRfaXRlbXNfbWluKENPTkZJR1siaXRlbXNfc3RhdHNf
anNvbiJdLCBvdXQoQ09ORklHWyJpdGVtc19taW5fanNvbiJdKSwgQ09ORklHKQogICAgICAgIGlw
X21hcCA9IHtpdFsibiJdOiBpdC5nZXQoInAiLCAwKSBmb3IgaXQgaW4gaXRlbXNfbWlufQoKICAg
IG1vYnNfYmFzZSA9IFtdCiAgICBpZiBDT05GSUdbImdlbmVyYXRlX21vYnMiXToKICAgICAgICBt
b2JzX2Jhc2UgPSBidWlsZF9tb2JzX2Jhc2UoQ09ORklHWyJtb2JzX2pzb24iXSkKCiAgICBkdW1w
ZXJfZGF0YSA9IE5vbmUKICAgIGlmIENPTkZJR1siZ2VuZXJhdGVfaXRlbXNfdHh0Il06CiAgICAg
ICAgZHVtcGVyX3BhdGggPSBDT05GSUdbIml0ZW1zX2R1bXBlcl9qc29uIl0KICAgICAgICBpZiBu
b3Qgb3MucGF0aC5leGlzdHMoZHVtcGVyX3BhdGgpOgogICAgICAgICAgICBwcmludChmIlxuW0tS
SVRJSyBIQVRBXSAne2R1bXBlcl9wYXRofScgYnVsdW5hbWFkaSEgRHVtcGVyJ2luIHVyZXR0aWdp
ICIKICAgICAgICAgICAgICAgICAgZiJpdGVtcy5qc29uJ2kgYnUgaXNpbWxlIHlhbmluYSBrb3kg
bXEuIikKICAgICAgICBlbHNlOgogICAgICAgICAgICBwcmludCgiXG5bRHVtcGVyIHZlcmlzaSBv
a3VudXlvciAoZGlsZGVuIGJhZ2ltc2l6LCB0ZWsgc2VmZXJsaWspXSIpCiAgICAgICAgICAgIGR1
bXBlcl9kYXRhID0gbG9hZF9qc29uKGR1bXBlcl9wYXRoKQoKICAgICMgMikgTG9rYWxpemFzeW9u
IC0gZWtzaWsgZGlsbGVyIGljaW4gVEVLIEdFQ0lTVEUgdXJldGlsaXIKICAgIGxvY19ieV9sYW5n
ID0gbG9hZF9vcl9idWlsZF9sb2NhbGl6YXRpb25fbXVsdGkoQ09ORklHLCBsYW5ndWFnZXMpCgog
ICAgIyAzKSBEaWwgYmFzaW5hIGNpa3RpbGFyCiAgICBmb3IgdG14X2NvZGUsIHN1ZmZpeCBpbiBs
YW5ndWFnZXM6CiAgICAgICAgcHJpbnQoZiJcbnsnLScgKiA2MH1cbiAgPj4gRGlsIGlzbGVuaXlv
cjoge3N1ZmZpeH0gIChUTVg6IHt0bXhfY29kZX0pXG57Jy0nICogNjB9IikKICAgICAgICBsb2Mg
PSBsb2NfYnlfbGFuZy5nZXQoc3VmZml4LCB7fSkKICAgICAgICBpZiBub3QgbG9jOgogICAgICAg
ICAgICBwcmludChmIiAgW1VZQVJJXSAne3N1ZmZpeH0nIGljaW4gbG9rYWxpemFzeW9uIHZlcmlz
aSB5b2svYm9zLiIpCgogICAgICAgIG1vYnNfbWluX2xhbmcgPSBbXQogICAgICAgIGlmIENPTkZJ
R1siZ2VuZXJhdGVfbW9icyJdIGFuZCBtb2JzX2Jhc2U6CiAgICAgICAgICAgIG1vYnNfcGF0aCA9
IG91dChDT05GSUdbIm1vYnNfbWluX3BhdHRlcm4iXS5mb3JtYXQoU1VGRklYPXN1ZmZpeCkpCiAg
ICAgICAgICAgIG1vYnNfbWluX2xhbmcgPSBidWlsZF9tb2JzX21pbl9mb3JfbGFuZyhtb2JzX2Jh
c2UsIGxvYywgbW9ic19wYXRoKQoKICAgICAgICBpZiBDT05GSUdbImdlbmVyYXRlX2l0ZW1zX3R4
dCJdIGFuZCBkdW1wZXJfZGF0YSBpcyBub3QgTm9uZToKICAgICAgICAgICAgaXRlbXNfdHh0X3Bh
dGggPSBvdXQoQ09ORklHWyJpdGVtc190eHRfcGF0dGVybiJdLmZvcm1hdChTVUZGSVg9c3VmZml4
KSkKICAgICAgICAgICAgYnVpbGRfaXRlbXNfdHh0X2Zvcl9sYW5nKGR1bXBlcl9kYXRhLCBpcF9t
YXAsIGxvYywgaXRlbXNfdHh0X3BhdGgpCgogICAgICAgIGlmIENPTkZJR1siZ2VuZXJhdGVfbW9i
c190eHQiXSBhbmQgbW9ic19taW5fbGFuZzoKICAgICAgICAgICAgbW9ic2lkX3BhdGggPSBvdXQo
Q09ORklHWyJtb2JzaWRfdHh0X3BhdHRlcm4iXS5mb3JtYXQoU1VGRklYPXN1ZmZpeCkpCiAgICAg
ICAgICAgIGJ1aWxkX21vYnNfdHh0KG1vYnNfbWluX2xhbmcsIG1vYnNpZF9wYXRoLCBDT05GSUdb
Im1vYnNfaW5kZXhfb2Zmc2V0Il0pCgogICAgIyA0KSBTcGVsbHMgKGRpbGRlbiBiYWdpbXNpeikK
ICAgIGlmIENPTkZJR1siZ2VuZXJhdGVfc3BlbGxzIl06CiAgICAgICAgc3BlbGxzX3BhdGggPSBv
dXQoQ09ORklHWyJzcGVsbHNfb3V0cHV0Il0pCiAgICAgICAgYnVpbGRfc3BlbGxzKENPTkZJR1si
c3BlbGxzX2pzb24iXSwgc3BlbGxzX3BhdGgpCgogICAgIyA1KSBab25lcyAoZGlsZGVuIGJhZ2lt
c2l6LCBiYXogKyB3b3JsZC5qc29uIGJpcmxlc3RpcikKICAgIGlmIENPTkZJR1siZ2VuZXJhdGVf
em9uZXMiXToKICAgICAgICB6b25lc19vdXQgPSBvdXQoQ09ORklHWyJ6b25lc19vdXRwdXQiXSkK
ICAgICAgICBidWlsZF96b25lcyhDT05GSUdbIndvcmxkX2pzb24iXSwgQ09ORklHWyJ6b25lc19i
YXNlX2pzb24iXSwgem9uZXNfb3V0KQoKICAgIHByaW50KCJcbiIgKyAiPSIgKiA2MCkKICAgIHBy
aW50KCIgICBUYW1hbWxhbmRpISIpCiAgICBwcmludCgiPSIgKiA2MCkKCmlmIF9fbmFtZV9fID09
ICJfX21haW5fXyI6CiAgICBtYWluKCk=
:::B64:UPDATER:END
"""


if __name__ == "__main__":
    sys.exit(main())
