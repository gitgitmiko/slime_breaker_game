"""Generates the raw UI art sheets with gflow-cli (Google Flow, Nano Banana).

Usage (one job at a time; the Flow profile cannot run two generations in parallel):
  python3 asset_work/scripts/gflow_ui_queue.py [job ...]      # no job = every job that has no output yet

Raw sheets land in asset_work/ui_src/raw/<job>/ and are committed, so build_ui_modern.py can cut the
sprites again without regenerating. Needs a signed-in gflow profile (`gflow auth status`) and xvfb-run.
"""
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RAW = os.path.join(ROOT, "asset_work", "ui_src", "raw")
GFLOW = os.environ.get("GFLOW", os.path.expanduser("~/github-repos/gflow-cli/.venv/bin/gflow"))
PREVIEWS = os.path.join(ROOT, "asset_work", "previews")

STYLE = (
    "Game UI icon art for a cute casual fantasy mobile game, in the polished style of modern top mobile games "
    "(Brawl Stars, Cookie Run Kingdom): chunky rounded cartoon shapes, a thick dark violet outline around every "
    "object, glossy candy shading with soft specular highlights, vibrant saturated colors. "
    "Flat solid pure bright green (#00FF00) chroma key background, no shadows on the background, no glow, "
    "no text, no letters, no frames or tiles behind the objects. "
)
GRID = "The objects are arranged in a neat grid of {cols} columns and {rows} rows, all the same size, each fully separated from its neighbours by wide gaps, front view. "

