import re, sys

peace = open(r"C:\Program Files\EqualizerAPO\config\6.peace", encoding="utf-8-sig").read().splitlines()
src = open(r"D:\AHK\调音\src\PeaceWpf\TutorialCalibrationDemo.cs", encoding="utf-8").read()

def section(text_lines, name):
    out, on = [], False
    for ln in text_lines:
        s = ln.strip()
        if s == f"[{name}]":
            on = True; continue
        if on and s.startswith("["):
            break
        if on and "=" in s:
            out.append(float(s.split("=", 1)[1]))
    return out

freqs = section(peace, "Frequencies")
gains = section(peace, "Gains")
quals = section(peace, "Qualities")
filters = section(peace, "Filters")
print("6.peace: %d bands, [Filters] entries: %d" % (len(freqs), len(filters)))
print("counts match:", len(freqs) == len(gains) == len(quals))

def array(name):
    m = re.search(r"private static readonly double\[\] " + name + r"\s*=\s*\{(.*?)\};", src, re.S)
    if not m: sys.exit(f"array {name} not found")
    return [float(x) for x in re.findall(r"-?\d+(?:\.\d+)?", m.group(1))]

src_f, src_g, src_q = array("Frequencies"), array("Gains"), array("Qualities")
for label, a, b in (("freq", freqs, src_f), ("gain", gains, src_g), ("Q", quals, src_q)):
    same = len(a) == len(b) and all(abs(x - y) < 1e-9 for x, y in zip(a, b))
    print(f"{label:5}: source {len(b)} vs preset {len(a)} -> {'OK' if same else 'MISMATCH'}")
    if not same:
        for i, (x, y) in enumerate(zip(a, b)):
            if abs(x - y) >= 1e-9:
                print(f"   idx {i}: preset {x} != source {y}")
print("peaking type used:", "EqLimits.PeakingFilter" in src, "| fitter removed:", "EqCurveFitter" not in src)
