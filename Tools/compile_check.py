"""Offline compile check for PG.* assemblies and EditMode test sources while Unity is open.

Reuses Unity's generated .rsp files (Library/Bee), swaps the source list for the current *.cs
files of each asmdef folder and chains fresh reference assemblies into a temp dir. The EditMode
test assembly is compiled but never executed.
Usage: python Tools/compile_check.py   (exit code 1 on errors)
"""
import glob, os, re, subprocess, sys, tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNITY = r"C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Data"
ORDER = ["Core", "Content", "World", "WorldGen", "Sim", "Powers", "Persistence", "Render", "Governor", "UI", "Boot", "Editor"]
# PG.Governor is new; until Unity imports its asmdef the Bee artifacts lack its rsp and we
# synthesize one from the PG.UI rsp (a reference superset of what PG.Governor needs).
SYNTHESIZED = {"Governor": "UI"}


def sources(asm):
    base = os.path.join(ROOT, "Assets", "_Project", asm)
    skip = [os.path.dirname(p) for p in glob.glob(os.path.join(base, "**", "*.asmdef"), recursive=True)
            if os.path.dirname(p) != base]
    out = []
    for p in glob.glob(os.path.join(base, "**", "*.cs"), recursive=True):
        if any(p.startswith(s + os.sep) for s in skip):
            continue
        out.append('"' + os.path.relpath(p, ROOT).replace("\\", "/") + '"')
    return out


def compile_editmode_tests(dag, tmp):
    """Compile the current EditMode test sources without executing Unity tests."""
    asm = "Tests.EditMode"
    rsp_path = os.path.join(dag, f"PG.{asm}.rsp")
    lines = open(rsp_path, encoding="utf-8").read().splitlines()
    out = []
    for ln in lines:
        if ln.startswith('"Assets/'):
            continue
        if ln.startswith("-out:"):
            ln = f'-out:"{tmp}/PG.{asm}.dll"'
        elif ln.startswith("-refout:"):
            ln = f'-refout:"{tmp}/PG.{asm}.ref.dll"'
        else:
            m = re.match(r'-r:".*/(PG\.\w+)\.ref\.dll"', ln)
            if m:
                ln = f'-r:"{tmp}/{m.group(1)}.ref.dll"'
        out.append(ln)
    test_root = os.path.join(ROOT, "Assets", "_Project", "Tests", "EditMode")
    out.extend('"' + os.path.relpath(p, ROOT).replace("\\", "/") + '"'
               for p in glob.glob(os.path.join(test_root, "**", "*.cs"), recursive=True))
    tr = os.path.join(tmp, f"PG.{asm}.rsp")
    open(tr, "w", encoding="utf-8").write("\n".join(out))
    unity = os.path.join(UNITY, "netcorerun", "netcorerun.exe")
    csc = os.path.join(UNITY, "DotNetSdkRoslyn", "csc.dll")
    result = subprocess.run([unity, csc, "/nologo", "@" + tr], cwd=ROOT, capture_output=True, text=True)
    errors = [line for line in result.stdout.splitlines() if ": error " in line]
    print(f"PG.{asm}: {'OK' if result.returncode == 0 else 'FAIL'} ({len(errors)} errors)")
    for error in errors[:40]:
        print("   ", error)
    if result.returncode != 0 and not errors:
        print(result.stdout[-2000:], result.stderr[-2000:])
    return result.returncode == 0


def main():
    dags = glob.glob(os.path.join(ROOT, "Library", "Bee", "artifacts", "*.dag"))
    required = [asm for asm in ORDER if asm not in SYNTHESIZED] + ["Tests.EditMode"]
    complete = [d for d in dags if all(os.path.exists(os.path.join(d, f"PG.{asm}.rsp")) for asm in required)]
    if not complete:
        raise FileNotFoundError("No Unity Bee artifact directory contains response files for every PG assembly")
    dag = max(complete, key=lambda d: os.path.getmtime(os.path.join(d, "PG.Sim.rsp")))
    tmp = os.path.join(tempfile.gettempdir(), "pg_compile_check")
    os.makedirs(tmp, exist_ok=True)
    failed = False
    for asm in ORDER:
        if asm in SYNTHESIZED and not os.path.exists(os.path.join(dag, f"PG.{asm}.rsp")):
            print(f"PG.{asm}: SYNTH (no Bee rsp yet; template {SYNTHESIZED[asm]})")
            lines = open(os.path.join(dag, f"PG.{SYNTHESIZED[asm]}.rsp"), encoding="utf-8").read().splitlines()
        else:
            lines = open(os.path.join(dag, f"PG.{asm}.rsp"), encoding="utf-8").read().splitlines()
            # A Bee rsp predating the asmdef lacks asmdef edits (e.g. new references) and
            # reports false CS0012-style failures; warn instead of confusing.
            ad = os.path.join(ROOT, "Assets", "_Project", asm, f"PG.{asm}.asmdef")
            if os.path.exists(ad) and os.path.getmtime(os.path.join(dag, f"PG.{asm}.rsp")) < os.path.getmtime(ad):
                print(f"PG.{asm}: WARNING Bee rsp predates the asmdef; asmdef edits are not reflected in this run")
        out = []
        for ln in lines:
            if ln.startswith('"Assets/'):
                continue
            if ln.startswith("-out:"):
                ln = f'-out:"{tmp}/PG.{asm}.dll"'
            elif ln.startswith("-refout:"):
                ln = f'-refout:"{tmp}/PG.{asm}.ref.dll"'
            else:
                m = re.match(r'-r:".*/(PG\.\w+)\.ref\.dll"', ln)
                if m:
                    ln = f'-r:"{tmp}/{m.group(1)}.ref.dll"'
            out.append(ln)
        out += sources(asm)
        # Assemblies compiled before the governor existed may now reference it (UI, Boot, tests).
        gov_ref = f'-r:"{tmp}/PG.Governor.ref.dll"'
        if asm != "Governor" and os.path.exists(os.path.join(tmp, "PG.Governor.ref.dll")) and gov_ref not in out:
            out.append(gov_ref)
        tr = os.path.join(tmp, f"PG.{asm}.rsp")
        open(tr, "w", encoding="utf-8").write("\n".join(out))
        r = subprocess.run([os.path.join(UNITY, "netcorerun", "netcorerun.exe"),
                            os.path.join(UNITY, "DotNetSdkRoslyn", "csc.dll"), "/nologo", "@" + tr],
                           cwd=ROOT, capture_output=True, text=True)
        errors = [l for l in r.stdout.splitlines() if ": error " in l]
        print(f"PG.{asm}: {'OK' if r.returncode == 0 else 'FAIL'} ({len(errors)} errors)")
        for e in errors[:40]:
            print("   ", e)
        if r.returncode != 0:
            failed = True
            if not errors:
                print(r.stdout[-2000:], r.stderr[-2000:])
            break
    if not failed:
        failed = not compile_editmode_tests(dag, tmp)
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