JOBS = {
    "logo": dict(aspect="16:9", n=4, prompt=(
        "Mobile game logo wordmark that reads exactly \"Dumpling Witch\" on two lines (\"Dumpling\" above \"Witch\"). "
        "Chunky bubbly cartoon letters with a cream-to-warm-gold gradient face, a thick dark violet outline and a royal purple "
        "3D extrude underneath, glossy highlights. A purple witch hat with a gold band sits tilted on the letter D, and a cute "
        "smiling white steamed dumpling with rosy cheeks peeks from behind the word Witch, a few gold sparkles. "
        "Style of modern top mobile game logos (Brawl Stars, Cookie Run Kingdom). Flat solid pure bright green (#00FF00) "
        "chroma key background, no shadow on the background, centered, large margins.")),
    "skills": dict(aspect="16:9", n=2, prompt=STYLE + GRID.format(cols=3, rows=2) + (
        "Six magic skill icons: 1) three sharp light-blue ice crystal shards orbiting in a ring; "
        "2) a bright yellow zigzag lightning bolt that forks into two; "
        "3) a round exploding ring of orange and red flames; "
        "4) a purple and magenta magic comet missile with a star-shaped head and a sparkle trail; "
        "5) a golden hammer with a small crown, radiating power; "
        "6) a floating faceted rainbow crystal gem.")),
    "upgrades_a": dict(aspect="16:9", n=2, prompt=STYLE + GRID.format(cols=3, rows=2) + (
        "Six stat upgrade icons: 1) a short magic sword wrapped in orange flame (attack power); "
        "2) a brown boot with white wings (move speed); "
        "3) a glossy red heart with a white plus sign (health); "
        "4) two crossed purple magic wands with yellow star tips and speed lines (attack speed); "
        "5) a cyan wind swoosh with a double chevron arrow (dash); "
        "6) a red and white bullseye target pierced by an arrow (range).")),
    "upgrades_b": dict(aspect="16:9", n=2, prompt=STYLE + GRID.format(cols=3, rows=2) + (
        "Six stat upgrade icons: 1) a glossy blue five-pointed star with a small green up arrow (experience); "
        "2) a steel-blue kite shield with gold trim (armor); "
        "3) a red heart with small purple bat wings and two white fangs (lifesteal); "
        "4) a gold and red crosshair reticle with a sparkle in the centre (critical chance); "
        "5) a spiky orange and yellow impact burst explosion (critical damage); "
        "6) a stack of shiny gold coins embossed with a tiny dumpling.")),
    "items": dict(aspect="16:9", n=2, prompt=STYLE + GRID.format(cols=3, rows=2) + (
        "Six equipment icons of a little witch: 1) a wooden magic staff topped with a gold crescent moon holding a light-blue crystal, drawn diagonally; "
        "2) a big floppy purple witch hat with a gold band and two gold X stitches; "
        "3) a lilac robe jacket with bell sleeves and gold trim; "
        "4) a pair of short purple pants with a gold belt; "
        "5) a pair of brown leather boots with folded cuffs; "
        "6) a brown leather adventurer backpack with a gold buckle.")),
    "hud_a": dict(aspect="16:9", n=2, prompt=STYLE + GRID.format(cols=4, rows=2) + (
        "Eight HUD icons: 1) a glossy red heart; 2) a single shiny gold coin embossed with a tiny smiling dumpling, seen from the front; "
        "3) a gold trophy cup; 4) a purple and gold stopwatch; 5) a cute cream-white cartoon skull with purple eye sockets; "
        "6) a round wooden bamboo door with a gold knob; 7) a retro TV screen showing a white play triangle (watch video ad); "
        "8) a gold padlock.")),
    "hud_b": dict(aspect="16:9", n=2, prompt=STYLE + GRID.format(cols=4, rows=2) + (
        "Eight HUD icons, each drawn as a cream-white chunky glyph with a thick dark violet outline: 1) a cog gear; "
        "2) a little house (home); 3) a circular retry arrow; 4) a musical note; 5) a loudspeaker with sound waves; "
        "6) a game controller joystick; 7) a video camera; 8) a lightning speedometer gauge.")),
    # No --ref here: the account's Flow composer refuses image-to-image (gflow exit 36), so the hero is described in words.
    "avatar": dict(aspect="1:1", n=4, prompt=(
        "Game UI avatar portrait of a cute chibi witch girl (giant floppy purple witch hat with a gold jagged rim and two gold X "
        "stitches, round cream face with only two black oval eyes and no nose or mouth, blonde bob hair fading to pink at the tips, purple capelet). "
        "Head and shoulders bust, facing the viewer, slightly tilted, cheerful. 2D cartoon illustration in the polished style of modern mobile "
        "games (Brawl Stars, Cookie Run Kingdom): thick dark violet outline, glossy cel shading, vibrant colors. "
        "Flat solid pure bright green (#00FF00) chroma key background, no shadow, no frame, no text.")),
    "keyart": dict(aspect="16:9", n=4, prompt=(
        "Mobile game promotional key art, wide illustration. A cute chibi witch girl (giant floppy purple witch hat with gold jagged rim "
        "and gold X stitches, round cream face with only two black oval eyes, blonde-to-pink bob hair, purple capelet, lilac jacket, cream skirt, "
        "brown boots) leaps and swings a wooden staff topped with a gold crescent moon and a light-blue crystal, casting sparkling purple magic bolts "
        "at a crowd of cute glossy translucent jelly dumplings with kawaii faces, and one giant pink boss dumpling with a gold star on its forehead. "
        "They fight inside a huge round woven-bamboo dim sum steamer arena with a grassy floor, warm steam and glittery bokeh in the background. "
        "Polished 2D cartoon illustration in the style of modern top mobile games, thick outlines, glossy cel shading, vibrant purple, gold and "
        "mint palette, dynamic composition, the witch on the left third, empty space in the upper centre. No text, no logo, no UI.")),
    # Store icon / feature graphic companions for the witch bust.
    "mascots": dict(aspect="16:9", n=2, prompt=STYLE + GRID.format(cols=2, rows=1) + (
        "Two cute mascot characters: 1) a big glossy translucent pink jelly steamed dumpling (bao) with pleats and a twisted top knot, "
        "a kawaii face with big shiny dark eyes, rosy cheeks, a small open smiling mouth, and a gold star on its forehead; "
        "2) a small glossy mint-green jelly slime blob with two little round bumps on top, big shiny eyes and a tiny mouth.")),
}


def run(job):
    spec = JOBS[job]
    out = os.path.join(RAW, job)
    os.makedirs(out, exist_ok=True)
    refs = spec.get("refs")
    cmd = ["xvfb-run", "-a", "-s", "-screen 0 1600x1000x24", GFLOW, "image", "i2i" if refs else "t2i", spec["prompt"],
           "--aspect", spec["aspect"], "--model", spec.get("model", "nano-pro"), "-n", str(spec["n"]), "--out", out]
    for ref in refs or []:
        cmd += ["--ref", os.path.join(PREVIEWS, ref)]
    print("GFLOW_JOB", job, flush=True)
    code = subprocess.call(cmd, stdout=subprocess.DEVNULL)
    print("GFLOW_DONE", job, code, sorted(os.listdir(out)), flush=True)
    return code


def main():
    def pending(job):
        out = os.path.join(RAW, job)
        return not (os.path.isdir(out) and os.listdir(out))

    jobs = sys.argv[1:] or [job for job in JOBS if pending(job)]
    failed = [job for job in jobs if run(job) != 0]
    print("GFLOW_QUEUE_OK" if not failed else "GFLOW_QUEUE_FAIL " + " ".join(failed), flush=True)
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
